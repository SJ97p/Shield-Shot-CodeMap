using System;

namespace Shield_Shot.InputSystemV2.Combat.Domain
{
    public readonly struct AttackChargeSettings
    {
        public float MinimumAimDistanceRatio { get; }
        public double ChargeStartDelay { get; }
        public double FullChargeDuration { get; }
        public double ChargeSignalInterval { get; }

        public AttackChargeSettings(
            float minimumAimDistanceRatio,
            double chargeStartDelay,
            double fullChargeDuration,
            double chargeSignalInterval)
        {
            if (float.IsNaN(minimumAimDistanceRatio) ||
                float.IsInfinity(minimumAimDistanceRatio) ||
                minimumAimDistanceRatio < 0f ||
                minimumAimDistanceRatio > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumAimDistanceRatio));
            }

            if (double.IsNaN(chargeStartDelay) ||
                double.IsInfinity(chargeStartDelay) ||
                chargeStartDelay < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargeStartDelay));
            }

            if (double.IsNaN(fullChargeDuration) ||
                double.IsInfinity(fullChargeDuration) ||
                fullChargeDuration <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullChargeDuration));
            }
            if (double.IsNaN(chargeSignalInterval) ||
                double.IsInfinity(chargeSignalInterval) ||
                chargeSignalInterval <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargeSignalInterval));
            }

            MinimumAimDistanceRatio =
                minimumAimDistanceRatio;

            ChargeStartDelay =
                chargeStartDelay;

            FullChargeDuration =
                fullChargeDuration;

            ChargeSignalInterval =
                chargeSignalInterval;
        }
    }
}