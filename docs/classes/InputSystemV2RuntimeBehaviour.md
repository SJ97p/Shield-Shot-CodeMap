# InputSystemV2RuntimeBehaviour

## 책임

Input System V2 제품 Runtime의 Composition Root다. 필터, Router, Gesture Tracker, Interpreter와 Adapter를 조립하고 생명주기를 관리한다.

## 하지 않는 일

- Unity 입력을 직접 해석하지 않는다.
- 공격·방어 영역 계산 규칙을 직접 소유하지 않는다.
- 활 또는 방패를 직접 회전시키지 않는다.
- 진단 Counter와 Inspector 표시를 소유하지 않는다.

## 설계 이유

객체 조립은 구체 타입을 알아야 하므로 한 지점에 모으되, 각 처리 규칙은 응집된 클래스로 위임했다. Diagnostic을 꺼도 Runtime이 동작하도록 제품 생명주기를 독립시켰다.

