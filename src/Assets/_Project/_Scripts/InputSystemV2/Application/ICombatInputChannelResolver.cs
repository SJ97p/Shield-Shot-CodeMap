using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public interface ICombatInputChannelResolver
    {
        CombatInputChannel Resolve(
            in PointerSample beganSample);
    }
}