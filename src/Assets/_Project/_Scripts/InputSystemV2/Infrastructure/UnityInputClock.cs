using Shield_Shot.InputSystemV2.Application;
using UnityEngine.InputSystem.LowLevel;

namespace Shield_Shot.InputSystemV2.Infrastructure
{
    public sealed class UnityInputClock
        : IInputClock
    {
        public double Now =>
            InputState.currentTime;
    }
}