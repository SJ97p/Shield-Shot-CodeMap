using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public interface IPointerSampleSink
    {
        void Receive(in PointerSample sample);
    }
}