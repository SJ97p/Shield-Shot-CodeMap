using System.Collections.Generic; // 💡 HashSet 사용을 위해 추가
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;

using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Shield_Shot.InputSystem
{
    public class InputProvider : MonoBehaviour
    {
        [Header("Router 참조")]
        [SerializeField] private TouchRouter touchRouter;

        // 💡 [핵심] UI에서 시작된 손가락 ID(또는 마우스 -1)를 기억하는 블랙리스트 저장소
        private HashSet<int> uiIgnoredFingers = new HashSet<int>();

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            touchRouter?.ResetRouting(
                notifyCancellation: true);

            uiIgnoredFingers.Clear();
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (touchRouter == null) return;

            // 1. 실제 모바일/태블릿 터치 처리 (EnhancedTouch)
            var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            if (activeTouches.Count > 0)
            {
                foreach (var touch in activeTouches)
                {
                    int id = touch.finger.index;

                    // [처리 1] 새로운 터치가 시작될 때 UI 위였다면 블랙리스트에 등록
                    if (touch.phase == TouchPhase.Began)
                    {
                        if (IsUIBlocked(id))
                        {
                            uiIgnoredFingers.Add(id);
                            continue; // 인게임 라우터로 안 보냄
                        }
                    }

                    // [처리 2] 이미 UI에서 시작된 손가락이라면 움직이거나 멈춰도 무조건 패스
                    if (uiIgnoredFingers.Contains(id))
                    {
                        // 터치가 끝나면 블랙리스트에서 해제해줍니다.
                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            uiIgnoredFingers.Remove(id);
                        }
                        continue;
                    }

                    // [처리 3] UI가 아닌 순수 인게임 터치만 라우터로 배송
                    touchRouter.ProcessTouch(id, touch.screenPosition, touch.phase);
                }
            }
            // 2. 터치가 없을 경우 PC 에디터용 마우스 시뮬레이션
            else if (Pointer.current != null)
            {
                SimulateMouseAsTouch();
            }
        }

        private void SimulateMouseAsTouch()
        {
            var pointer = Pointer.current;
            Vector2 mousePos = pointer.position.ReadValue();
            bool isPressed = pointer.press.isPressed;
            bool wasPressedThisFrame = pointer.press.wasPressedThisFrame;
            bool wasReleasedThisFrame = pointer.press.wasReleasedThisFrame;

            int mouseId = -1; // 마우스는 편의상 -1 고정

            // 마우스 클릭 시작 시 UI 위라면 블랙리스트 등록
            if (wasPressedThisFrame)
            {
                if (IsUIBlocked(mouseId))
                {
                    uiIgnoredFingers.Add(mouseId);
                    return;
                }
            }

            // 블랙리스트에 있으면 마우스 드래그/업 이벤트 전부 인게임 스킵
            if (uiIgnoredFingers.Contains(mouseId))
            {
                if (wasReleasedThisFrame)
                {
                    uiIgnoredFingers.Remove(mouseId);
                }
                return;
            }

            // 순수 인게임 마우스 클릭 처리
            if (wasPressedThisFrame)
            {
                touchRouter.ProcessTouch(mouseId, mousePos, TouchPhase.Began);
            }
            else if (isPressed)
            {
                Vector2 delta = pointer.delta.ReadValue();
                TouchPhase phase = (delta.sqrMagnitude > 0.01f) ? TouchPhase.Moved : TouchPhase.Stationary;
                touchRouter.ProcessTouch(mouseId, mousePos, phase);
            }
            else if (wasReleasedThisFrame)
            {
                touchRouter.ProcessTouch(mouseId, mousePos, TouchPhase.Ended);
            }
        }

        private bool IsUIBlocked(int pointerId)
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject(pointerId);
        }
    }
}