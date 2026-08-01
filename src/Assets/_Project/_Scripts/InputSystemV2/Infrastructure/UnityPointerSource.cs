using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using EnhancedTouchData =
    UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Shield_Shot.InputSystemV2.Infrastructure
{
    public sealed class UnityPointerSource : ICancelablePointerSource
    {
        private readonly Dictionary<PointerKey, Vector2>
    activePointers = new Dictionary<PointerKey, Vector2>(4);

        private const int MousePointerId = 0;
        private const float MouseMovementEpsilonSquared = 0.01f;

        public void Collect(IPointerSampleSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            double timestamp = InputState.currentTime;
            var activeTouches = EnhancedTouchData.activeTouches;

            if (activeTouches.Count > 0)
            {
                CollectTouches(
                    activeTouches,
                    timestamp,
                    sink);

                return;
            }

            CollectMouse(
                timestamp,
                sink);
        }

        private void CollectTouches(
    UnityEngine.InputSystem.Utilities.ReadOnlyArray<EnhancedTouchData> touches,
    double timestamp,
    IPointerSampleSink sink)
        {
            for (int index = 0;
                 index < touches.Count;
                 index++)
            {
                EnhancedTouchData touch =
                    touches[index];

                if (!TryConvertPhase(
                        touch.phase,
                        out PointerPhase phase))
                {
                    continue;
                }

                if (phase == PointerPhase.Stationary)
                {
                    continue;
                }

                PointerSample sample =
                    new PointerSample(
                        pointerId: touch.finger.index,
                        deviceKind: PointerDeviceKind.Touch,
                        phase: phase,
                        screenPosition: touch.screenPosition,
                        timestamp: timestamp);

                sink.Receive(in sample);
            }
        }

        private void CollectMouse(
            double timestamp,
            IPointerSampleSink sink)
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            PointerPhase phase;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                phase = PointerPhase.Began;
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                phase = PointerPhase.Ended;
            }
            else if (mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();

                if (delta.sqrMagnitude <=
                    MouseMovementEpsilonSquared)
                {
                    return;
                }

                phase = PointerPhase.Moved;
            }
            else
            {
                return;
            }

            PointerSample sample =
                new PointerSample(
                    pointerId: MousePointerId,
                    deviceKind: PointerDeviceKind.Mouse,
                    phase: phase,
                    screenPosition: mouse.position.ReadValue(),
                    timestamp: timestamp);

            Deliver(in sample, sink);
        }
        private void Deliver(
    in PointerSample sample,
    IPointerSampleSink sink)
        {
            PointerKey key =
                PointerKey.From(in sample);

            switch (sample.Phase)
            {
                case PointerPhase.Began:
                case PointerPhase.Moved:
                case PointerPhase.Stationary:
                    activePointers[key] =
                        sample.ScreenPosition;
                    break;

                case PointerPhase.Ended:
                case PointerPhase.Canceled:
                    activePointers.Remove(key);
                    break;
            }

            sink.Receive(in sample);
        }

        public void CancelActivePointers(
    IPointerSampleSink sink,
    double timestamp)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(
                    nameof(sink));
            }

            if (timestamp < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestamp));
            }

            try
            {
                foreach (KeyValuePair<PointerKey, Vector2> entry
                         in activePointers)
                {
                    PointerKey key =
                        entry.Key;

                    PointerSample canceledSample =
                        new PointerSample(
                            pointerId: key.PointerId,
                            deviceKind: key.DeviceKind,
                            phase: PointerPhase.Canceled,
                            screenPosition: entry.Value,
                            timestamp: timestamp);

                    sink.Receive(in canceledSample);
                }
            }
            finally
            {
                activePointers.Clear();
            }
        }

        private static bool TryConvertPhase(
            InputTouchPhase inputPhase,
            out PointerPhase phase)
        {
            switch (inputPhase)
            {
                case InputTouchPhase.Began:
                    phase = PointerPhase.Began;
                    return true;

                case InputTouchPhase.Moved:
                    phase = PointerPhase.Moved;
                    return true;

                case InputTouchPhase.Stationary:
                    phase = PointerPhase.Stationary;
                    return true;

                case InputTouchPhase.Ended:
                    phase = PointerPhase.Ended;
                    return true;

                case InputTouchPhase.Canceled:
                    phase = PointerPhase.Canceled;
                    return true;

                default:
                    phase = default;
                    return false;
            }
        }
    }
}