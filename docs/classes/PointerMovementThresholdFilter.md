# PointerMovementThresholdFilter

## 책임

마지막으로 허용된 위치와 비교해 최소 이동 거리보다 작은 Move 샘플을 차단한다. Began, Ended와 Canceled의 의미는 보존한다.

## 설계 이유

손가락 미세 떨림과 고빈도 포인터 이벤트가 Gesture·무기·UI·네트워크까지 전달되는 것을 입력 경계에서 막는다. 필터는 게임플레이 의미를 모르며 거리 정책 하나에만 응집되어 있다.

