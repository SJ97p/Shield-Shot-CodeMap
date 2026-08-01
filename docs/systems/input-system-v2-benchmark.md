# V1/V2 Deterministic Input Benchmark

## 측정 목적

코드 구조만 보고 V2가 빠를 것이라고 결론 내리지 않았다. 사람이 직접 드래그하면 경로, 속도, 샘플 수와 프레임이 매번 달라지므로 두 버전의 차이를 설명할 수 없다. 동일한 입력량과 실행 조건을 보장하는 결정론적 벤치마크를 별도로 구축했다.

## 비교 절차

```text
선형 드래그 시나리오 생성
-> V1 워밍업 1회
-> V1 측정 10회
-> V2 워밍업 1회
-> V2 측정 10회
-> 평균 / 중앙값 / P95 / 최댓값 계산
-> 기기 정보와 결과를 JSON 저장
```

### 공통 조건

- 시나리오 길이: 10초
- 입력 빈도: 120 samples/sec
- 버전별 측정: 10회
- 워밍업: 1회
- 목표 프레임: 60 FPS
- VSync: 0
- V1 `ProcessSamples Calls`: 12,010
- V2 `ProcessSamples Calls`: 12,010

두 버전의 Calls가 같지 않으면 Recorder 용량 부족 또는 샘플 손실로 판단하고 결과를 신뢰하지 않았다.

## 측정 코드의 책임

| 클래스 | 책임 |
|---|---|
| `LinearDragBenchmarkSequenceFactory` | 동일한 시작점·종료점·시간·샘플 수를 가진 입력 생성 |
| `BenchmarkPointerSequencePlayer` | 버전에 독립적으로 시나리오 재생 |
| `V1BenchmarkInputAdapter` | 공통 샘플을 V1 `TouchRouter` 입력으로 변환 |
| `V2BenchmarkInputAdapter` | 공통 샘플을 V2 `PointerSample`로 변환 |
| `InputProfilerRecorderSession` | 지정 Marker의 Calls와 시간을 자동 수집 |
| `InputComparisonBatchStatistics` | 평균·중앙값·P95·최댓값 계산 |
| `InputComparisonBuildRunner` | Development Build 자동 실행과 환경 통제 |
| `JsonInputComparisonResultWriter` | 기기·조건·결과를 JSON으로 저장 |

## Marker

| 버전 | Marker | 범위 |
|---|---|---|
| V1 | `Input.V1.ProcessSamples` | 공통 샘플 진입 처리 |
| V1 | `Input.V1.GestureUpdate` | 프레임 기반 제스처 지속 갱신 |
| V2 | `Input.V2.ProcessSamples` | 필터·라우팅·제스처 입력 처리 |
| V2 | `Input.V2.CompleteFrame` | 프레임 내 이동 병합 결과 확정 |
| V2 | `Input.V2.AttackTick` | 시간 기반 차징 상태 갱신 |

`Calls`는 Draw Call이 아니다. 또한 `Marker Total`은 게임 전체 CPU 시간이 아니라 위 입력 Marker들의 합이다.

## 결과 요약

원본 측정 자료는 개인 PC 이름만 일반화해 함께 공개했습니다.

- [Windows Development Build JSON](../data/input-benchmark/windows-development-build.json)
- [Galaxy S23+ JSON](../data/input-benchmark/galaxy-s23-plus.json)

| 환경 | V1 평균 | V2 평균 | 감소율 | 상대 비용 |
|---|---:|---:|---:|---:|
| Editor 반복 측정 | 478.4928 ms | 21.4800 ms | 95.51% | 약 22.3배 낮음 |
| Windows Development Build | 100.1458 ms | 23.6776 ms | 76.36% | 약 4.23배 낮음 |
| Galaxy S23+ Development Build | 360.2023 ms | 123.0634 ms | 65.83% | 약 2.93배 낮음 |

Editor 결과는 탐색과 회귀 확인용으로 사용하고, 포트폴리오 대표 수치는 Development Build와 실제 기기 결과로 제한했다.

## Windows Development Build

| 통계 | V1 | V2 |
|---|---:|---:|
| 평균 | 100.1458 ms | 23.6776 ms |
| 중앙값 | 100.2809 ms | 23.6906 ms |
| P95 | 102.2201 ms | 24.4077 ms |
| 최댓값 | 102.3660 ms | 24.7680 ms |

- 평균 Marker 비용 76.36% 감소
- V1 `GestureUpdate`: 95.5710 ms
- V2 `CompleteFrame + AttackTick`: 17.4463 ms
- 지속 처리 구간 약 81.7% 감소
- 약 0.127 ms/frame 확보

## Galaxy S23+ 실제 기기

| 통계 | V1 | V2 |
|---|---:|---:|
| 평균 | 360.2023 ms | 123.0634 ms |
| 중앙값 | 361.2721 ms | 122.8111 ms |
| P95 | 364.5108 ms | 125.6213 ms |
| 최댓값 | 364.8852 ms | 126.8343 ms |

- 평균 Marker 비용 65.83% 감소
- 10초당 약 237.14 ms 절약
- 60 FPS 기준 약 0.395 ms/frame 확보
- 16.67 ms 프레임 예산의 약 2.37%
- 지속 처리 구간 약 73.5% 감소

## 불리한 수치도 함께 본 이유

Galaxy S23+에서 V2 `ProcessSamples`는 34.4691 ms, V1은 25.7417 ms로 V2의 샘플 진입 비용이 약 33.9% 높았다. V2가 필터, 영역 판정과 상태 병합을 선행하기 때문이다.

하지만 V1의 지속 갱신은 334.4606 ms, V2의 `CompleteFrame + AttackTick`은 88.5943 ms였다. 진입 지점에서 조금 더 계산하고 하위 계층의 반복 실행을 약 245.87 ms 줄여 전체 비용을 낮춘 것이다.

이 결과는 V2의 목적을 명확히 보여준다. 입력 한 건의 최소 비용을 추구한 것이 아니라, 의미 없는 입력이 UI·VFX·네트워크 같은 비싼 소비자까지 전파되지 않도록 경계에서 정리했다.

## 결과 해석의 한계

- Marker Total 감소율을 게임 전체 CPU 감소율로 해석하지 않는다.
- Galaxy S23+ 한 기기의 결과를 모든 Android 기기로 일반화하지 않는다.
- 네트워크 트래픽 감소는 구조적 가능성이며, Network Tick 기반 전송 정책을 추가한 뒤 별도 측정해야 한다.
- GC Alloc, 전체 Frame Time P99와 저사양 기기 검증은 후속 측정 항목이다.
