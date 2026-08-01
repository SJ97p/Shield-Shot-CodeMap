using System;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Gestures.Application;
using Shield_Shot.InputSystemV2.Gestures.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class DefenseGestureInterpreter
        : IPointerGestureSink
    {
        private readonly IDefenseInputSink next;

        public DefenseGestureInterpreter(
            IDefenseInputSink next)
        {
            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));
        }

        public void Receive(
            in PointerGestureSample gesture)
        {
            DefenseInputPhase phase =
                ConvertPhase(gesture.Phase);

            var sample =
                new DefenseInputSample(
                    pointer: gesture.Pointer,
                    phase: phase,
                    startPosition: gesture.StartPosition,
                    currentPosition: gesture.CurrentPosition,
                    displacement: gesture.Displacement,
                    timestamp: gesture.Timestamp);

            next.Receive(in sample);
        }

        private static DefenseInputPhase ConvertPhase(
            PointerGesturePhase phase)
        {
            switch (phase)
            {
                case PointerGesturePhase.Began:
                    return DefenseInputPhase.Began;

                case PointerGesturePhase.Changed:
                    return DefenseInputPhase.DirectionChanged;

                case PointerGesturePhase.Completed:
                    return DefenseInputPhase.Released;

                case PointerGesturePhase.Canceled:
                    return DefenseInputPhase.Canceled;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(phase),
                        phase,
                        "Unsupported pointer gesture phase.");
            }
        }
    }
}