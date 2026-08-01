using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class FixedCombatInputLayoutProvider
        : ICombatInputLayoutProvider
    {
        public CombatInputLayout CurrentLayout { get; }

        public FixedCombatInputLayoutProvider(
            in CombatInputLayout layout)
        {
            CurrentLayout = layout;
        }
    }
}