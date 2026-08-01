using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    public interface IBenchmarkPointerInputTarget
    {
        void BeginSequence(
            Vector2 viewportSize,
            double startTimestamp);

        void Receive(
            in BenchmarkPointerSample sample);

        void CompleteFrame();

        void EndSequence();
    }
}