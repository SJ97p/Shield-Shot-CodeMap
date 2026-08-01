using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class AcceptAllPointerFilter : IPointerFilter
    {
        public bool Accept(in PointerSample sample)
        {
            return true;
        }
    }
}