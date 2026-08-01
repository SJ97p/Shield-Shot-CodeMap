using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Combat.Application;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;
using Shield_Shot.InputSystemV2.Gestures.Application;
using Shield_Shot.InputSystemV2.Infrastructure;
using UnityEngine;
using Unity.Profiling;

namespace Shield_Shot.InputSystemV2.Integration
{
    [RequireComponent(typeof(UnityPointerInputDriver))]
    public sealed class InputSystemV2RuntimeBehaviour
        : MonoBehaviour
    {
        [Header("Filter")]
        [SerializeField, Min(0f)]
        private float minimumMovementDistance = 5f;

        [Header("Combat Routing")]
        [SerializeField]
        private CombatSplitDirection splitDirection =
            CombatSplitDirection.LeftRight;

        [SerializeField, Range(0.01f, 0.99f)]
        private float splitRatio = 0.5f;

        [SerializeField]
        private bool isInverted;

        [SerializeField]
        private Vector2 referenceResolution =
            new Vector2(1080f, 1920f);

        [Header("Attack Charge")]
        [SerializeField, Range(0f, 1f)]
        private float minimumAimDistanceRatio = 0.05f;

        [SerializeField, Min(0f)]
        private double chargeStartDelay = 0.15d;

        [SerializeField, Min(0.01f)]
        private double fullChargeDuration = 1d;

        [SerializeField, Min(0.001f)]
        private double chargeSignalInterval = 1d / 30d;

        [Header("Gameplay Output")]
        [SerializeField]
        private WeaponAttackInputAdapter weaponAttackInputAdapter;
        [SerializeField]
        private ShieldDefenseInputAdapter shieldDefenseInputAdapter;


        private IPointerFrameSink runtimePipeline;

        private bool externalInputActive;
        public bool IsExternalInputActive =>
        externalInputActive;

        private UnityPointerInputDriver driver;

        private PointerStartBlockFilter viewportBlockFilter;
        private PointerStartBlockFilter uiBlockFilter;
        private PointerMovementThresholdFilter movementFilter;
        private PointerMoveCoalescingSink movementCoalescer;

        private CombatPointerRouter combatRouter;
        private RuntimeCombatInputLayoutProvider runtimeLayoutProvider;

        private PointerGestureTracker attackGestureTracker;
        private PointerGestureTracker defenseGestureTracker;

        private AttackGestureInterpreter attackInterpreter;
        private DefenseGestureInterpreter defenseInterpreter;

        private static readonly ProfilerMarker
    AttackTickMarker =
        new ProfilerMarker(
            "Input.V2.AttackTick");
        public bool IsConfigured
        {
            get;
            private set;
        }

        private void Awake()
        {
            driver =
                GetComponent<UnityPointerInputDriver>();
            driver.enabled = false;
            BuildPipeline();
        }

        private void OnEnable()
        {
            externalInputActive = false;


            if (driver != null)
            {
                driver.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (attackInterpreter == null)
            {
                return;
            }

            AttackTickMarker.Begin();

            try
            {
                attackInterpreter.Tick();
            }
            finally
            {
                AttackTickMarker.End();
            }
        }

        private void OnDisable()
        {
            /*
             * Driver를 먼저 끄면 활성 입력이 Canceled로 전달된다.
             * 그 이후 각 모듈의 내부 상태를 초기화한다.
             */
            if (driver != null &&
                driver.enabled)
            {
                driver.enabled = false;
            }

            ResetRuntime();
            externalInputActive = false;
        }

        [ContextMenu("Reset Input System V2 Runtime")]
        public void ResetRuntime()
        {
            viewportBlockFilter?.Reset();
            uiBlockFilter?.Reset();
            movementFilter?.Reset();
            movementCoalescer?.Reset();

            combatRouter?.Reset();

            attackGestureTracker?.Reset();
            defenseGestureTracker?.Reset();

            attackInterpreter?.Reset();
            weaponAttackInputAdapter?.ResetInput();
            shieldDefenseInputAdapter?.ResetInput();
        }

        private void BuildPipeline()
        {
            IsConfigured = false;

            Rect fallbackViewport =
                new Rect(
                    0f,
                    0f,
                    referenceResolution.x,
                    referenceResolution.y);

            IPointerViewportProvider viewportProvider =
                new ScreenPointerViewportProvider(
                    in fallbackViewport);

            CombatInputLayout initialCombatLayout =
                new CombatInputLayout(
                    splitDirection,
                    splitRatio,
                    isInverted);

            runtimeLayoutProvider =
                new RuntimeCombatInputLayoutProvider(
                    in initialCombatLayout);

            ICombatInputLayoutProvider layoutProvider =
                runtimeLayoutProvider;

            ICombatInputChannelResolver channelResolver =
                new SplitCombatInputChannelResolver(
                    layoutProvider,
                    viewportProvider);

            AttackChargeSettings chargeSettings =
                new AttackChargeSettings(
                    minimumAimDistanceRatio,
                    chargeStartDelay,
                    fullChargeDuration,
                    chargeSignalInterval);

            IAttackChargeSettingsProvider
                chargeSettingsProvider =
                    new FixedAttackChargeSettingsProvider(
                        in chargeSettings);

            IInputClock inputClock =
                new UnityInputClock();

            IAttackInputSink attackOutput =
                weaponAttackInputAdapter;

            if (attackOutput == null)
            {
                Debug.LogError(
                    "WeaponAttackInputAdapter is not assigned.",
                    this);

                enabled = false;
                return;
            }

            IDefenseInputSink defenseOutput =
                shieldDefenseInputAdapter;

            if (defenseOutput == null)
            {
                Debug.LogError(
                    "ShieldDefenseInputAdapter is not assigned.",
                    this);

                enabled = false;
                return;
            }

            attackInterpreter =
                new AttackGestureInterpreter(
                    chargeSettingsProvider,
                    viewportProvider,
                    inputClock,
                    attackOutput);

            attackGestureTracker =
                new PointerGestureTracker(
                    attackInterpreter);

            defenseInterpreter =
                new DefenseGestureInterpreter(
                    defenseOutput);

            defenseGestureTracker =
                new PointerGestureTracker(
                    defenseInterpreter);

            combatRouter =
                new CombatPointerRouter(
                    channelResolver,
                    attackGestureTracker,
                    defenseGestureTracker);

            movementFilter =
                new PointerMovementThresholdFilter(
                    minimumMovementDistance);

            IPointerSampleSink movementFilteredPipeline =
                new FilteredPointerSink(
                    movementFilter,
                    combatRouter);

            movementCoalescer =
                new PointerMoveCoalescingSink(
                    movementFilteredPipeline);

            IPointerStartBlockPolicy uiBlockPolicy =
                new EventSystemPointerStartBlockPolicy();

            uiBlockFilter =
                new PointerStartBlockFilter(
                    uiBlockPolicy);

            IPointerFrameSink uiFilteredPipeline =
                new FilteredPointerFrameSink(
                    uiBlockFilter,
                    movementCoalescer);

            IPointerStartBlockPolicy viewportBlockPolicy =
                new ViewportPointerStartBlockPolicy(
                    viewportProvider);

            viewportBlockFilter =
                new PointerStartBlockFilter(
                    viewportBlockPolicy);

            runtimePipeline =
                new FilteredPointerFrameSink(
                    viewportBlockFilter,
                    uiFilteredPipeline);

            driver.SetPipeline(
                runtimePipeline);

            IsConfigured = true;
        }
        public void BeginExternalInput()
        {
            if (!IsConfigured ||
                runtimePipeline == null)
            {
                throw new System.InvalidOperationException(
                    "Input System V2 Runtime is not configured.");
            }

            /*
             * 실제 Unity 입력 수집만 중단한다.
             * Runtime Behaviour는 활성 상태로 남아
             * AttackGestureInterpreter.Tick()을 계속 수행한다.
             */
            if (driver != null &&
                driver.enabled)
            {
                driver.enabled = false;
            }

            ResetRuntime();
            externalInputActive = true;
        }

        public void SubmitExternalSample(
            in PointerSample sample)
        {
            if (!externalInputActive)
            {
                throw new System.InvalidOperationException(
                    "BeginExternalInput must be called first.");
            }

            runtimePipeline.Receive(
                in sample);
        }

        public void CompleteExternalFrame()
        {
            if (!externalInputActive)
            {
                throw new System.InvalidOperationException(
                    "BeginExternalInput must be called first.");
            }

            runtimePipeline.CompleteFrame();
        }

        public void EndExternalInput()
        {
            if (!externalInputActive)
            {
                return;
            }

            runtimePipeline.CompleteFrame();
            ResetRuntime();

            externalInputActive = false;
        }

        public void ResumeLiveInput()
        {
            EndExternalInput();

            if (driver != null)
            {
                driver.enabled = true;
            }
        }

        public void ApplyCombatLayout(
    in CombatInputLayout layout)
        {
            /*
             * Inspector와 저장 데이터에도 현재 적용값을 남긴다.
             */
            splitDirection = layout.SplitDirection;
            splitRatio = layout.SplitRatio;
            isInverted = layout.IsInverted;

            /*
             * Awake 이전에 호출됐다면 serialized field만 변경한다.
             * 이후 BuildPipeline에서 해당 값으로 Provider가 생성된다.
             */
            runtimeLayoutProvider?.Apply(
                in layout);
        }

        [ContextMenu("Apply Inspector Combat Layout")]
        private void ApplyInspectorCombatLayout()
        {
            var layout =
                new CombatInputLayout(
                    splitDirection,
                    splitRatio,
                    isInverted);

            ApplyCombatLayout(
                in layout);
        }
    }
}