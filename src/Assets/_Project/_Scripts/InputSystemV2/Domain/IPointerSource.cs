namespace Shield_Shot.InputSystemV2.Application
{
    public interface IPointerSource
    {
        void Collect(IPointerSampleSink sink);
    }
}