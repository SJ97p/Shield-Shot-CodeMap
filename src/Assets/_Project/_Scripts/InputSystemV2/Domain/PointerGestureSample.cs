using System;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Gestures.Domain
{
    public readonly struct PointerGestureSample
    {
        public PointerKey Pointer { get; }
        public PointerGesturePhase Phase { get; }

        public Vector2 StartPosition { get; }
        public Vector2 PreviousPosition { get; }
        public Vector2 CurrentPosition { get; }

        public double StartTimestamp { get; }
        public double Timestamp { get; }

        public Vector2 Delta =>
            CurrentPosition - PreviousPosition;

        public Vector2 Displacement =>
            CurrentPosition - StartPosition;

        public float DisplacementSquared =>
            Displacement.sqrMagnitude;

        public double Duration =>
            Timestamp - StartTimestamp;

        public PointerGestureSample(
            PointerKey pointer,
            PointerGesturePhase phase,
            Vector2 startPosition,
            Vector2 previousPosition,
            Vector2 currentPosition,
            double startTimestamp,
            double timestamp)
        {
            if (pointer.DeviceKind ==
                PointerDeviceKind.Unknown)
            {
                throw new ArgumentException(
                    "Pointer key must be initialized.",
                    nameof(pointer));
            }

            if (phase == PointerGesturePhase.Unknown)
            {
                throw new ArgumentException(
                    "Gesture phase cannot be Unknown.",
                    nameof(phase));
            }

            if (startTimestamp < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startTimestamp));
            }

            if (timestamp < startTimestamp)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestamp),
                    timestamp,
                    "Timestamp cannot be earlier than the gesture start.");
            }

            Pointer = pointer;
            Phase = phase;

            StartPosition = startPosition;
            PreviousPosition = previousPosition;
            CurrentPosition = currentPosition;

            StartTimestamp = startTimestamp;
            Timestamp = timestamp;
        }
    }
}