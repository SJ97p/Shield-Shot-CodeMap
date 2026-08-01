using UnityEngine;

namespace Shield_Shot.InputSystemV2.Application
{
    public interface IPointerViewportProvider
    {
        Rect CurrentViewport { get; }
    }
}