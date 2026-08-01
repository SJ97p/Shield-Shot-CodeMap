using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public interface IAttackInputSink
    {
        void Receive(
            in AttackInputSample sample);
    }
}