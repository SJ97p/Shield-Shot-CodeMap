using UnityEngine;

namespace Shield_Shot.InputSystem.Data
{
    [System.Serializable]
    public class InputSettings
    {
        public SplitMode splitMode =
            SplitMode.TopBottom;

        [Range(0.01f, 0.99f)]
        public float splitRatio = 0.5f;

        public bool isInverted = false;
    }
}