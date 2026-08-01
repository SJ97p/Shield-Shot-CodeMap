using System;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Combat.Domain
{
    public readonly struct DefenseInputSample
    {
        public PointerKey Pointer { get; }
        public DefenseInputPhase Phase { get; }

        public Vector2 StartPosition { get; }
        public Vector2 CurrentPosition { get; }
        public Vector2 Displacement { get; }

        public double Timestamp { get; }

        public DefenseInputSample(
            PointerKey pointer,
            DefenseInputPhase phase,
            Vector2 startPosition,
            Vector2 currentPosition,
            Vector2 displacement,
            double timestamp)
        {
            if (pointer.DeviceKind == PointerDeviceKind.Unknown)
            {
                throw new ArgumentException(
                    "Pointer key must be initialized.",
                    nameof(pointer));
            }

            if (phase == DefenseInputPhase.Unknown)
            {
                throw new ArgumentException(
                    "Defense phase cannot be Unknown.",
                    nameof(phase));
            }

            if (!IsFinite(startPosition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startPosition));
            }

            if (!IsFinite(currentPosition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentPosition));
            }

            if (!IsFinite(displacement))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(displacement));
            }

            if (double.IsNaN(timestamp) ||
                double.IsInfinity(timestamp) ||
                timestamp < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestamp));
            }

            Pointer = pointer;
            Phase = phase;

            StartPosition = startPosition;
            CurrentPosition = currentPosition;
            Displacement = displacement;

            Timestamp = timestamp;
        }

        private static bool IsFinite(Vector2 value)
        {
            return
                !float.IsNaN(value.x) &&
                !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.y);
        }
    }
}