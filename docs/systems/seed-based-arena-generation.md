# Seed-Based Arena Generation

## Problem

PvP에서는 양쪽 클라이언트가 같은 아레나 지형을 봐야 합니다. 클라이언트마다 random terrain, pond, wall이 다르게 생성되면 전투 판정과 시각 정보가 달라질 수 있습니다.

## Solution

StateAuthority가 seed를 생성하고, 각 peer가 같은 seed로 terrain/grid/wall을 재생성하는 방향으로 설계했습니다.

```text
StateAuthority
-> ArenaSeed 생성
-> Networked state로 공유
-> 각 peer가 같은 seed로 terrain generation
-> 같은 pond/wall/theme 재현
```

## Current Status

Seed based arena generation은 플레이 가능 수준의 기반이 마련되어 있습니다. 다만 active cell network sync는 기획 확정 전까지 보류합니다.

## Portfolio Point

이 구조는 모든 cell 상태를 네트워크로 보내는 대신, seed와 generation rule을 공유해 같은 결과를 재현하려는 접근입니다.
