using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;
using Shield_Shot.InputSystemV2.Gestures.Application;
using Shield_Shot.InputSystemV2.Gestures.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class AttackGestureInterpreter
        : IPointerGestureSink
    {
        private readonly IAttackChargeSettingsProvider
            settingsProvider;

        private readonly IPointerViewportProvider
            viewportProvider;

        private readonly IInputClock clock;
        private readonly IAttackInputSink next;

        private readonly Dictionary<PointerKey, ActiveAttack>
            activeAttacks;

        public AttackGestureInterpreter(
            IAttackChargeSettingsProvider settingsProvider,
            IPointerViewportProvider viewportProvider,
            IInputClock clock,
            IAttackInputSink next,
            int initialPointerCapacity = 2)
        {
            this.settingsProvider = settingsProvider
                ?? throw new ArgumentNullException(
                    nameof(settingsProvider));

            this.viewportProvider = viewportProvider
                ?? throw new ArgumentNullException(
                    nameof(viewportProvider));

            this.clock = clock
                ?? throw new ArgumentNullException(
                    nameof(clock));

            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));

            if (initialPointerCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialPointerCapacity));
            }

            activeAttacks =
                new Dictionary<PointerKey, ActiveAttack>(
                    initialPointerCapacity);
        }

        public void Receive(
            in PointerGestureSample gesture)
        {
            switch (gesture.Phase)
            {
                case PointerGesturePhase.Began:
                    Begin(in gesture);
                    break;

                case PointerGesturePhase.Changed:
                    Change(in gesture);
                    break;

                case PointerGesturePhase.Completed:
                    End(
                        in gesture,
                        AttackInputPhase.Released);
                    break;

                case PointerGesturePhase.Canceled:
                    End(
                        in gesture,
                        AttackInputPhase.Canceled);
                    break;
            }
        }

        public void Tick()
        {
            double timestamp = clock.Now;

            foreach (KeyValuePair<PointerKey, ActiveAttack> entry
                     in activeAttacks)
            {
                ActiveAttack attack =
                    entry.Value;

                if (!attack.IsAimEligible)
                {
                    continue;
                }

                bool stateChanged =
                    EvaluateCharge(
                        attack,
                        timestamp);

                if (attack.ChargeState ==
                    AttackChargeState.Aiming)
                {
                    continue;
                }

                bool signalIntervalReached =
                    timestamp -
                    attack.LastChargeSignalTimestamp >=
                    attack.Settings.ChargeSignalInterval;

                if (!stateChanged &&
                    !signalIntervalReached)
                {
                    continue;
                }

                if (!stateChanged &&
                    attack.ChargeState ==
                    AttackChargeState.FullyCharged)
                {
                    continue;
                }

                attack.LastChargeSignalTimestamp =
                    timestamp;

                Emit(
                    attack.Pointer,
                    AttackInputPhase.ChargeChanged,
                    attack,
                    timestamp);
            }
        }

        public void Reset()
        {
            activeAttacks.Clear();
        }

        private void Begin(
            in PointerGestureSample gesture)
        {
            if (activeAttacks.TryGetValue(
                    gesture.Pointer,
                    out ActiveAttack previousAttack))
            {
                EvaluateCharge(
                    previousAttack,
                    gesture.Timestamp);

                Emit(
                    previousAttack.Pointer,
                    AttackInputPhase.Canceled,
                    previousAttack,
                    gesture.Timestamp);

                activeAttacks.Remove(
                    gesture.Pointer);
            }

            Rect viewport =
                viewportProvider.CurrentViewport;

            if (viewport.width <= 0f ||
                viewport.height <= 0f)
            {
                return;
            }

            AttackChargeSettings settings =
                settingsProvider.CurrentSettings;

            float shortSide =
                Mathf.Min(
                    viewport.width,
                    viewport.height);

            float minimumAimDistance =
                shortSide *
                settings.MinimumAimDistanceRatio;

            ActiveAttack attack =
                new ActiveAttack(
                    gesture.Pointer,
                    settings,
                    minimumAimDistance *
                    minimumAimDistance,
                    gesture.Timestamp);

            activeAttacks.Add(
                gesture.Pointer,
                attack);

            Emit(
                gesture.Pointer,
                AttackInputPhase.Began,
                attack,
                gesture.Timestamp);
        }

        private void Change(
            in PointerGestureSample gesture)
        {
            if (!activeAttacks.TryGetValue(
                    gesture.Pointer,
                    out ActiveAttack attack))
            {
                return;
            }

            attack.AimVector =
                gesture.Displacement;

            bool isAimEligible =
                gesture.DisplacementSquared >=
                attack.MinimumAimDistanceSquared;

            if (isAimEligible &&
                !attack.IsAimEligible)
            {
                attack.IsAimEligible = true;
                attack.AimEligibleTimestamp =
                    gesture.Timestamp;
            }
            else if (!isAimEligible &&
                     attack.IsAimEligible)
            {
                attack.IsAimEligible = false;
                attack.ChargeState =
                    AttackChargeState.Aiming;

                attack.ChargeRatio = 0f;
                attack.AimEligibleTimestamp = 0d;
            }

            bool stateChanged =
                EvaluateCharge(
                    attack,
                    gesture.Timestamp);

            if (stateChanged)
            {
                attack.LastChargeSignalTimestamp =
                    gesture.Timestamp;
            }

            Emit(
                gesture.Pointer,
                AttackInputPhase.AimChanged,
                attack,
                gesture.Timestamp);
        }

        private void End(
            in PointerGestureSample gesture,
            AttackInputPhase finalPhase)
        {
            if (!activeAttacks.TryGetValue(
                    gesture.Pointer,
                    out ActiveAttack attack))
            {
                return;
            }

            attack.AimVector =
                gesture.Displacement;

            EvaluateCharge(
                attack,
                gesture.Timestamp);

            activeAttacks.Remove(
                gesture.Pointer);

            Emit(
                gesture.Pointer,
                finalPhase,
                attack,
                gesture.Timestamp);
        }

        private static bool EvaluateCharge(
            ActiveAttack attack,
            double timestamp)
        {
            AttackChargeState previousState =
                attack.ChargeState;

            if (!attack.IsAimEligible)
            {
                attack.ChargeState =
                    AttackChargeState.Aiming;

                attack.ChargeRatio = 0f;

                return previousState !=
                       attack.ChargeState;
            }

            double chargeStartTimestamp =
                attack.AimEligibleTimestamp +
                attack.Settings.ChargeStartDelay;

            if (timestamp <
                chargeStartTimestamp)
            {
                attack.ChargeState =
                    AttackChargeState.Aiming;

                attack.ChargeRatio = 0f;

                return previousState !=
                       attack.ChargeState;
            }

            double chargeElapsed =
                timestamp -
                chargeStartTimestamp;

            attack.ChargeRatio =
                Mathf.Clamp01(
                    (float)(
                        chargeElapsed /
                        attack.Settings.FullChargeDuration));

            attack.ChargeState =
                attack.ChargeRatio >= 1f
                    ? AttackChargeState.FullyCharged
                    : AttackChargeState.Charging;

            return previousState !=
                   attack.ChargeState;
        }

        private void Emit(
            PointerKey pointer,
            AttackInputPhase phase,
            ActiveAttack attack,
            double timestamp)
        {
            AttackInputSample sample =
                new AttackInputSample(
                    pointer,
                    phase,
                    attack.ChargeState,
                    attack.AimVector,
                    attack.IsAimEligible,
                    attack.ChargeRatio,
                    timestamp);

            next.Receive(in sample);
        }

        private sealed class ActiveAttack
        {
            public PointerKey Pointer { get; }
            public AttackChargeSettings Settings { get; }
            public float MinimumAimDistanceSquared { get; }

            public Vector2 AimVector;
            public bool IsAimEligible;
            public double AimEligibleTimestamp;
            public double LastChargeSignalTimestamp;

            public AttackChargeState ChargeState;
            public float ChargeRatio;

            public ActiveAttack(
                PointerKey pointer,
                AttackChargeSettings settings,
                float minimumAimDistanceSquared,
                double startTimestamp)
            {
                Pointer = pointer;
                Settings = settings;

                MinimumAimDistanceSquared =
                    minimumAimDistanceSquared;

                AimVector = Vector2.zero;
                IsAimEligible = false;
                AimEligibleTimestamp = 0d;

                LastChargeSignalTimestamp =
                    startTimestamp;

                ChargeState =
                    AttackChargeState.Aiming;

                ChargeRatio = 0f;
            }
        }
    }
}