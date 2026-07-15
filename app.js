const repoBase = "https://github.com/sj97p/Shield-Shot-CodeMap/blob/main/";
const sourceBase = "src/Assets/_Project/_Scripts/";

const evidence = {
  augment: { src: "assets/evidence/projectile-augment-result.gif", caption: "증강 선택 후 projectile behavior가 실제 발사체 효과로 이어지는 장면" },
  gridScene: { src: "assets/evidence/element-field-grid-sceneview.png", caption: "ElementField data grid, fire cells, scene view debug" },
  fireGrass: { src: "assets/evidence/element-field-fire-grass.gif", caption: "Fire trail과 grass terrain 반응" },
  windSand: { src: "assets/evidence/element-field-wind-sand.gif", caption: "Wind trail과 sand terrain 반응" },
  waterIce: { src: "assets/evidence/element-field-water-ice.gif", caption: "Ice trail과 water/pond freeze 반응" },
  aimLine: { src: "assets/evidence/aim-line-projectile-alignment.gif", caption: "Aim line과 실제 projectile 경로 정렬" },
  weaponShield: { src: "assets/evidence/network-weapon-shield-spawn.gif", caption: "2클라이언트 network weapon/shield spawn" },
  pvpFeedback: { src: "assets/evidence/pvp-vfx-popup-sync.gif", caption: "PvP hit, VFX, damage popup sync" },
};

const nodes = {
  overview: system({
    title: "Gameplay / PvP System Map",
    summary: "6인 팀 프로젝트에서 게임 기획, PM, 전투/네트워크/몬스터/게임플레이 시스템 설계와 통합을 담당한 범위의 Code Map입니다.",
    intent: "전체 요구사항을 먼저 정의하고, 전투 확장 구조, 속성 필드, PvP 네트워크 전투가 서로 다른 구현처럼 흩어지지 않도록 공통 데이터 흐름과 책임 경계를 설계했습니다.",
    issue: "진행 중 프로젝트라 구현은 팀원과 분담되었고, 병합/프리팹 세팅/네트워크 lifecycle 차이로 기능이 깨지는 상황이 반복되었습니다. 따라서 핵심 설명은 제가 설계하고 직접 통합/수정한 전투, 필드, PvP 네트워크 영역으로 제한합니다.",
    final: "Projectile augment, ElementField data grid, Network projectile payload, hit/VFX/popup sync, network weapon/shield spawn을 핵심 노드로 두고 코드 근거와 시행착오를 함께 탐색할 수 있게 구성했습니다.",
    next: "Evidence와 Profiler 자료는 개발 진행에 따라 추가하고, active cell network sync와 Wind/Ice network visual full sync는 기획 확정 후 확장합니다.",
    classes: ["ElementFieldGrid", "NetworkProjectileFireHandler", "NetworkProjectileActor", "PvpMatchStateController", "ProjectileBehaviorRegistry"],
    evidence: [evidence.augment, evidence.gridScene, evidence.weaponShield, evidence.pvpFeedback],
    code: [],
    graph: `flowchart TD
      req["Game Design / PM Requirements"]
      augment["Projectile Behavior & Augment"]
      field["ElementField Grid"]
      pvp["PvP Network Combat"]
      aim["Aim Prediction"]
      spawn["Network Weapon / Shield"]
      hit["Hit / VFX / Popup Sync"]

      req --> augment
      req --> field
      req --> pvp
      augment --> pvp
      field --> pvp
      pvp --> aim
      pvp --> spawn
      pvp --> hit

      click augment call selectNode("augment")
      click field call selectNode("field")
      click pvp call selectNode("pvpProjectile")
      click aim call selectNode("aimPrediction")
      click spawn call selectNode("networkSpawn")
      click hit call selectNode("hitFeedback")`,
  }),

  augment: system({
    title: "Projectile Behavior & Augment Injection",
    summary: "투사체 효과를 조건문 누적이 아니라 외부 Behavior 주입과 우선순위 기반 실행 구조로 확장했습니다.",
    intent: "증강, 스킬, 속성 효과가 계속 추가될 것을 전제로 Projectile core를 직접 수정하지 않고 확장할 수 있어야 했습니다. 저는 이 요구사항을 설계했고, 팀원이 구현한 기본 흐름을 프로젝트 의도에 맞게 수정/통합했습니다.",
    issue: "초기 구조는 체인 라이트닝 같은 단일 스킬 추가로 시작했지만, 항상 적용되는 증강, 다음 화살 1회성 효과, 직접 타격, 충돌, 이동 중 필드 효과가 서로 다른 생명주기를 가진다는 문제가 있었습니다.",
    final: "hit/collision/movement behavior를 분리하고, PlayerStatus -> ProjectileShooter -> ProjectileBase로 이어지는 주입 흐름을 기준화했습니다. PvP에서는 이 구조가 payload code/level로 변환되어 NetworkProjectileFireHandler와 Registry를 통해 복구됩니다.",
    next: "추가 파라미터가 많은 증강은 payload 구조 확장이 필요합니다. 또한 공개용 스냅샷에서는 우선순위 정책을 더 명시적인 policy로 분리할 수 있습니다.",
    classes: ["ProjectileBehaviorSO", "ProjectileShooter", "ProjectileBase", "PlayerStatus", "ProjectileBehaviorRegistry", "PvpProjectileAugmentPayload"],
    evidence: [evidence.augment],
    code: [
      code("GameplayCore/Weapon/Projectile/ProjectileShooter.cs", "Fire", ["Fire", "TryInjectAndConsumeNextShotBehavior", "ApplyBuiltInWeaponModifiers"]),
      code("GameplayCore/Weapon/Projectile/ProjectileBase.cs", "AddHitBehavior", ["AddHitBehavior", "AddCollisionBehavior", "ExecuteHit", "ExecuteCollision"]),
      code("Network/PvP/ProjectileBehaviorRegistry.cs", "TryApply", ["TryApply", "Rebuild"]),
      code("Network/PvP/PvpProjectileAugmentPayload.cs", "Payload", ["HasAnyAugment", "Entries"]),
    ],
    graph: `flowchart TD
      status["PlayerStatus"]
      shooter["ProjectileShooter"]
      projectile["ProjectileBase"]
      hit["IHitBehavior"]
      collision["ICollisionBehavior"]
      movement["Movement Behavior"]
      registry["ProjectileBehaviorRegistry"]
      payload["PvpProjectileAugmentPayload"]

      status --> shooter --> projectile
      projectile --> hit
      projectile --> collision
      projectile --> movement
      payload --> registry --> projectile

      click shooter call selectNode("ProjectileShooter")
      click projectile call selectNode("ProjectileBase")
      click registry call selectNode("ProjectileBehaviorRegistry")
      click payload call selectNode("PvpProjectileAugmentPayload")`,
  }),

  field: system({
    title: "ElementField Grid & Terrain Reaction",
    summary: "GameObject cell 중심 구현을 데이터 그리드로 전환해 속성 장판, terrain reaction, spawn/camera/wall 기준을 통합했습니다.",
    intent: "Fire/Wind/Ice 효과가 단순 hit VFX가 아니라 필드와 terrain에 남아 전투 규칙에 영향을 주도록 설계했습니다. ElementFieldGrid를 아레나 좌표계의 기준으로 삼았습니다.",
    issue: "cell마다 GameObject/Collider를 유지하면 비용이 커지고, 큰 몬스터는 pivot 기준으로 장판을 놓칠 수 있었습니다. Terrain, Camera, Spawn, Wall 기준이 분리되면 projectile trail과 필드 반응 위치도 어긋납니다.",
    final: "ElementFieldCellData[,]를 중심으로 Paint/PaintCircle, terrain state, reaction resolver, visual controller, effect system을 분리했습니다. Fire/Wind/Ice 및 Water/Pond freeze까지 같은 grid 기준에서 처리합니다.",
    next: "Active cell network sync는 아직 보류입니다. 현재는 local/arena gameplay 기준으로 안정화하고, PvP에서 cell state가 직접 전투 판정에 쓰이는 기획이 확정되면 network state로 올립니다.",
    classes: ["ElementFieldGrid", "ElementFieldCellData", "ElementReactionResolver", "ElementFieldEffectSystem", "ElementFieldVisualController", "ArenaTerrainPainter"],
    evidence: [evidence.gridScene, evidence.fireGrass, evidence.windSand, evidence.waterIce],
    code: [
      code("GameplayCore/Field/ElementFieldGrid.cs", "Paint", ["Paint", "PaintCircle", "PaintCell", "TickActiveCells", "RefreshHotWindFireSpread"]),
      code("GameplayCore/Field/ElementReactionResolver.cs", "Resolve", ["Resolve"]),
      code("GameplayCore/Field/ElementFieldEffectSystem.cs", "Sample", ["RefreshTargets", "TryGetBestEffectCell", "BuildSamplePoints"]),
      code("GameplayCore/Field/ArenaTerrainPainter.cs", "GenerateThemeTerrain", ["GenerateThemeTerrain", "GeneratePonds", "RefreshFrozenWaterSurfaceMaterials"]),
    ],
    graph: `flowchart TD
      projectile["Projectile Trail"]
      grid["ElementFieldGrid"]
      data["ElementFieldCellData[,]"]
      reaction["ElementReactionResolver"]
      visual["ElementFieldVisualController"]
      effect["ElementFieldEffectSystem"]
      terrain["ArenaTerrainPainter"]

      projectile --> grid --> data
      data --> reaction
      reaction --> visual
      data --> effect
      data --> terrain

      click grid call selectNode("ElementFieldGrid")
      click data call selectNode("ElementFieldCellData")
      click reaction call selectNode("ElementReactionResolver")
      click effect call selectNode("ElementFieldEffectSystem")
      click terrain call selectNode("ArenaTerrainPainter")`,
  }),

  pvpProjectile: system({
    title: "PvP Network Projectile Sync",
    summary: "로컬 projectile과 다른 네트워크 lifecycle에서도 증강, damage, hit, VFX가 동작하도록 payload와 RPC 경로를 분리했습니다.",
    intent: "로컬 전투의 확장 구조를 Fusion PvP에 그대로 끌고 오지 않고, InputAuthority/StateAuthority 기준으로 발사, payload, spawn, feedback을 재설계했습니다.",
    issue: "네트워크 projectile에는 local context나 Launcher가 없을 수 있고, SO 참조를 그대로 전송할 수도 없습니다. 또한 projectile이 despawn되면 VFX 호출 지점이 사라질 수 있습니다.",
    final: "NetworkProjectileFireHandler가 payload를 만들고 Runner.Spawn으로 projectile을 생성합니다. ProjectileBehaviorRegistry가 code/level을 runtime behavior로 복구하고, NetworkProjectileActor가 hit/collision event를 RPC로 broadcast합니다.",
    next: "Wind/Ice network visual full sync는 기반 마련 상태이며 proxy visual 최종 검증은 추후 항목입니다. payload 파라미터가 늘어날 경우 압축/버전 정책이 필요합니다.",
    classes: ["NetworkProjectileFireHandler", "NetworkProjectileActor", "PvpProjectileAugmentPayload", "PvpProjectileAugmentEntry", "ProjectileBehaviorRegistry"],
    evidence: [evidence.pvpFeedback, evidence.weaponShield],
    code: [
      code("Network/PvP/NetworkProjectileFireHandler.cs", "Fire", ["Fire", "RPC_RequestFire", "SpawnProjectile", "ApplyProjectileBehaviors", "CreateAugmentPayload"]),
      code("Network/PvP/NetworkProjectileActor.cs", "FixedUpdateNetwork", ["FixedUpdateNetwork", "RPC_PlayHitVfx", "RPC_PlayCollisionVfx", "SetElementVisual"]),
      code("Network/PvP/PvpProjectileAugmentEntry.cs", "Entry", ["BehaviorCode", "Level", "ElementType"]),
    ],
    graph: `flowchart TD
      input["InputAuthority Fire"]
      handler["NetworkProjectileFireHandler"]
      rpc["RPC_RequestFire"]
      spawn["Runner.Spawn"]
      payload["PvpProjectileAugmentPayload"]
      registry["ProjectileBehaviorRegistry"]
      actor["NetworkProjectileActor"]
      feedback["VFX / Popup RPC"]

      input --> handler --> rpc --> spawn
      handler --> payload --> registry --> spawn
      spawn --> actor --> feedback

      click handler call selectNode("NetworkProjectileFireHandler")
      click actor call selectNode("NetworkProjectileActor")
      click payload call selectNode("PvpProjectileAugmentPayload")
      click registry call selectNode("ProjectileBehaviorRegistry")`,
  }),

  aimPrediction: system({
    title: "Aim Line / Projectile Alignment",
    summary: "Aim line과 실제 network projectile의 시작점, collision radius, wall layer 기준을 통일했습니다.",
    intent: "PvP에서 조준선은 단순 시각 보조가 아니라 전투 예측의 기준입니다. 실제 projectile 판정과 같은 시작점/반경/layer를 공유해야 했습니다.",
    issue: "처음에는 방향 변환 문제처럼 보였지만, network projectile은 spawnForwardOffset이 있고, prefab radius도 로컬 projectile과 달랐습니다. 일부 prefab은 PvpWall layer도 감지하지 못했습니다.",
    final: "IProjectileAimPredictionProvider를 통해 현재 fire handler가 실제 spawn origin과 projectile radius를 제공합니다. AimLineRenderer는 provider가 있으면 그 값을 우선 사용합니다.",
    next: "RandomReflect는 의도적으로 정확 예측 대상에서 제외합니다. 벽 collider 두께와 visual scale 차이는 prefab tuning과 함께 계속 조정해야 합니다.",
    classes: ["AimLineRenderer", "IProjectileAimPredictionProvider", "NetworkProjectileFireHandler", "WeaponBase"],
    evidence: [evidence.aimLine],
    code: [
      code("GameplayCore/Weapon/Aim/AimLineRenderer.cs", "UpdateAimLine", ["UpdateAimLine", "TryGetProjectileCollisionRadius", "GetPredictedProjectileOrigin"]),
      code("Network/PvP/NetworkProjectileFireHandler.cs", "GetPredictedProjectileOrigin", ["GetPredictedProjectileOrigin", "TryGetProjectileCollisionRadius", "GetSpawnPosition"]),
      code("GameplayCore/Weapon/Core/WeaponBase.cs", "ProjectileFireHandler", ["ProjectileFireHandler", "ResolveProjectileFireHandler"]),
    ],
    graph: `flowchart TD
      aim["AimLineRenderer"]
      weapon["WeaponBase"]
      handler["ProjectileFireHandler"]
      provider["IProjectileAimPredictionProvider"]
      origin["Actual Spawn Origin"]
      radius["Actual Projectile Radius"]
      cast["SphereCast Prediction"]
      wall["Wall / PvpWall"]

      aim --> weapon --> handler --> provider
      provider --> origin --> cast
      provider --> radius --> cast
      cast --> wall

      click aim call selectNode("AimLineRenderer")
      click weapon call selectNode("WeaponBase")
      click handler call selectNode("NetworkProjectileFireHandler")`,
  }),

  hitFeedback: system({
    title: "PvP Hit Detection / VFX / Damage Popup",
    summary: "PvP hit가 안 되던 문제를 hitbox/layer 단계에서 복구하고, VFX와 damage popup을 모든 peer에 동기화했습니다.",
    intent: "네트워크 전투에서는 hit 판정, damage 적용, VFX, popup이 같은 사건처럼 보이지만 실제로는 단계가 다릅니다. 각 단계를 분리해 문제 원인을 좁히고 RPC feedback 경로를 만들었습니다.",
    issue: "화살이 상대를 통과했지만 target candidate 로그가 없었습니다. 원인은 damage 로직이 아니라 Player hitbox localPosition override와 모든 자식 collider를 PvpWeapon layer로 바꾸던 보정 부작용이었습니다.",
    final: "Player hitbox만 정규화하고 해당 collider만 target layer로 설정했습니다. PvpWeaponHitTarget은 projectile damage/critical을 PvpWeaponHealth로 넘기고, Health는 damage popup RPC를 호출합니다.",
    next: "진단 로그는 개발 중에는 유용하지만 공개/릴리즈 전에는 debug flag나 conditional log로 정리하는 것이 좋습니다.",
    classes: ["PvpWeaponActorIdentity", "PvpWeaponHitTarget", "PvpWeaponHealth", "NetworkProjectileActor", "DamagePopupManager"],
    evidence: [evidence.pvpFeedback],
    code: [
      code("Network/PvP/PvpWeaponActorIdentity.cs", "Normalize", ["NormalizeHitTarget", "ApplySpawnPose", "Spawned"]),
      code("Network/PvP/PvpWeaponHitTarget.cs", "ApplyHit", ["CanBeHitBy", "ApplyHit"]),
      code("Network/PvP/PvpWeaponHealth.cs", "ApplyDamage", ["ApplyDamage", "RPC_ShowDamagePopup", "HandleDeath"]),
      code("Network/PvP/NetworkProjectileActor.cs", "RPC_PlayHitVfx", ["OnProjectileHitExecuted", "RPC_PlayHitVfx", "RPC_PlayCollisionVfx"]),
    ],
    graph: `flowchart TD
      projectile["ProjectileBase SphereCast"]
      candidate["Target Candidate"]
      target["PvpWeaponHitTarget"]
      health["PvpWeaponHealth"]
      popup["Damage Popup RPC"]
      vfx["Hit / Collision VFX RPC"]
      identity["PvpWeaponActorIdentity"]

      identity --> candidate
      projectile --> candidate --> target --> health --> popup
      projectile --> vfx

      click identity call selectNode("PvpWeaponActorIdentity")
      click target call selectNode("PvpWeaponHitTarget")
      click health call selectNode("PvpWeaponHealth")
      click vfx call selectNode("NetworkProjectileActor")`,
  }),

  networkSpawn: system({
    title: "Network Weapon & Shield Spawn",
    summary: "로비 장착 데이터를 PvP 원격 클라이언트에서도 WeaponId/ShieldId 기반으로 복구하는 구조입니다.",
    intent: "원격 클라이언트는 상대의 로컬 item instance를 가질 수 없으므로, 장착 상태를 ID 기반 network state로 전달하고 각 peer가 같은 prefab을 복구해야 했습니다.",
    issue: "병합 후 WeaponCore_Network에서 활/방패 생성이 다시 깨졌고, shield는 생성되지만 SkillShield 참조 또는 network prefab 구성이 맞지 않았습니다.",
    final: "WeaponCore_Network를 actor 중심으로 두고, NetworkWeaponManager와 NetworkShieldSpawnSetup이 ID 기반으로 network prefab을 복구하도록 정리했습니다. 방패 충돌은 NetworkShieldActor/Detector에서 authority 기준으로 처리합니다.",
    next: "현재는 Resources 기반 prefab loading을 사용합니다. 프로젝트가 커지면 Addressables나 명시적 catalog 검증 도구를 고려할 수 있습니다.",
    classes: ["NetworkWeaponManager", "NetworkShieldSpawnSetup", "NetworkShieldActor", "NetworkShieldColliderDetector", "PvpWeaponActorIdentity"],
    evidence: [evidence.weaponShield],
    code: [
      code("Network/PvP/NetworkWeaponManager.cs", "ResolveNetworkWeaponPrefabFromResources", ["RequestLoadoutSync", "RPC_SubmitLoadout", "ResolveNetworkWeaponPrefabFromResources", "SpawnWeaponVisual"]),
      code("Network/PvP/NetworkShieldSpawnSetup.cs", "SpawnShield", ["SendShieldLoadoutToStateAuthority", "RPC_SubmitShieldLoadout", "SpawnShield", "ResolveShieldData"]),
      code("Network/PvP/NetworkShieldActor.cs", "NotifyShieldHit", ["NotifyShieldHit", "RPC_PlayReflectVfx", "InjectLocalShieldReferences"]),
      code("Network/PvP/NetworkShieldColliderDetector.cs", "OnTriggerEnter", ["OnTriggerEnter", "IsProjectileLayer", "TryGetProjectile"]),
    ],
    graph: `flowchart TD
      data["PlayerDataManager Equipped Data"]
      ids["WeaponId / ShieldId"]
      state["Networked State"]
      weapon["NetworkWeaponManager"]
      shieldSetup["NetworkShieldSpawnSetup"]
      shieldActor["NetworkShieldActor"]
      actor["WeaponCore_Network"]

      data --> ids --> state
      state --> weapon --> actor
      state --> shieldSetup --> shieldActor --> actor

      click weapon call selectNode("NetworkWeaponManager")
      click shieldSetup call selectNode("NetworkShieldSpawnSetup")
      click shieldActor call selectNode("NetworkShieldActor")
      click actor call selectNode("PvpWeaponActorIdentity")`,
  }),
};

function system(data) {
  return { kind: "System", ...data };
}

function code(path, focus, methods) {
  return { path, focus, methods };
}

const classDocs = {
  ElementFieldGrid: classDoc("ElementFieldGrid", "속성 필드의 cell data와 좌표 변환, paint 진입점을 관리합니다.", "GameplayCore/Field/ElementFieldGrid.cs", ["BuildGrid", "Paint", "PaintCircle", "WorldToCell", "CellToWorld", "TickActiveCells"]),
  ElementFieldCellData: classDoc("ElementFieldCellData", "cell 하나의 terrain/current element 상태를 저장하는 데이터 구조입니다.", "GameplayCore/Field/ElementFieldCellData.cs", ["ElementFieldCellData"]),
  ElementReactionResolver: classDoc("ElementReactionResolver", "terrain/current/incoming element 조합의 반응 결과를 계산합니다.", "GameplayCore/Field/ElementReactionResolver.cs", ["Resolve"]),
  ElementFieldEffectSystem: classDoc("ElementFieldEffectSystem", "대상 collider bounds를 sample해 field 상태이상을 적용합니다.", "GameplayCore/Field/ElementFieldEffectSystem.cs", ["RefreshTargets", "TryGetBestEffectCell"]),
  ArenaTerrainPainter: classDoc("ArenaTerrainPainter", "grid terrain data를 Unity Terrain, pond, water surface로 변환합니다.", "GameplayCore/Field/ArenaTerrainPainter.cs", ["GenerateThemeTerrain", "GeneratePonds", "RefreshFrozenWaterSurfaceMaterials"]),
  ProjectileBehaviorSO: classDoc("ProjectileBehaviorSO", "투사체에 주입 가능한 behavior ScriptableObject의 기준입니다.", "GameplayCore/Weapon/Projectile/ProjectileBehaviorSO.cs", ["CanInject", "Inject"]),
  ProjectileShooter: classDoc("ProjectileShooter", "로컬 projectile 생성과 behavior 주입 흐름을 담당합니다.", "GameplayCore/Weapon/Projectile/ProjectileShooter.cs", ["Fire", "ApplyBuiltInWeaponModifiers"]),
  ProjectileBase: classDoc("ProjectileBase", "projectile movement, hit/collision behavior 실행 중심입니다.", "GameplayCore/Weapon/Projectile/ProjectileBase.cs", ["Simulate", "AddHitBehavior", "AddCollisionBehavior", "ExecuteHit", "ExecuteCollision"]),
  AimLineRenderer: classDoc("AimLineRenderer", "조준 예측선을 계산하고 wall reflection 경로를 표시합니다.", "GameplayCore/Weapon/Aim/AimLineRenderer.cs", ["UpdateAimLine", "TryGetProjectileCollisionRadius"]),
  WeaponBase: classDoc("WeaponBase", "무기 데이터와 fire handler, aim/charge/visual 컴포넌트를 연결합니다.", "GameplayCore/Weapon/Core/WeaponBase.cs", ["ProjectileFireHandler", "ResolveProjectileFireHandler"]),
  NetworkProjectileFireHandler: classDoc("NetworkProjectileFireHandler", "PvP network fire request, projectile spawn, payload 적용, aim prediction provider를 담당합니다.", "Network/PvP/NetworkProjectileFireHandler.cs", ["Fire", "RPC_RequestFire", "SpawnProjectile", "ApplyProjectileBehaviors", "CreateAugmentPayload", "GetPredictedProjectileOrigin"]),
  NetworkProjectileActor: classDoc("NetworkProjectileActor", "network projectile simulation과 hit/collision VFX RPC를 담당합니다.", "Network/PvP/NetworkProjectileActor.cs", ["FixedUpdateNetwork", "RPC_PlayHitVfx", "RPC_PlayCollisionVfx", "SetElementVisual"]),
  PvpProjectileAugmentPayload: classDoc("PvpProjectileAugmentPayload", "network projectile에 적용할 augment entry 묶음입니다.", "Network/PvP/PvpProjectileAugmentPayload.cs", ["HasAnyAugment"]),
  PvpProjectileAugmentEntry: classDoc("PvpProjectileAugmentEntry", "behavior code, level, element type을 담는 network entry입니다.", "Network/PvP/PvpProjectileAugmentEntry.cs", ["BehaviorCode", "Level"]),
  ProjectileBehaviorRegistry: classDoc("ProjectileBehaviorRegistry", "payload code/level을 runtime projectile behavior로 변환합니다.", "Network/PvP/ProjectileBehaviorRegistry.cs", ["TryApply", "Rebuild"]),
  NetworkWeaponManager: classDoc("NetworkWeaponManager", "WeaponId/WeaponType 기반 network weapon prefab 복구를 담당합니다.", "Network/PvP/NetworkWeaponManager.cs", ["RequestLoadoutSync", "RPC_SubmitLoadout", "ResolveNetworkWeaponPrefabFromResources", "SpawnWeaponVisual"]),
  NetworkShieldSpawnSetup: classDoc("NetworkShieldSpawnSetup", "ShieldId 기반 network shield prefab 복구를 담당합니다.", "Network/PvP/NetworkShieldSpawnSetup.cs", ["SendShieldLoadoutToStateAuthority", "RPC_SubmitShieldLoadout", "SpawnShield"]),
  NetworkShieldActor: classDoc("NetworkShieldActor", "network shield hit/reflect authority 처리를 담당합니다.", "Network/PvP/NetworkShieldActor.cs", ["NotifyShieldHit", "RPC_PlayReflectVfx"]),
  NetworkShieldColliderDetector: classDoc("NetworkShieldColliderDetector", "shield trigger에서 network projectile을 감지합니다.", "Network/PvP/NetworkShieldColliderDetector.cs", ["OnTriggerEnter"]),
  PvpWeaponActorIdentity: classDoc("PvpWeaponActorIdentity", "PvP actor의 owner/side/spawn pose/hitbox 기준을 관리합니다.", "Network/PvP/PvpWeaponActorIdentity.cs", ["Spawned", "ApplySpawnPose"]),
  PvpWeaponHitTarget: classDoc("PvpWeaponHitTarget", "PvP projectile hit 가능 여부와 damage 전달을 담당합니다.", "Network/PvP/PvpWeaponHitTarget.cs", ["CanBeHitBy", "ApplyHit"]),
  PvpWeaponHealth: classDoc("PvpWeaponHealth", "PvP HP, damage, death/score, damage popup RPC를 담당합니다.", "Network/PvP/PvpWeaponHealth.cs", ["ApplyDamage", "RPC_ShowDamagePopup", "HandleDeath"]),
  PvpMatchStateController: classDoc("PvpMatchStateController", "PvP countdown/fight/round/augment/match state를 network state로 관리합니다.", "Network/PvP/PvpMatchStateController.cs", ["StartCountdown", "StartFight", "AddScore", "NotifyLocalAugmentSelectionCompleted"]),
};

function classDoc(title, summary, path, methods) {
  return {
    kind: "Class",
    title,
    summary,
    intent: "이 클래스는 선택된 시스템의 책임을 코드 레벨에서 확인하기 위한 핵심 근거입니다.",
    issue: "공개 스냅샷은 전체 프로젝트가 아니라 포트폴리오 범위의 코드만 포함하므로, 일부 참조 타입은 원본 프로젝트 문맥에 의존합니다.",
    final: `${path} 파일에서 주요 변수와 함수 흐름을 확인할 수 있습니다.`,
    next: "추후 문서화 단계에서 클래스별 책임과 공개 snippet 범위를 더 세분화할 수 있습니다.",
    classes: [],
    evidence: [],
    code: [code(path, methods[0], methods)],
    graph: classGraph(title, methods),
  };
}

function classGraph(name, methods) {
  const methodLines = methods.map((m) => `      ${m}["${m}()"]`).join("\\n");
  const clicks = methods.map((m) => `      click ${m} call focusMethod("${name}","${m}")`).join("\\n");
  return `flowchart TD
      cls["${name}"]
${methodLines}
      cls --> ${methods.join("\\n      cls --> ")}
${clicks}`;
}

const treeGroups = [
  ["Overview", ["overview"]],
  ["Core Systems", ["augment", "field", "pvpProjectile", "aimPrediction", "hitFeedback", "networkSpawn"]],
];

let historyStack = [];
let currentId = "overview";
let currentCodeLines = [];

const els = {};

document.addEventListener("DOMContentLoaded", () => {
  bindElements();
  if (window.mermaid) {
    window.mermaid.initialize({ startOnLoad: false, securityLevel: "loose", theme: "default" });
  }
  renderTree();
  bindModal();
  bindResizers();
  document.querySelector("#reset-view").addEventListener("click", () => selectNode("overview"));
  document.querySelector("#graph-back").addEventListener("click", goBack);
  selectNode("overview", false);
});

function bindElements() {
  for (const id of [
    "tree", "scope-label", "graph-title", "graph", "graph-back", "breadcrumbs",
    "detail-kind", "detail-title", "detail-summary", "detail-intent",
    "detail-issue", "detail-final", "detail-next", "evidence-list",
    "class-list", "code-link", "code-methods", "code-preview", "code-scroll",
    "media-modal", "modal-close", "modal-image", "modal-caption"
  ]) {
    els[id] = document.getElementById(id);
  }
}

function renderTree() {
  els.tree.innerHTML = "";
  for (const [group, ids] of treeGroups) {
    const label = document.createElement("p");
    label.className = "label";
    label.textContent = group;
    els.tree.appendChild(label);
    for (const id of ids) {
      const node = nodes[id];
      const button = document.createElement("button");
      button.type = "button";
      button.dataset.node = id;
      button.innerHTML = `<strong>${node.title}</strong><span>${node.summary}</span>`;
      button.addEventListener("click", () => selectNode(id));
      els.tree.appendChild(button);
    }
  }
}

async function selectNode(id, pushHistory = true) {
  const node = nodes[id] || classDocs[id] || nodes.overview;
  if (pushHistory && currentId !== id) historyStack.push(currentId);
  currentId = id;

  document.querySelectorAll(".tree button").forEach((button) => {
    button.classList.toggle("active", button.dataset.node === id);
  });
  els["graph-back"].disabled = historyStack.length === 0;
  els.breadcrumbs.textContent = id === "overview" ? "Overview" : `Overview / ${node.title}`;

  els["scope-label"].textContent = node.kind;
  els["graph-title"].textContent = node.title;
  els["detail-kind"].textContent = node.kind;
  els["detail-title"].textContent = node.title;
  els["detail-summary"].textContent = node.summary;
  els["detail-intent"].textContent = node.intent;
  els["detail-issue"].textContent = node.issue;
  els["detail-final"].textContent = node.final;
  els["detail-next"].textContent = node.next;

  renderEvidence(node.evidence);
  renderClasses(node.classes);
  await renderCode(node.code);
  await renderGraph(node.graph);
}

function goBack() {
  const previous = historyStack.pop();
  if (previous) selectNode(previous, false);
}

async function renderGraph(graph) {
  els.graph.removeAttribute("data-processed");
  els.graph.textContent = graph;
  if (window.mermaid) await window.mermaid.run({ nodes: [els.graph] });
}

function renderEvidence(items) {
  els["evidence-list"].innerHTML = "";
  if (!items || items.length === 0) {
    els["evidence-list"].innerHTML = `<p class="muted">이 노드에는 별도 Evidence가 없습니다.</p>`;
    return;
  }
  for (const item of items) {
    const card = document.createElement("button");
    card.type = "button";
    card.className = "evidence-card";
    card.innerHTML = `<img src="${item.src}" alt="${item.caption}"><span>${item.caption}</span>`;
    card.addEventListener("click", () => openModal(item));
    els["evidence-list"].appendChild(card);
  }
}

function renderClasses(classes) {
  els["class-list"].innerHTML = "";
  for (const name of classes || []) {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "chip";
    chip.textContent = name;
    chip.addEventListener("click", () => selectNode(name));
    els["class-list"].appendChild(chip);
  }
}

async function renderCode(files) {
  els["code-methods"].innerHTML = "";
  els["code-link"].href = "#";
  if (!files || files.length === 0) {
    currentCodeLines = [];
    els["code-preview"].innerHTML = "시스템이나 클래스를 선택하면 관련 코드가 표시됩니다.";
    return;
  }
  const active = files[0];
  await loadCode(active);
}

async function loadCode(fileInfo, focusMethod = null) {
  const path = sourceBase + fileInfo.path;
  els["code-link"].href = repoBase + path.replaceAll("\\\\", "/");
  try {
    const response = await fetch(path);
    if (!response.ok) throw new Error(String(response.status));
    const text = await response.text();
    currentCodeLines = text.split("\\n");
    renderMethodButtons(fileInfo);
    renderHighlightedCode(currentCodeLines, focusMethod || fileInfo.focus);
  } catch (error) {
    currentCodeLines = [];
    els["code-preview"].textContent =
      `코드 스냅샷을 불러오지 못했습니다. GitHub Pages 배포 후에는 정상 표시됩니다.\\n\\nExpected: ${path}`;
  }
}

function renderMethodButtons(fileInfo) {
  els["code-methods"].innerHTML = "";
  for (const method of fileInfo.methods || []) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "method-btn";
    button.textContent = method;
    button.addEventListener("click", () => renderHighlightedCode(currentCodeLines, method));
    els["code-methods"].appendChild(button);
  }
}

function renderHighlightedCode(lines, focus = null) {
  const hitLine = focus ? findLine(lines, focus) : 1;
  const start = Math.max(1, hitLine - 14);
  const end = Math.min(lines.length, hitLine + 34);
  const html = [];
  for (let i = start; i <= end; i++) {
    const hit = i === hitLine ? " line-hit" : "";
    html.push(`<span class="code-line${hit}" data-line="${i}"><span class="line-no">${i}</span><span class="line-code">${highlight(lines[i - 1] || "")}</span></span>`);
  }
  els["code-preview"].innerHTML = html.join("");
  const hit = els["code-preview"].querySelector(".line-hit");
  if (hit) hit.scrollIntoView({ block: "center" });
}

function findLine(lines, symbol) {
  const rx = new RegExp(`\\\\b${escapeRegExp(symbol)}\\\\b`);
  const index = lines.findIndex((line) => rx.test(line));
  return index >= 0 ? index + 1 : 1;
}

function highlight(line) {
  const escaped = line
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
  if (escaped.trim().startsWith("//")) return `<span class="com">${escaped}</span>`;
  return escaped
    .replace(/(\".*?\")/g, '<span class="str">$1</span>')
    .replace(/\\b(public|private|protected|internal|class|struct|sealed|static|readonly|void|bool|int|float|string|return|if|else|for|foreach|while|switch|case|break|continue|new|using|namespace|override|virtual|event|delegate|out|ref|in|try|catch)\\b/g, '<span class="kw">$1</span>')
    .replace(/\\b(Vector2Int|Vector3|Quaternion|NetworkBehaviour|NetworkObject|Networked|Rpc|TickTimer|NetworkBool|PlayerRef|GameObject|Transform|MonoBehaviour|SerializeField)\\b/g, '<span class="type">$1</span>')
    .replace(/\\b(\\d+(?:\\.\\d+)?)\\b/g, '<span class="num">$1</span>');
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\\]\\\\]/g, "\\\\$&");
}

function focusMethod(className, method) {
  const doc = classDocs[className];
  if (!doc || !doc.code || !doc.code[0]) return;
  selectNode(className).then(() => {
    setTimeout(() => renderHighlightedCode(currentCodeLines, method), 120);
  });
}

function openModal(item) {
  els["modal-image"].src = item.src;
  els["modal-image"].alt = item.caption;
  els["modal-caption"].textContent = item.caption;
  els["media-modal"].setAttribute("aria-hidden", "false");
}

function bindModal() {
  els["modal-close"].addEventListener("click", closeModal);
  els["media-modal"].addEventListener("click", (event) => {
    if (event.target === els["media-modal"]) closeModal();
  });
}

function closeModal() {
  els["media-modal"].setAttribute("aria-hidden", "true");
}

function bindResizers() {
  let active = null;
  document.querySelectorAll(".splitter").forEach((splitter) => {
    splitter.addEventListener("mousedown", (event) => {
      active = splitter.dataset.resizer;
      document.body.style.userSelect = "none";
      event.preventDefault();
    });
  });
  window.addEventListener("mousemove", (event) => {
    if (!active) return;
    const width = window.innerWidth;
    if (active === "graph-detail") {
      const graph = Math.max(300, event.clientX - 260);
      document.documentElement.style.setProperty("--graph-col", `${graph}px`);
    } else if (active === "detail-code") {
      const code = Math.max(360, width - event.clientX - 20);
      document.documentElement.style.setProperty("--code-col", `${code}px`);
    }
  });
  window.addEventListener("mouseup", () => {
    active = null;
    document.body.style.userSelect = "";
  });
}

window.selectNode = selectNode;
window.focusMethod = focusMethod;
