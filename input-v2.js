Object.assign(nodes, {
  inputV2: system({
    title: "Input System V2 Refactoring",
    summary: "프레임 중심으로 반복 전파되던 V1 입력을 Source, Filter, Router, Gesture, Interpreter, Adapter 책임으로 분리하고 실제 기기에서 검증했습니다.",
    intent: "활 조작의 굼뜬 반응과 드래그 중 과도한 갱신을 단순 Update 최적화 문제가 아니라 입력 빈도가 UI·VFX·네트워크까지 전파될 수 있는 구조 문제로 보았습니다. 기존 V1은 기준선으로 보존하고 V2를 병렬 구현해 기능과 수치를 함께 비교했습니다.",
    issue: "V1은 InputProvider → TouchRouter → GestureAnalyzer → PlayerInputReceiver → Weapon 흐름으로 나뉘어 있었지만 수집, 제스처 갱신과 게임플레이 전달 주기가 프레임 흐름에 묶여 있었습니다. 작은 이동과 유지 상태도 GestureUpdate를 반복 실행했고, 특정 정책을 바꾸면 여러 계층을 함께 확인해야 했습니다.",
    final: "Unity 의존성은 Source/Policy에, 잡음 제거와 프레임 병합은 Filter에, 영역 판정은 Resolver/Router에, 상태 해석은 Gesture/Interpreter에, 기존 무기 연결은 Adapter에 배치했습니다. Windows Development Build에서 76.36%, Galaxy S23+에서 65.83%의 입력 Marker 비용 감소를 확인했습니다.",
    next: "회전 감도, 빠른 드래그 시 차징 해제, Network Tick 기반 전송 제한을 기존 파이프라인을 수정하지 않고 별도 정책으로 확장할 예정입니다.",
    classes: ["UnityPointerInputDriver", "PointerMovementThresholdFilter", "PointerMoveCoalescingSink", "CombatPointerRouter", "PointerGestureTracker", "AttackGestureInterpreter", "DefenseGestureInterpreter", "WeaponAttackInputAdapter", "ShieldDefenseInputAdapter", "InputSystemV2RuntimeBehaviour"],
    evidence: [],
    code: [
      code("InputSystemV2/Integration/InputSystemV2RuntimeBehaviour.cs", "BuildPipeline", ["Awake", "BuildPipeline", "ApplyCombatLayout", "ResetRuntime"]),
      code("InputSystemV2/Application/CombatPointerRouter.cs", "Receive", ["Receive", "CompleteFrame", "Reset"]),
    ],
    graph: `flowchart LR
      source["Unity Pointer Source"] --> filters["Start / Threshold Filters"]
      filters --> coalescer["Frame Move Coalescer"]
      coalescer --> router["Combat Pointer Router"]
      router --> attack["Attack Gesture / Interpreter"]
      router --> defense["Defense Gesture / Interpreter"]
      attack --> weapon["Weapon Adapter"]
      defense --> shield["Shield Adapter"]`,
  }),

  inputV1VsV2: system({
    title: "V1 to V2 Design Decision",
    summary: "기존 코드를 덮어쓰지 않고 V1을 기준선으로 남긴 채 V2를 별도 구현한 판단과 책임 재배치 과정입니다.",
    intent: "리팩터링 전 동작과 수치를 잃지 않고 기능 회귀, 입력량과 처리 비용을 직접 비교하려 했습니다. 최적화라는 결론을 코드 인상에 의존하지 않고 동일 입력으로 증명하는 것이 목표였습니다.",
    issue: "V1의 클래스가 하나로 합쳐진 것은 아니었지만 서로 다른 변경 이유가 프레임 이벤트 체인에 묶여 있었습니다. 입력 API, UI 차단, 영역 설정, 제스처 상태, 차징과 무기 호출이 독립적으로 교체하거나 시험하기 어려웠습니다.",
    final: "V1은 TouchRouter 앞 경계에 Benchmark Adapter를 두고, V2는 PointerSample 경계에 Adapter를 두었습니다. 같은 12,010개 샘플을 양쪽에 주입해 구조 차이만 비교했고, V2는 입력 진입 비용 일부를 사용해 비싼 지속 갱신의 전파를 크게 줄였습니다.",
    next: "향후 네트워크 비교에서도 로컬 입력 빈도와 송신 Tick을 분리하고 동일 명령 시나리오로 트래픽과 원격 보간 품질을 함께 측정할 예정입니다.",
    classes: ["LegacyInputProvider", "LegacyGestureAnalyzer", "V1BenchmarkInputAdapter", "V2BenchmarkInputAdapter", "InputComparisonBuildRunner"],
    evidence: [],
    code: [
      code("InputSystem/InputProvider.cs", "Update", ["Update", "SimulateMouseAsTouch", "IsUIBlocked"]),
      code("InputSystem/GestureAnalyzer.cs", "Update", ["Update", "ReceiveRawTouch", "OnTouchMoved"]),
      code("Performance/InputComparison/V1BenchmarkInputAdapter.cs", "Process", ["Process"]),
      code("Performance/InputComparison/V2BenchmarkInputAdapter.cs", "Process", ["Process"]),
    ],
    graph: `flowchart TD
      v1["V1: Frame-driven propagation"] --> repeated["Repeated GestureUpdate"]
      repeated --> consumers["Weapon / UI / VFX / Network"]
      decision["Keep V1 as baseline"] --> v2["Build V2 in parallel"]
      v2 --> filtered["Filter + Coalesce + State Change"]
      filtered --> consumers
      v1 --> benchmark["Same deterministic samples"]
      v2 --> benchmark`,
  }),

  inputBenchmark: system({
    title: "Deterministic Input Benchmark",
    summary: "V1/V2에 동일한 10초 선형 드래그를 주입하고 워밍업과 10회 반복 측정으로 평균, 중앙값, P95와 최댓값을 수집했습니다.",
    intent: "사람의 드래그는 속도와 샘플 수가 매번 달라지므로 성능 개선의 근거가 될 수 없습니다. 동일 입력량과 동일 실행 조건을 자동으로 재현하고 Development Build와 실제 Android 기기에서 검증했습니다.",
    issue: "초기 측정에서는 Recorder 순환 버퍼 용량과 무제한 FPS 때문에 오래된 샘플이 유실되어 개선율이 과장될 수 있었습니다. Editor 단일 실행은 초기화와 프레임 변동의 영향도 컸습니다.",
    final: "60 FPS, VSync 0, 워밍업 1회, 버전별 10회, ProcessSamples Calls 12,010을 검증 조건으로 고정했습니다. Windows에서 76.36%, Galaxy S23+에서 65.83% 감소했으며 모바일에서 약 0.395 ms/frame을 확보했습니다.",
    next: "GC Alloc, 전체 Frame Time P99, 저사양 Android와 Network Tick 전송량을 같은 JSON 결과 체계에 추가할 예정입니다.",
    classes: ["BenchmarkPointerSequencePlayer", "V1BenchmarkInputAdapter", "V2BenchmarkInputAdapter", "InputProfilerRecorderSession", "InputComparisonBatchStatistics", "InputComparisonBuildRunner"],
    evidence: [],
    code: [
      code("Performance/InputComparison/LinearDragBenchmarkSequenceFactory.cs", "Create", ["Create"]),
      code("Performance/InputComparison/InputProfilerRecorderSession.cs", "Start", ["Start", "Stop"]),
      code("Performance/InputComparison/InputComparisonBuildRunner.cs", "Start", ["Start", "RunBenchmark"]),
    ],
    graph: `flowchart LR
      scenario["10 sec / 120 samples per sec"] --> v1warm["V1 Warmup"]
      v1warm --> v1runs["V1 x 10"]
      v1runs --> v2warm["V2 Warmup"]
      v2warm --> v2runs["V2 x 10"]
      v2runs --> stats["Avg / Median / P95 / Max"]
      stats --> json["Device + Result JSON"]`,
  }),

  UnityPointerInputDriver: cls({
    title: "UnityPointerInputDriver",
    summary: "Unity 입력 Source와 V2 파이프라인 생명주기를 연결하고 프레임 완료를 전달합니다.",
    intent: "Unity 입력 수집 주기와 도메인 입력 해석을 분리합니다.",
    issue: "하위 계층이 EnhancedTouch와 Mouse를 직접 알면 합성 입력 교체와 독립 테스트가 어렵습니다.",
    final: "Source가 PointerSample을 만들고 Driver는 Sink 전달과 CompleteFrame 호출만 담당합니다.",
    next: "새 입력 장치는 IPointerSource 구현으로 추가할 수 있습니다.",
    classes: ["PointerMovementThresholdFilter", "InputSystemV2RuntimeBehaviour"], evidence: [],
    code: [code("InputSystemV2/Infrastructure/UnityPointerInputDriver.cs", "Configure", ["Configure", "OnEnable", "OnDisable", "Update"])],
    graph: classGraph("UnityPointerInputDriver", ["-IPointerSource source", "-IPointerFrameSink sink", "+Configure()", "+Update()"], ["PointerMovementThresholdFilter", "InputSystemV2RuntimeBehaviour"]),
  }),

  PointerMovementThresholdFilter: cls({
    title: "PointerMovementThresholdFilter",
    summary: "최소 거리 미만의 포인터 이동 잡음을 입력 경계에서 제거합니다.",
    intent: "미세 떨림이 Gesture, 무기와 후속 시스템으로 전파되지 않게 합니다.",
    issue: "매 Move를 전달하면 실제 의미 변화 없이 하위 호출량이 증가합니다.",
    final: "마지막 허용 위치와 비교해 Move만 필터링하고 Began/Ended/Canceled 의미는 보존합니다.",
    next: "기기 DPI 기반 임계값 정책으로 확장할 수 있습니다.",
    classes: ["PointerMoveCoalescingSink", "CombatPointerRouter"], evidence: [],
    code: [code("InputSystemV2/Application/PointerMovementThresholdFilter.cs", "ShouldPass", ["ShouldPass", "Reset"])],
    graph: classGraph("PointerMovementThresholdFilter", ["-float minimumDistance", "+ShouldPass()", "+Reset()"], ["PointerMoveCoalescingSink", "CombatPointerRouter"]),
  }),

  PointerMoveCoalescingSink: cls({
    title: "PointerMoveCoalescingSink",
    summary: "같은 프레임에 들어온 여러 Move를 포인터별 마지막 값으로 병합합니다.",
    intent: "터치 샘플 빈도와 게임 로직 처리 빈도를 분리합니다.",
    issue: "고주사율 입력 장치의 모든 중간 좌표를 게임플레이가 처리할 필요는 없습니다.",
    final: "Began/Ended는 즉시 전달하고 Move는 CompleteFrame 시 한 번 전달합니다.",
    next: "네트워크 Snapshot 병합 정책에도 같은 개념을 적용할 수 있습니다.",
    classes: ["CombatPointerRouter"], evidence: [],
    code: [code("InputSystemV2/Application/PointerMoveCoalescingSink.cs", "CompleteFrame", ["Receive", "CompleteFrame", "Reset"])],
    graph: classGraph("PointerMoveCoalescingSink", ["-Dictionary pendingMoves", "+Receive()", "+CompleteFrame()"], ["CombatPointerRouter"]),
  }),

  CombatPointerRouter: cls({
    title: "CombatPointerRouter",
    summary: "포인터를 현재 레이아웃에 따라 공격 또는 방어 Gesture 경로로 전달합니다.",
    intent: "영역 정책과 Gesture 상태 추적을 분리합니다.",
    issue: "Router가 좌우·상하 계산까지 소유하면 설정 추가 때 전달 코드도 바뀝니다.",
    final: "ICombatInputChannelResolver에 판정을 위임하고 활성 포인터의 채널만 추적합니다.",
    next: "추가 액션 영역은 Resolver 정책과 채널 확장으로 대응할 수 있습니다.",
    classes: ["PointerGestureTracker", "AttackGestureInterpreter", "DefenseGestureInterpreter"], evidence: [],
    code: [code("InputSystemV2/Application/CombatPointerRouter.cs", "Receive", ["Receive", "CompleteFrame", "Reset"])],
    graph: classGraph("CombatPointerRouter", ["-ICombatInputChannelResolver resolver", "+Receive()", "+Reset()"], ["PointerGestureTracker", "AttackGestureInterpreter", "DefenseGestureInterpreter"]),
  }),

  PointerGestureTracker: cls({
    title: "PointerGestureTracker",
    summary: "포인터 생명주기를 Began, Changed, Completed, Canceled Gesture로 변환합니다.",
    intent: "공격과 방어 의미를 모르는 재사용 가능한 상태 추적기를 만듭니다.",
    issue: "포인터 상태와 차징/방패 의미를 함께 처리하면 기능 규칙이 얽힙니다.",
    final: "공통 Gesture 상태만 만들고 공격·방어 해석은 각 Interpreter에 위임합니다.",
    next: "멀티 포인터 Gesture가 필요하면 별도 조합 Tracker를 둘 수 있습니다.",
    classes: ["AttackGestureInterpreter", "DefenseGestureInterpreter"], evidence: [],
    code: [code("InputSystemV2/Application/PointerGestureTracker.cs", "Receive", ["Receive", "Reset"])],
    graph: classGraph("PointerGestureTracker", ["-Dictionary activePointers", "+Receive()", "+Reset()"], ["AttackGestureInterpreter", "DefenseGestureInterpreter"]),
  }),

  AttackGestureInterpreter: cls({
    title: "AttackGestureInterpreter",
    summary: "공격 Gesture를 조준, 차징, 해제와 취소 명령으로 해석합니다.",
    intent: "시간 기반 차징 규칙을 WeaponBase에서 분리합니다.",
    issue: "무기가 포인터 시간과 임계값까지 알면 입력 장치와 게임플레이가 결합됩니다.",
    final: "IInputClock과 AttackChargeSettings를 사용해 AttackInputSample만 출력합니다.",
    next: "빠른 드래그 시 차징 해제 규칙을 별도 정책으로 추가할 수 있습니다.",
    classes: ["WeaponAttackInputAdapter"], evidence: [],
    code: [code("InputSystemV2/Application/AttackGestureInterpreter.cs", "Receive", ["Receive", "Tick", "Reset"])],
    graph: classGraph("AttackGestureInterpreter", ["-AttackChargeSettings settings", "+Receive()", "+Tick()"], ["WeaponAttackInputAdapter"]),
  }),

  DefenseGestureInterpreter: cls({
    title: "DefenseGestureInterpreter",
    summary: "방어 Gesture를 시작, 방향 변경, 해제와 취소 명령으로 해석합니다.",
    intent: "방패 회전 구현과 포인터 상태 변환을 분리합니다.",
    issue: "Shield가 원시 Touch를 직접 받으면 테스트와 감도 정책 추가가 어렵습니다.",
    final: "DefenseInputSample만 출력하고 실제 궤도 회전은 Adapter에 맡깁니다.",
    next: "방패 감도와 방향 급변 필터를 독립 정책으로 추가할 수 있습니다.",
    classes: ["ShieldDefenseInputAdapter"], evidence: [],
    code: [code("InputSystemV2/Application/DefenseGestureInterpreter.cs", "Receive", ["Receive", "Reset"])],
    graph: classGraph("DefenseGestureInterpreter", ["+Receive()", "+Reset()"], ["ShieldDefenseInputAdapter"]),
  }),

  WeaponAttackInputAdapter: cls({
    title: "WeaponAttackInputAdapter",
    summary: "V2 공격 출력을 기존 WeaponBase API로 번역합니다.",
    intent: "입력 도메인이 구체적인 무기 구현에 의존하지 않게 합니다.",
    issue: "Interpreter가 WeaponManager를 직접 알면 무기 변경이 입력 핵심 로직에 영향을 줍니다.",
    final: "IAttackInputSink 경계에서 현재 무기를 캡처하고 기존 입력 Context로 변환합니다.",
    next: "총기 등 다른 출력은 별도 Adapter로 같은 공격 계약을 구현할 수 있습니다.",
    classes: ["InputSystemV2RuntimeBehaviour"], evidence: [],
    code: [code("InputSystemV2/Integration/WeaponAttackInputAdapter.cs", "Receive", ["Receive", "Begin", "ApplyState", "Release", "Cancel"])],
    graph: classGraph("WeaponAttackInputAdapter", ["-WeaponManager weaponManager", "+Receive()", "+ResetInput()"], ["InputSystemV2RuntimeBehaviour"]),
  }),

  ShieldDefenseInputAdapter: cls({
    title: "ShieldDefenseInputAdapter",
    summary: "V2 방어 출력을 현재 방패의 궤도 회전 API로 번역합니다.",
    intent: "방패 장착 책임과 입력 해석 책임을 유지한 채 두 시스템을 연결합니다.",
    issue: "V2가 방패 생성 과정을 알면 WeaponManager와 강하게 결합됩니다.",
    final: "CurrentShield 계약으로 OrbitController만 조회하고 DefenseInputSample을 변위 호출로 변환합니다.",
    next: "감도 설정은 Adapter 입력 경계 또는 궤도 회전 정책으로 확장할 수 있습니다.",
    classes: ["InputSystemV2RuntimeBehaviour"], evidence: [],
    code: [code("InputSystemV2/Integration/ShieldDefenseInputAdapter.cs", "Receive", ["Receive", "Begin", "ApplyDirection", "End"])],
    graph: classGraph("ShieldDefenseInputAdapter", ["-WeaponManager weaponManager", "+Receive()", "+ResetInput()"], ["InputSystemV2RuntimeBehaviour"]),
  }),

  InputSystemV2RuntimeBehaviour: cls({
    title: "InputSystemV2RuntimeBehaviour",
    summary: "V2 제품 파이프라인을 조립하고 생명주기를 관리하는 Composition Root입니다.",
    intent: "구체 객체 생성은 한곳에 모으되 처리 규칙은 응집된 객체에 위임합니다.",
    issue: "초기에는 Live Diagnostic이 Runtime을 소유해 진단을 끄면 무기도 멈췄습니다.",
    final: "제품 Runtime을 독립시키고 Diagnostic과 Benchmark는 관찰 또는 외부 주입만 수행합니다.",
    next: "씬 참조를 자동 바인딩하는 Installer를 추가하면 제품 프리팹 배치가 단순해집니다.",
    classes: ["UnityPointerInputDriver", "WeaponAttackInputAdapter", "ShieldDefenseInputAdapter"], evidence: [],
    code: [code("InputSystemV2/Integration/InputSystemV2RuntimeBehaviour.cs", "BuildPipeline", ["Awake", "BuildPipeline", "ApplyCombatLayout", "ResetRuntime"])],
    graph: classGraph("InputSystemV2RuntimeBehaviour", ["-IPointerFrameSink runtimePipeline", "+ApplyCombatLayout()", "+ResetRuntime()", "-BuildPipeline()"], ["UnityPointerInputDriver", "WeaponAttackInputAdapter", "ShieldDefenseInputAdapter"]),
  }),

  LegacyInputProvider: cls({
    title: "V1 InputProvider",
    summary: "Enhanced Touch와 Mouse를 직접 읽어 기존 TouchRouter로 전달하는 V1 입력 시작점입니다.",
    intent: "개선 전 기준선을 숨기지 않고 V2와 직접 비교합니다.",
    issue: "수집 주기와 게임 로직 갱신 주기가 프레임 이벤트 체인에 연결됩니다.",
    final: "제품 V1은 보존하고 Benchmark Adapter를 별도 추가해 동일 입력을 주입했습니다.",
    next: "V2 전환 검증이 끝나면 제품 씬에서는 비활성화합니다.",
    classes: ["LegacyGestureAnalyzer", "V1BenchmarkInputAdapter"], evidence: [],
    code: [code("InputSystem/InputProvider.cs", "Update", ["Update", "SimulateMouseAsTouch", "IsUIBlocked"])],
    graph: classGraph("LegacyInputProvider", ["+Update()", "-ProcessTouches()", "-ProcessMouse()"], ["LegacyGestureAnalyzer", "V1BenchmarkInputAdapter"]),
  }),

  LegacyGestureAnalyzer: cls({
    title: "V1 GestureAnalyzer",
    summary: "V1 포인터 상태와 매 프레임 지속 입력 이벤트를 생성합니다.",
    intent: "V2에서 줄이려 한 반복 처리 구간의 기준선입니다.",
    issue: "활성 Gesture가 유지되면 작은 상태 변화에도 GestureUpdate 비용이 누적됩니다.",
    final: "V2에서는 공통 Tracker와 공격/방어 Interpreter로 책임을 분리했습니다.",
    next: "V1은 성능 비교와 안전한 롤백 기준으로 유지합니다.",
    classes: ["AttackGestureInterpreter", "DefenseGestureInterpreter"], evidence: [],
    code: [code("InputSystem/GestureAnalyzer.cs", "Update", ["Update", "ReceiveRawTouch", "OnTouchMoved"])],
    graph: classGraph("LegacyGestureAnalyzer", ["+UpdateGesture()", "+OnInputStay"], ["AttackGestureInterpreter", "DefenseGestureInterpreter"]),
  }),

  V1BenchmarkInputAdapter: cls({
    title: "V1BenchmarkInputAdapter", summary: "공통 Benchmark Sample을 V1 입력 경계로 변환합니다.",
    intent: "기존 V1 제품 코드를 수정하지 않고 동일 입력을 주입합니다.", issue: "EnhancedTouch 객체를 억지로 생성하면 비교 도구가 Unity 구현 세부사항에 묶입니다.",
    final: "V1이 요구하는 최소 입력 데이터로 변환해 TouchRouter 경로에 전달합니다.", next: "V1 제거 전까지 회귀 기준으로 유지합니다.",
    classes: ["V2BenchmarkInputAdapter", "InputComparisonBuildRunner"], evidence: [],
    code: [code("Performance/InputComparison/V1BenchmarkInputAdapter.cs", "Process", ["Process"])],
    graph: classGraph("V1BenchmarkInputAdapter", ["+Process()"], ["V2BenchmarkInputAdapter", "InputComparisonBuildRunner"]),
  }),

  V2BenchmarkInputAdapter: cls({
    title: "V2BenchmarkInputAdapter", summary: "공통 Benchmark Sample을 V2 PointerSample 경계로 변환합니다.",
    intent: "실제 Source를 교체해도 파이프라인 의미가 유지되는지 검증합니다.", issue: "Live 입력과 합성 입력이 동시에 들어오면 결과가 오염됩니다.",
    final: "측정 중 외부 입력 모드로 전환하고 종료 후 Live 입력 상태를 복구합니다.", next: "Recorded device trace 재생으로 확장할 수 있습니다.",
    classes: ["V1BenchmarkInputAdapter", "InputComparisonBuildRunner"], evidence: [],
    code: [code("Performance/InputComparison/V2BenchmarkInputAdapter.cs", "Process", ["Process"])],
    graph: classGraph("V2BenchmarkInputAdapter", ["+Process()"], ["V1BenchmarkInputAdapter", "InputComparisonBuildRunner"]),
  }),

  BenchmarkPointerSequencePlayer: cls({
    title: "BenchmarkPointerSequencePlayer", summary: "버전에 독립적으로 결정론적 입력 시퀀스를 재생합니다.",
    intent: "사람의 입력 편차를 제거합니다.", issue: "프레임 시간에만 의존하면 실행마다 샘플 수가 달라질 수 있습니다.",
    final: "정해진 Timestamp와 Sample 순서를 Target 계약으로 전달합니다.", next: "복수 포인터 시나리오를 추가할 수 있습니다.",
    classes: ["V1BenchmarkInputAdapter", "V2BenchmarkInputAdapter"], evidence: [],
    code: [code("Performance/InputComparison/BenchmarkPointerSequencePlayer.cs", "Play", ["Play"])],
    graph: classGraph("BenchmarkPointerSequencePlayer", ["+Play()"], ["V1BenchmarkInputAdapter", "V2BenchmarkInputAdapter"]),
  }),

  InputProfilerRecorderSession: cls({
    title: "InputProfilerRecorderSession", summary: "입력 Marker의 Calls와 실행 시간을 자동 수집합니다.",
    intent: "Profiler 창 수동 판독 대신 반복 가능한 수치를 만듭니다.", issue: "Recorder 순환 버퍼가 작으면 오래된 샘플 유실로 개선율이 왜곡됩니다.",
    final: "시나리오 조건에 맞는 Capacity와 종료 프레임을 보장하고 Metric Summary를 생성합니다.", next: "GC Alloc과 P99 수집을 추가할 예정입니다.",
    classes: ["InputComparisonBatchStatistics", "InputComparisonBuildRunner"], evidence: [],
    code: [code("Performance/InputComparison/InputProfilerRecorderSession.cs", "Start", ["Start", "Stop"])],
    graph: classGraph("InputProfilerRecorderSession", ["+Start()", "+Stop()"], ["InputComparisonBatchStatistics", "InputComparisonBuildRunner"]),
  }),

  InputComparisonBatchStatistics: cls({
    title: "InputComparisonBatchStatistics", summary: "반복 실행 결과의 평균, 중앙값, P95와 최댓값을 계산합니다.",
    intent: "평균만으로 숨겨지는 스파이크와 반복 안정성을 함께 봅니다.", issue: "10회 P95를 단순 인덱스로 고르면 최댓값과 동일해질 수 있습니다.",
    final: "정렬된 Run 결과에서 선형 보간으로 P95를 계산합니다.", next: "반복 수 증가 시 신뢰구간을 추가할 수 있습니다.",
    classes: ["InputProfilerRecorderSession", "InputComparisonBuildRunner"], evidence: [],
    code: [code("Performance/InputComparison/InputComparisonBatchStatistics.cs", "Create", ["Create"])],
    graph: classGraph("InputComparisonBatchStatistics", ["+Average", "+Median", "+P95", "+Max"], ["InputProfilerRecorderSession", "InputComparisonBuildRunner"]),
  }),

  InputComparisonBuildRunner: cls({
    title: "InputComparisonBuildRunner", summary: "Development Build에서 조건 고정, 자동 실행과 JSON 저장을 담당합니다.",
    intent: "Editor 결과를 최종 성능 근거로 사용하지 않습니다.", issue: "무제한 FPS와 VSync 차이는 Recorder Capacity와 프레임 호출량을 바꿉니다.",
    final: "60 FPS, VSync 0을 적용하고 측정 후 원래 설정을 복원하며 기기 정보와 결과를 저장합니다.", next: "CI 기기 팜과 기준치 회귀 실패 판정으로 확장할 수 있습니다.",
    classes: ["InputProfilerRecorderSession", "InputComparisonBatchStatistics"], evidence: [],
    code: [code("Performance/InputComparison/InputComparisonBuildRunner.cs", "Start", ["Start", "RunBenchmark"])],
    graph: classGraph("InputComparisonBuildRunner", ["+Start()", "-RunBenchmark()", "-WriteResult()"], ["InputProfilerRecorderSession", "InputComparisonBatchStatistics"]),
  }),
});

treeGroups.push(
  { title: "Input System V2", ids: ["inputV2", "inputV1VsV2", "inputBenchmark"] },
  { title: "Input Pipeline", ids: ["UnityPointerInputDriver", "PointerMovementThresholdFilter", "PointerMoveCoalescingSink", "CombatPointerRouter", "PointerGestureTracker", "AttackGestureInterpreter", "DefenseGestureInterpreter", "WeaponAttackInputAdapter", "ShieldDefenseInputAdapter", "InputSystemV2RuntimeBehaviour"] },
  { title: "Input Benchmark", ids: ["LegacyInputProvider", "LegacyGestureAnalyzer", "BenchmarkPointerSequencePlayer", "V1BenchmarkInputAdapter", "V2BenchmarkInputAdapter", "InputProfilerRecorderSession", "InputComparisonBatchStatistics", "InputComparisonBuildRunner"] },
);

renderTree();
