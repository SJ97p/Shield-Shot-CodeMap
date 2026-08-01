using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public interface IPointerStartBlockPolicy
    {
        bool ShouldBlock(
            in PointerSample beganSample);
    }
}