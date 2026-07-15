# Shield & Shot Code Map

Unity 기반 모바일 액션 프로젝트 **Shield & Shot**에서 제가 담당한 게임플레이 시스템 설계, 전투/증강 구조, 속성 필드, PvP 네트워크 전투 안정화 과정을 정리한 포트폴리오용 Code Map입니다.

이 저장소는 전체 Unity 프로젝트를 공개하기보다, 제가 설계 방향을 잡고 직접 통합/수정한 **Projectile Behavior 기반 증강 구조, ElementField 데이터 그리드, PvP 네트워크 투사체/피격/VFX 동기화, 무기/방패 네트워크 스폰, 시행착오 복구 과정**을 중심으로 설명합니다.

> 현재 프로젝트는 개발 진행 중입니다. 문서에서는 플레이 가능 수준까지 통합된 시스템과, 추후 개발 예정 또는 보류된 항목을 구분해 기록합니다.

## Repository Links

- [GitHub Repository](https://github.com/sj97p/Shield-Shot-CodeMap)
- [GitHub Pages](https://sj97p.github.io/Shield-Shot-CodeMap/)
- [Architecture Overview](https://github.com/sj97p/Shield-Shot-CodeMap/blob/main/docs/architecture.md)
- [Class Diagram](https://github.com/sj97p/Shield-Shot-CodeMap/blob/main/docs/class-diagram.md)
- [Source Snapshot](https://github.com/sj97p/Shield-Shot-CodeMap/tree/main/src/Assets/_Project/_Scripts)

## Project Summary

| Item | Description |
|---|---|
| Project | Shield & Shot |
| Engine / Language | Unity / C# |
| Team | 6인 팀 프로젝트 |
| My Role | Game Design, PM, Gameplay System Design, Combat/Network System Design, System Integration |
| Main Focus | Projectile Augment, ElementField, PvP Network Combat, Debugging & Stabilization |
| Repository Goal | 기능 나열보다 설계 의도, 시스템 흐름, 시행착오, 통합 과정을 보여주는 것 |

## My Role

이 프로젝트에서 저는 단일 기능 구현자보다는 **게임플레이 시스템 설계와 통합을 주도하는 역할**을 맡았습니다.

- 게임 기획 및 PM으로 전체 요구사항과 일정 흐름 관리
- 전투/네트워크/몬스터/게임플레이 시스템의 요구사항과 책임 분리 설계
- 투사체 효과를 외부 Behavior 주입으로 확장할 수 있는 구조 요구사항 설계
- 팀원이 구현한 `ProjectileShooter`, `ProjectileBase`, `WeaponManager`, `PlayerStatus` 흐름을 프로젝트 요구에 맞게 수정/통합
- `ElementFieldGrid` 기반 속성 필드와 아레나 좌표계 직접 설계/수정
- Photon Fusion 기반 PvP 전투 구조, 네트워크 무기/방패/투사체 흐름 설계 및 안정화
- PvP hit 판정, VFX, damage popup, aim line 예측 오류 등 핵심 런타임 문제 디버깅
- `SceneController` 기반 씬 context 흐름의 뼈대 설계

일부 하위 구현은 팀원과 분담했지만, 주요 요구사항과 구조 방향은 제가 설계했고, 구현된 구조를 직접 수정/검증하며 실제 플레이 흐름에 맞게 정리했습니다.

## Core Systems

| System | Design Intent | Result |
|---|---|---|
| Projectile Behavior & Augment Injection | 투사체 효과를 조건문 누적이 아니라 외부 Behavior 주입으로 확장 | hit/collision/movement behavior 분리, 우선순위 기반 주입 |
| ElementField Grid | 속성 장판, 지형 반응, 아레나 기준 좌표계를 하나의 데이터 그리드로 통합 | GameObject cell 의존을 줄이고 `ElementFieldCellData[,]` 중심으로 전환 |
| PvP Network Projectile Sync | 로컬 투사체와 다른 네트워크 lifecycle에서도 증강, 피격, VFX가 동작하도록 분리 | payload, registry, network actor, RPC 경로 정리 |
| PvP Hit Detection Recovery | hit가 안 되는 원인을 damage 이후가 아니라 target candidate 이전 단계에서 분리 | hitbox 위치/layer 정규화로 PvP 피격 복구 |
| Aim Prediction Alignment | 조준선과 실제 투사체의 시작점/반경/layer 기준을 통일 | network projectile spawn offset/radius 기반 예측 provider 추가 |
| Network Weapon & Shield Spawn | 로비 장착 데이터를 PvP 원격 클라이언트에서도 ID 기반으로 복구 | WeaponId/ShieldId 기반 network prefab 생성 흐름 정리 |
| Scene Context Flow | 씬마다 다른 진입 데이터를 공통 context 흐름으로 전달 | SceneController 기반 뼈대 설계 |

## Current Status

### Playable / Integrated

- Projectile Behavior 구조
- Augment 선택 흐름
- ElementFieldGrid 데이터 그리드
- Fire / Wind / Ice field
- Terrain / Pond generation
- Random reflect wall
- PvP match flow
- Network weapon / shield spawn
- Network projectile payload
- PvP hit detection
- Network VFX sync
- Damage popup sync
- Seed based arena generation
- SceneController context flow

### Deferred / In Progress

- Active cell network sync
- Wind/Ice network visual full sync
- Profiler 기반 최적화 검증
- PvP 밸런싱 및 장기 플레이 검증

## Technical Postmortems

이 프로젝트에서 가장 의미 있었던 시행착오는 다음 다섯 가지입니다.

1. GameObject cell 방식에서 data grid 방식으로 전환
2. Projectile augment 우선순위와 외부 주입 구조 정리
3. Network projectile에 증강/VFX가 적용되지 않던 문제
4. Aim line과 실제 projectile 경로가 어긋나던 문제
5. PvP hit 판정이 안 되던 hitbox 위치/layer 문제

## Visual Evidence

아래 자료는 `assets/evidence` 폴더에 추가할 예정입니다.

- PvP 2클라이언트 매칭/전투 GIF
- 라운드 종료 -> 증강 선택 -> 다음 라운드 GIF
- 네트워크 투사체 발사 및 피격 GIF
- Aim line과 실제 projectile 경로 정렬 비교 GIF
- 방패 반사 및 hit/wall/reflect VFX 동기화 GIF
- ElementFieldGrid Scene View 캡처
- Fire/Wind/Ice 필드 반응 GIF
- 빌드 2개 실행 캡처

Profiler 기반 최적화 자료는 추후 최적화 개발 단계에서 추가할 예정입니다.

## Key Documents

- [Architecture Overview](docs/architecture.md)
- [Class Diagram](docs/class-diagram.md)
- [Projectile Behavior & Augment Injection](docs/systems/projectile-behavior-augment-injection.md)
- [ElementField Grid](docs/systems/element-field-grid.md)
- [PvP Network Projectile Sync](docs/systems/pvp-network-projectile-sync.md)
- [Projectile Aim Prediction](docs/systems/projectile-aim-prediction.md)
- [PvP Hit Detection Recovery](docs/systems/pvp-hit-detection-recovery.md)
- [Network Weapon & Shield Spawn](docs/systems/network-weapon-shield-spawn.md)
- [PvP Network Recovery Postmortem](docs/systems/pvp-network-recovery-postmortem.md)
- [Current Status & Roadmap](docs/systems/current-status-and-roadmap.md)


## Public Snapshot Policy

이 저장소는 전체 프로젝트 원본이 아니라 포트폴리오용 코드 스냅샷입니다.

- 공개 범위는 제가 설계 방향을 잡고 직접 통합/수정한 핵심 게임플레이 및 PvP 네트워크 시스템으로 제한합니다.
- Unity asset, prefab, ScriptableObject 원본은 포함하지 않고, 필요한 경우 캡처 또는 설명 문서로 대체합니다.
- Backend key, API key, token 등 민감 정보는 공개하지 않으며 필요한 경우 placeholder로 대체합니다.
- 일부 하위 구현은 팀원과 분담했으며, 문서에서는 설계 의도와 통합/안정화 과정을 중심으로 설명합니다.
## Reading Guide

1. 빠르게 역할과 결과를 보고 싶다면 README의 `My Role`, `Core Systems`, `Current Status`를 먼저 확인합니다.
2. 설계 의도와 구조 흐름을 보고 싶다면 `docs/architecture.md`와 `docs/systems`를 확인합니다.
3. 시행착오와 문제 해결 과정을 보고 싶다면 `pvp-hit-detection-recovery`, `projectile-aim-prediction`, `pvp-network-recovery-postmortem` 문서를 확인합니다.
4. 실제 코드 스냅샷은 공개 가능한 담당 파트만 `src/Assets/_Project/_Scripts` 아래에 선별해 추가합니다.


