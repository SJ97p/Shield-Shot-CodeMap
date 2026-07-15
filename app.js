const repoBase = "https://github.com/sj97p/Shield-Shot-CodeMap/blob/main/";

const systems = {
  overview: {
    title: "Gameplay System Design Map",
    short: "전체 구조",
    summary:
      "기획, PM, 전투/네트워크/몬스터/게임플레이 시스템 설계를 통합해 진행한 Shield & Shot의 핵심 코드맵입니다.",
    intent:
      "단일 기능 구현보다 팀 프로젝트의 주요 요구사항과 시스템 책임을 먼저 정리하고, 각 구현이 같은 방향으로 연결되도록 설계했습니다.",
    postmortem:
      "진행 중인 프로젝트라 병합, prefab 설정, 네트워크 lifecycle 차이로 기능이 깨지는 경우가 많았습니다. 임시 대응보다 구조의 기준을 유지하며 복구하는 쪽을 선택했습니다.",
    result:
      "Projectile augment, ElementField, PvP network projectile/hit/VFX, network weapon/shield spawn을 중심으로 플레이 가능 수준의 구조를 통합했습니다.",
    classes: [
      "ElementFieldGrid",
      "NetworkProjectileFireHandler",
      "NetworkProjectileActor",
      "PvpMatchStateController",
      "ProjectileBehaviorRegistry",
    ],
    files: [],
    graph: `flowchart TD
      req["Game Design / PM Requirements"]
      combat["Projectile Behavior & Augment"]
      field["ElementField Grid"]
      pvp["PvP Network Combat"]
      weapon["Network Weapon / Shield"]
      feedback["Hit / VFX / Popup Sync"]
      post["Technical Postmortems"]

      req --> combat
      req --> field
      req --> pvp
      combat --> pvp
      field --> pvp
      pvp --> weapon
      pvp --> feedback
      post --> combat
      post --> field
      post --> pvp

      click combat call selectSystem("augment")
      click field call selectSystem("field")
      click pvp call selectSystem("projectile-sync")
      click weapon call selectSystem("weapon-shield")
      click feedback call selectSystem("hit-recovery")`,
  },
  augment: {
    title: "Projectile Behavior & Augment Injection",
    short: "투사체 / 증강",
    summary:
      "증강 효과를 ProjectileBase 조건문에 누적하지 않고 hit/collision/movement behavior로 분리해 주입하는 구조입니다.",
    intent:
      "투사체 효과는 외부에서 주입 가능한 Behavior로 확장되어야 하며, 증강/스킬/속성 효과는 우선순위와 실행 시점에 따라 조립되어야 한다는 요구사항을 설계했습니다.",
    postmortem:
      "가장 어려웠던 부분은 증강 적용 순서였습니다. 반사, 관통, 분열, 데미지 변형, 상태이상, 속성 trail은 모두 projectile에 붙지만 실행 시점이 달라 분리 기준이 필요했습니다.",
    result:
      "항상 적용되는 증강, 다음 화살 1회성 스킬, 직접 타격, 충돌, 이동 중 필드 효과를 분리해 이후 증강 추가가 가능한 구조를 만들었습니다.",
    classes: ["ProjectileBehaviorSO", "ProjectileShooter", "ProjectileBase", "PlayerStatus", "ProjectileBehaviorRegistry"],
    files: [
      "GameplayCore/Weapon/Projectile/ProjectileBehaviorSO.cs",
      "GameplayCore/Weapon/Projectile/ProjectileShooter.cs",
      "Network/PvP/ProjectileBehaviorRegistry.cs",
      "Network/PvP/PvpProjectileAugmentPayload.cs",
    ],
    graph: `flowchart TD
      augment["Selected Augment"] --> status["PlayerStatus"]
      status --> shooter["ProjectileShooter"]
      shooter --> projectile["ProjectileBase"]
      projectile --> hit["IHitBehavior"]
      projectile --> collision["ICollisionBehavior"]
      projectile --> movement["Movement Behavior"]
      hit --> damage["Damage / DOT / Chain / Freeze"]
      collision --> reflect["Reflect / Split"]
      movement --> field["ElementField Paint"]`,
  },
  field: {
    title: "ElementField Grid & Terrain Reaction",
    short: "속성 필드",
    summary:
      "GameObject cell 중심 구현을 데이터 그리드로 전환해 속성 장판, terrain reaction, spawn/camera/wall 기준을 통합했습니다.",
    intent:
      "Fire/Wind/Ice 같은 속성 효과는 단순 타격이 아니라 필드와 terrain에 남아야 했습니다. 이를 위해 ElementFieldGrid를 아레나 기준 좌표계로 설계했습니다.",
    postmortem:
      "cell마다 GameObject/Collider를 유지하면 비용과 판정 문제가 커졌습니다. 큰 몬스터는 pivot 기준으로 장판을 놓칠 수 있어 collider bounds sampling과 data grid 전환이 필요했습니다.",
    result:
      "`ElementFieldCellData[,]`를 중심으로 Paint, PaintCircle, terrain reaction, visual update, field effect를 분리했습니다.",
    classes: ["ElementFieldGrid", "ElementFieldCellData", "ElementReactionResolver", "ElementFieldEffectSystem", "ArenaTerrainPainter"],
    files: [
      "GameplayCore/Field/ElementFieldGrid.cs",
      "GameplayCore/Field/ElementFieldCellData.cs",
      "GameplayCore/Field/ElementReactionResolver.cs",
      "GameplayCore/Field/ElementFieldEffectSystem.cs",
      "GameplayCore/Field/ArenaTerrainPainter.cs",
    ],
    graph: `flowchart TD
      projectile["Projectile Movement"] --> paint["ElementFieldGrid.Paint"]
      paint --> data["ElementFieldCellData[,]"]
      data --> reaction["ElementReactionResolver"]
      reaction --> visual["ElementFieldVisualController"]
      data --> effect["ElementFieldEffectSystem"]
      data --> terrain["ArenaTerrainPainter"]`,
  },
  "projectile-sync": {
    title: "PvP Network Projectile Sync",
    short: "PvP 투사체",
    summary:
      "로컬 projectile과 다른 네트워크 lifecycle에서도 증강, damage, hit, VFX가 동작하도록 payload와 RPC 경로를 분리했습니다.",
    intent:
      "로컬 전투의 확장 구조를 PvP에 그대로 끌고 오지 않고, Fusion의 StateAuthority/InputAuthority 흐름에 맞는 network projectile 구조로 재설계했습니다.",
    postmortem:
      "네트워크 projectile에는 local context나 Launcher가 없을 수 있었고, SO 참조를 그대로 보낼 수도 없었습니다. 그래서 behavior code/level payload와 registry 변환 구조가 필요했습니다.",
    result:
      "NetworkProjectileFireHandler가 fire request와 payload를 만들고, NetworkProjectileActor가 tick simulation과 VFX RPC를 담당하도록 정리했습니다.",
    classes: ["NetworkProjectileFireHandler", "PvpProjectileAugmentPayload", "ProjectileBehaviorRegistry", "NetworkProjectileActor"],
    files: [
      "Network/PvP/NetworkProjectileFireHandler.cs",
      "Network/PvP/NetworkProjectileActor.cs",
      "Network/PvP/PvpProjectileAugmentPayload.cs",
      "Network/PvP/PvpProjectileAugmentEntry.cs",
    ],
    graph: `flowchart TD
      input["InputAuthority Fire"] --> rpc["RPC_RequestFire"]
      rpc --> spawn["StateAuthority Runner.Spawn"]
      spawn --> payload["PvpProjectileAugmentPayload"]
      payload --> registry["ProjectileBehaviorRegistry"]
      registry --> projectile["ProjectileBase"]
      projectile --> actor["NetworkProjectileActor"]
      actor --> feedback["Hit / Collision VFX RPC"]`,
  },
  "aim-prediction": {
    title: "Projectile Aim Prediction",
    short: "조준 예측",
    summary:
      "Aim line과 실제 network projectile의 시작점, collision radius, wall layer 기준을 통일했습니다.",
    intent:
      "PvP에서 조준선은 UX이면서 전투 공정성의 기준입니다. 예측 경로와 실제 projectile 판정이 같은 기준을 공유해야 했습니다.",
    postmortem:
      "처음에는 방향 변환 문제처럼 보였지만, 실제 원인은 spawnForwardOffset, projectile radius, PvpWall layer mask 차이가 겹친 문제였습니다.",
    result:
      "IProjectileAimPredictionProvider로 실제 network projectile origin/radius를 AimLineRenderer가 우선 사용하도록 했습니다.",
    classes: ["AimLineRenderer", "IProjectileAimPredictionProvider", "NetworkProjectileFireHandler", "WeaponBase"],
    files: [
      "Network/PvP/NetworkProjectileFireHandler.cs",
      "GameplayCore/Weapon/Aim/AimLineRenderer.cs",
      "GameplayCore/Weapon/Core/WeaponBase.cs",
    ],
    graph: `flowchart TD
      aim["AimLineRenderer"] --> weapon["Current WeaponBase"]
      weapon --> handler["ProjectileFireHandler"]
      handler --> provider["IProjectileAimPredictionProvider"]
      provider --> origin["Actual Spawn Origin"]
      provider --> radius["Actual Projectile Radius"]
      origin --> cast["SphereCast Prediction"]
      radius --> cast
      cast --> wall["Wall / PvpWall"]`,
  },
  "hit-recovery": {
    title: "PvP Hit Detection Recovery",
    short: "Hit 복구",
    summary:
      "PvP에서 projectile이 상대를 통과하던 문제를 spherecast candidate, hitbox bounds, layer 단계로 나눠 복구했습니다.",
    intent:
      "증상을 damage 로직 문제로 단정하지 않고, collision candidate 생성 이전 단계부터 로그를 넣어 원인을 분리했습니다.",
    postmortem:
      "최종 원인은 Player hitbox localPosition override와 모든 자식 collider를 PvpWeapon으로 바꾸던 layer 보정 부작용이었습니다.",
    result:
      "Player hitbox만 루트 기준으로 정규화하고 해당 collider만 PvpWeapon layer로 설정해 hit 판정을 복구했습니다.",
    classes: ["PvpWeaponActorIdentity", "PvpWeaponHitTarget", "PvpWeaponHealth", "ProjectileBase"],
    files: [
      "Network/PvP/PvpWeaponActorIdentity.cs",
      "Network/PvP/PvpWeaponHitTarget.cs",
      "Network/PvP/PvpWeaponHealth.cs",
      "GameplayCore/Weapon/Projectile/ProjectileBase.cs",
    ],
    graph: `flowchart TD
      symptom["Projectile passes through target"] --> log["SphereCast diagnostics"]
      log --> candidate{"Target candidate?"}
      candidate -->|No| bounds["Compare actor position / collider bounds"]
      bounds --> offset["Hitbox localPosition override"]
      offset --> layer["Layer normalization side effect"]
      layer --> fix["Normalize Player hitbox only"]
      fix --> result["Hit detection recovered"]`,
  },
  "weapon-shield": {
    title: "Network Weapon & Shield Spawn",
    short: "무기 / 방패",
    summary:
      "로비 장착 데이터를 PvP 원격 클라이언트에서도 WeaponId/ShieldId 기반으로 복구하는 구조입니다.",
    intent:
      "원격 클라이언트는 상대의 로컬 item instance를 가질 수 없으므로, 장착 상태를 ID 기반 network state로 전달하고 각 peer가 같은 prefab을 복구해야 했습니다.",
    postmortem:
      "병합 후 WeaponCore_Network에서 활/방패 생성이 다시 깨졌고, shield는 생성되지만 SkillShield 참조 또는 network prefab 구성이 맞지 않았습니다.",
    result:
      "WeaponCore_Network를 actor 중심으로 두고, NetworkWeaponManager와 NetworkShieldSpawnSetup이 ID 기반으로 network prefab을 복구하도록 정리했습니다.",
    classes: ["NetworkWeaponManager", "NetworkShieldSpawnSetup", "NetworkShieldActor", "NetworkShieldColliderDetector"],
    files: [
      "Network/PvP/NetworkWeaponManager.cs",
      "Network/PvP/NetworkShieldSpawnSetup.cs",
      "Network/PvP/NetworkShieldActor.cs",
      "Network/PvP/NetworkShieldColliderDetector.cs",
    ],
    graph: `flowchart TD
      data["PlayerDataManager Equipped Data"] --> ids["WeaponId / ShieldId"]
      ids --> state["Networked State"]
      state --> weapon["Load Network Weapon Prefab"]
      state --> shield["Load Network Shield Prefab"]
      weapon --> actor["WeaponCore_Network"]
      shield --> actor
      actor --> remote["Same state on remote peers"]`,
  },
};

const order = ["overview", "augment", "field", "projectile-sync", "aim-prediction", "hit-recovery", "weapon-shield"];

const els = {
  list: document.querySelector("#system-list"),
  reset: document.querySelector("#reset-view"),
  graph: document.querySelector("#graph"),
  title: document.querySelector("#graph-title"),
  scope: document.querySelector("#scope-label"),
  kind: document.querySelector("#detail-kind"),
  detailTitle: document.querySelector("#detail-title"),
  summary: document.querySelector("#detail-summary"),
  intent: document.querySelector("#detail-intent"),
  postmortem: document.querySelector("#detail-postmortem"),
  result: document.querySelector("#detail-result"),
  classes: document.querySelector("#class-list"),
  tabs: document.querySelector("#code-tabs"),
  code: document.querySelector("#code-preview"),
};

function sourceUrl(path) {
  return `src/Assets/_Project/_Scripts/${path}`;
}

async function loadCode(path) {
  try {
    const response = await fetch(sourceUrl(path));
    if (!response.ok) throw new Error(`${response.status}`);
    const text = await response.text();
    return text.split("\\n").slice(0, 220).join("\\n");
  } catch {
    return `코드 스냅샷을 아직 추가하지 않았거나 브라우저가 로컬 파일 fetch를 막았습니다.\\n\\nExpected: ${sourceUrl(path)}`;
  }
}

function renderList(activeId) {
  els.list.innerHTML = "";
  for (const id of order) {
    const item = systems[id];
    const button = document.createElement("button");
    button.className = `system-button${id === activeId ? " active" : ""}`;
    button.type = "button";
    button.innerHTML = `<strong>${item.short}</strong><span>${item.title}</span>`;
    button.addEventListener("click", () => selectSystem(id));
    els.list.appendChild(button);
  }
}

async function selectSystem(id) {
  const item = systems[id] || systems.overview;
  renderList(id);
  els.title.textContent = item.title;
  els.scope.textContent = id === "overview" ? "Overview" : "System";
  els.kind.textContent = id === "overview" ? "Overview" : "Selected System";
  els.detailTitle.textContent = item.title;
  els.summary.textContent = item.summary;
  els.intent.textContent = item.intent;
  els.postmortem.textContent = item.postmortem;
  els.result.textContent = item.result;
  els.classes.innerHTML = item.classes.map((name) => `<span class="chip">${name}</span>`).join("");

  els.tabs.innerHTML = "";
  els.code.textContent = item.files.length ? "코드 파일을 불러오는 중입니다." : "이 노드는 개요입니다. 왼쪽에서 세부 시스템을 선택하세요.";
  for (const [index, file] of item.files.entries()) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `code-tab${index === 0 ? " active" : ""}`;
    button.textContent = file.split("/").pop();
    button.addEventListener("click", async () => {
      document.querySelectorAll(".code-tab").forEach((tab) => tab.classList.remove("active"));
      button.classList.add("active");
      els.code.textContent = await loadCode(file);
    });
    els.tabs.appendChild(button);
  }
  if (item.files.length) {
    els.code.textContent = await loadCode(item.files[0]);
  }

  els.graph.removeAttribute("data-processed");
  els.graph.textContent = item.graph;
  if (window.mermaid) {
    await window.mermaid.run({ nodes: [els.graph] });
  }
}

window.selectSystem = selectSystem;

document.addEventListener("DOMContentLoaded", async () => {
  if (window.mermaid) {
    window.mermaid.initialize({ startOnLoad: false, securityLevel: "loose", theme: "default" });
  }
  els.reset.addEventListener("click", () => selectSystem("overview"));
  await selectSystem("overview");
});
