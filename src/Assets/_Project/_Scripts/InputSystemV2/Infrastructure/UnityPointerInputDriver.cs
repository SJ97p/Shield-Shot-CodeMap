using System;
using Shield_Shot.InputSystemV2.Application;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;

namespace Shield_Shot.InputSystemV2.Infrastructure
{
    public sealed class UnityPointerInputDriver
        : MonoBehaviour
    {
        private ICancelablePointerSource source;
        private IPointerFrameSink pipeline;
        private bool enhancedTouchRegistered;

        public bool IsConfigured =>
            source != null &&
            pipeline != null;

        private void Awake()
        {
            source = new UnityPointerSource();
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            enhancedTouchRegistered = true;
        }

        private void Update()
        {
            if (!IsConfigured)
            {
                return;
            }

            source.Collect(pipeline);
            pipeline.CompleteFrame();
        }

        private void OnDisable()
        {
            try
            {
                CancelActiveInput();
            }
            finally
            {
                DisableEnhancedTouch();
            }
        }

        public void SetPipeline(
            IPointerFrameSink pointerPipeline)
        {
            pipeline = pointerPipeline
                ?? throw new ArgumentNullException(
                    nameof(pointerPipeline));
        }

        private void OnApplicationFocus(
            bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelActiveInput();
            }
        }

        private void OnApplicationPause(
            bool isPaused)
        {
            if (isPaused)
            {
                CancelActiveInput();
            }
        }
        private void CancelActiveInput()
        {
            if (source == null ||
                pipeline == null)
            {
                return;
            }

            try
            {
                source.CancelActivePointers(
                    pipeline,
                    InputState.currentTime);
            }
            finally
            {
                pipeline.CompleteFrame();
            }
        }
        private void DisableEnhancedTouch()
        {
            if (!enhancedTouchRegistered)
            {
                return;
            }

            EnhancedTouchSupport.Disable();
            enhancedTouchRegistered = false;
        }
    }
}