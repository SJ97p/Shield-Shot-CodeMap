using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public interface IDefenseInputSink
    {
        void Receive(in DefenseInputSample sample);
    }
}