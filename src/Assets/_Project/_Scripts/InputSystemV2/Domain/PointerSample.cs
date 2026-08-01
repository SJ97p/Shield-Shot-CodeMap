using System;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Domain
{
    public readonly struct PointerSample
    {
        public int PointerId { get; }
        public PointerDeviceKind DeviceKind { get; }
        public PointerPhase Phase { get; }
        public Vector2 ScreenPosition { get; }
        public double Timestamp { get; }

        public PointerSample(
            int pointerId,
            PointerDeviceKind deviceKind,
            PointerPhase phase,
            Vector2 screenPosition,
            double timestamp)
        {
            if (pointerId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pointerId),
                    pointerId,
                    "Pointer ID cannot be negative.");
            }

            if (deviceKind == PointerDeviceKind.Unknown)
            {
                throw new ArgumentException(
                    "Pointer device kind cannot be Unknown.",
                    nameof(deviceKind));
            }

            if (timestamp < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestamp),
                    timestamp,
                    "Timestamp cannot be negative.");
            }

            PointerId = pointerId;
            DeviceKind = deviceKind;
            Phase = phase;
            ScreenPosition = screenPosition;
            Timestamp = timestamp;
        }
    }
}