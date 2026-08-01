using Shield_Shot.InputSystemV2.Gestures.Domain;

namespace Shield_Shot.InputSystemV2.Gestures.Application
{
    public sealed class DiscardPointerGestureSink
        : IPointerGestureSink
    {
        public static DiscardPointerGestureSink Instance
        {
            get;
        } = new DiscardPointerGestureSink();

        private DiscardPointerGestureSink()
        {
        }

        public void Receive(
            in PointerGestureSample gesture)
        {
        }
    }
}