using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class FixedAttackChargeSettingsProvider
        : IAttackChargeSettingsProvider
    {
        public AttackChargeSettings CurrentSettings
        {
            get;
        }

        public FixedAttackChargeSettingsProvider(
            in AttackChargeSettings settings)
        {
            CurrentSettings = settings;
        }
    }
}