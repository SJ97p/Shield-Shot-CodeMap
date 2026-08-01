using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine.EventSystems;

namespace Shield_Shot.InputSystemV2.Infrastructure
{
    public sealed class EventSystemPointerStartBlockPolicy
        : IPointerStartBlockPolicy
    {
        private readonly List<RaycastResult> raycastResults;

        private EventSystem cachedEventSystem;
        private PointerEventData pointerEventData;

        public EventSystemPointerStartBlockPolicy(
            int initialRaycastCapacity = 16)
        {
            raycastResults =
                new List<RaycastResult>(
                    initialRaycastCapacity);
        }

        public bool ShouldBlock(
            in PointerSample beganSample)
        {
            EventSystem currentEventSystem =
                EventSystem.current;

            if (currentEventSystem == null)
            {
                return false;
            }

            PreparePointerEventData(
                currentEventSystem,
                in beganSample);

            raycastResults.Clear();

            currentEventSystem.RaycastAll(
                pointerEventData,
                raycastResults);

            bool isBlocked =
                raycastResults.Count > 0;

            raycastResults.Clear();

            return isBlocked;
        }

        private void PreparePointerEventData(
            EventSystem currentEventSystem,
            in PointerSample sample)
        {
            if (cachedEventSystem != currentEventSystem ||
                pointerEventData == null)
            {
                cachedEventSystem =
                    currentEventSystem;

                pointerEventData =
                    new PointerEventData(
                        currentEventSystem);
            }
            else
            {
                pointerEventData.Reset();
            }

            pointerEventData.pointerId =
                sample.PointerId;

            pointerEventData.position =
                sample.ScreenPosition;
        }
    }
}