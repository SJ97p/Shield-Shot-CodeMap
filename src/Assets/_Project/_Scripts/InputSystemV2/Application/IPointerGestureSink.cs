using Shield_Shot.InputSystemV2.Gestures.Domain;

namespace Shield_Shot.InputSystemV2.Gestures.Application
{
    public interface IPointerGestureSink
    {
        void Receive(
            in PointerGestureSample gesture);
    }
}