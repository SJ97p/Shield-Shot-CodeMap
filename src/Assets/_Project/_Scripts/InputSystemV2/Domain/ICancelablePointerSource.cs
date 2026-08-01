namespace Shield_Shot.InputSystemV2.Application
{
    public interface ICancelablePointerSource
        : IPointerSource
    {
        void CancelActivePointers(
            IPointerSampleSink sink,
            double timestamp);
    }
}