using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public interface ICombatInputLayoutProvider
    {
        CombatInputLayout CurrentLayout { get; }
    }
}