using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public interface IPointerFilter
    {
        bool Accept(in PointerSample sample);
    }
}