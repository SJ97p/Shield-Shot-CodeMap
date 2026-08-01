using System;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Combat.Domain
{
    public readonly struct AttackInputSample
    {
        public PointerKey Pointer { get; }
        public AttackInputPhase Phase { get; }
        public AttackChargeState ChargeState { get; }
        public bool IsAimEligible { get; }

        public Vector2 AimVector { get; }
        public float ChargeRatio { get; }
        public double Timestamp { get; }

        public AttackInputSample(
            PointerKey pointer,
            AttackInputPhase phase,
            AttackChargeState chargeState,
            Vector2 aimVector,
            bool isAimEligible,
            float chargeRatio,
            double timestamp)
        {
            if (pointer.DeviceKind ==
                PointerDeviceKind.Unknown)
            {
                throw new ArgumentException(
                    "Pointer key must be initialized.",
                    nameof(pointer));
            }

            if (phase == AttackInputPhase.Unknown)
            {
                throw new ArgumentException(
                    "Attack input phase cannot be Unknown.",
                    nameof(phase));
            }

            if (chargeState ==
                AttackChargeState.Unknown)
            {
                throw new ArgumentException(
                    "Attack charge state cannot be Unknown.",
                    nameof(chargeState));
            }

            if (!IsFinite(aimVector.x) ||
                !IsFinite(aimVector.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aimVector));
            }

            if (float.IsNaN(chargeRatio) ||
                float.IsInfinity(chargeRatio) ||
                chargeRatio < 0f ||
                chargeRatio > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargeRatio));
            }

            if (double.IsNaN(timestamp) ||
                double.IsInfinity(timestamp) ||
                timestamp < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestamp));
            }

            Pointer = pointer;
            Phase = phase;
            ChargeState = chargeState;
            IsAimEligible = isAimEligible;
            AimVector = aimVector;
            ChargeRatio = chargeRatio;
            Timestamp = timestamp;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}