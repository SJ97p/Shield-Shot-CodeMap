namespace Shield_Shot.InputSystemV2.Application
{
    public interface IPointerFrameSink : IPointerSampleSink
    {
        void CompleteFrame();
    }
}