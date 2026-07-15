# Scene Context Flow

## Problem

로비, 인게임, PvP, 로딩 씬은 각기 다른 진입 데이터를 필요로 합니다. UI 자체는 팀원과 분담했지만, 씬마다 진입/종료 규칙이 달라지면 로비에서 전투, 전투에서 결과, PvP 종료 후 로비 복귀 같은 흐름이 쉽게 꼬일 수 있습니다.

## Solution

씬 전환의 뼈대는 `SceneController` / `BaseSceneController` 계열이 context를 받아 초기화하는 방향으로 설계했습니다.

```text
SceneFlowManager
-> SceneTransitionData
-> Loading Scene
-> Target Scene Controller
-> Initialize(context)
-> Enter
```

## Portfolio Point

이 구조는 UI 화면 하나의 구현보다, 여러 씬이 서로 다른 데이터를 받으면서도 동일한 lifecycle 규칙을 따르도록 만든 기반 작업입니다.
