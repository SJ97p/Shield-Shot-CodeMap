using System;
using System.Collections.Generic;
using Shield_Shot.InputSystem.Data;
using UnityEngine;
using Unity.Profiling;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Shield_Shot.InputSystem
{
    public class GestureAnalyzer : MonoBehaviour
    {
        [Header("Zone Identity")]
        [Tooltip("이 Analyzer가 어느 입력 존(공격/방어)에 속하는지. Inspector에서 반드시 맞게 지정할 것.")]
        [SerializeField] private InputZone _zone;

        public InputZone Zone => _zone;

        [Header("Threshold Settings")]
        [SerializeField] private float chargeWaitTime = 0.5f;
        [SerializeField] private float minDragDistanceForCharge = 50f;

        [Header("Diagnostics")]
        [SerializeField] private bool enableDebugLogging;

        // 🌟 외부(무기/방패 매니저 등)에서 구독할 이벤트들
        public event Action<InputContext> OnInputBegan;
        public event Action<InputContext> OnInputStay;
        public event Action<InputContext> OnInputUp;
        public event Action<InputContext> OnInputCanceled;

        // 기존 내부 데이터 추적용 딕셔너리
        private Dictionary<int, TrackedTouch> trackedTouches = new();
        private TrackedTouch activeChargingTouch;

        private static readonly ProfilerMarker
    GestureUpdateMarker =
        new ProfilerMarker(
            "Input.V1.GestureUpdate");

        private void Update()
        {
            if (trackedTouches.Count == 0)
            {
                return;
            }

            GestureUpdateMarker.Begin();

            try
            {
                foreach (TrackedTouch touch
                         in trackedTouches.Values)
                {
                    if (touch.state ==
                            GestureState.Charging ||
                        touch.state ==
                            GestureState.ChargedComplete)
                    {
                        continue;
                    }

                    float elapsed =
                        Time.time -
                        touch.startTime;

                    if (minDragDistanceForCharge > 0f)
                    {
                        float dragDist =
                            Vector2.Distance(
                                touch.startPosition,
                                touch.currentPosition);

                        if (dragDist <
                            minDragDistanceForCharge)
                        {
                            touch.lastMovedTime =
                                Time.time;

                            continue;
                        }

                        elapsed =
                            Time.time -
                            touch.lastMovedTime;
                    }

                    FireContextEvent(
                        touch,
                        GestureState.Tracking,
                        OnInputStay);

                    if (elapsed >= chargeWaitTime &&
                        !touch.isChargingTriggered)
                    {
                        touch.state =
                            GestureState.Charging;

                        touch.isChargingTriggered =
                            true;

                        activeChargingTouch =
                            touch;

                        if (enableDebugLogging)
                        {
                            Debug.Log(
                                $"🔥 차징 시작! " +
                                $"(Finger: {touch.fingerId})");
                        }
                    }
                }

                if (activeChargingTouch != null &&
                    trackedTouches.ContainsKey(
                        activeChargingTouch.fingerId))
                {
                    FireContextEvent(
                        activeChargingTouch,
                        GestureState.Charging,
                        OnInputStay);
                }
            }
            finally
            {
                GestureUpdateMarker.End();
            }
        }

        #region TouchRouter로부터 원시 터치 수신
        public void ReceiveRawTouch(int fingerId, Vector2 touchPos, TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: OnTouchBegan(fingerId, touchPos); break;
                case TouchPhase.Moved: OnTouchMoved(fingerId, touchPos); break;
                case TouchPhase.Stationary:
                    if (trackedTouches.TryGetValue(fingerId, out var t))
                        t.currentPosition = touchPos;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled: OnTouchEnded(fingerId, touchPos, phase); break;
            }
        }
        #endregion

        #region 내부 터치 단계 처리
        private void OnTouchBegan(int fingerId, Vector2 pos)
        {
            if (trackedTouches.ContainsKey(fingerId)) return;

            var newTouch = new TrackedTouch
            {
                fingerId = fingerId,
                startPosition = pos,
                currentPosition = pos,
                startTime = Time.time,
                lastMovedTime = Time.time,
                state = GestureState.Tracking
            };
            trackedTouches.Add(fingerId, newTouch);

            FireContextEvent(newTouch, GestureState.Tracking, OnInputBegan);
        }

        private void OnTouchMoved(int fingerId, Vector2 pos)
        {
            if (!trackedTouches.TryGetValue(fingerId, out var touch)) return;

            float moveDist = Vector2.Distance(touch.currentPosition, pos);
            touch.currentPosition = pos;

            // 활(Bow) 등에서 드래그 도중 차징이 취소되는 예외 처리 조건
            if (minDragDistanceForCharge > 0f && moveDist > 100.0f)
            {
                touch.lastMovedTime = Time.time;

                if (touch.state == GestureState.Charging || touch.state == GestureState.ChargedComplete)
                {
                    touch.state = GestureState.Tracking;
                    touch.isChargingTriggered = false;
                    activeChargingTouch = null;

                    FireContextEvent(touch, GestureState.Canceled, OnInputCanceled);
                }
            }
        }

        private void OnTouchEnded(int fingerId, Vector2 pos, TouchPhase phase)
        {
            if (!trackedTouches.TryGetValue(fingerId, out var touch)) return;
            touch.currentPosition = pos;

            if (phase != TouchPhase.Canceled)
            {
                // 손을 정상적으로 뗐을 때 기존 상태가 차징이었다면 그 상태 유지해서 Up 이벤트 송출
                GestureState finalState = (touch.state == GestureState.Charging) ? GestureState.Charging : GestureState.Released;
                FireContextEvent(touch, finalState, OnInputUp);
            }
            else
            {
                FireContextEvent(touch, GestureState.Canceled, OnInputCanceled);
            }

            if (activeChargingTouch?.fingerId == fingerId)
                activeChargingTouch = null;

            trackedTouches.Remove(fingerId);
        }
        #endregion

        // InputContext와 GestureState로 데이터를 포장하여 이벤트를 실행하는 메서드
        private void FireContextEvent(TrackedTouch touch, GestureState inputState, Action<InputContext> targetEvent)
        {
            if (targetEvent == null) return;

            InputContext ctx = new InputContext
            {
                fingerId = touch.fingerId,
                state = inputState,
                holdTime = Time.time - touch.startTime,
                dragVector = touch.currentPosition - touch.startPosition,
                totalDistance = Vector2.Distance(touch.startPosition, touch.currentPosition),
                startPosition = touch.startPosition
            };

            targetEvent.Invoke(ctx);
        }

        public void ResetTracking(
    bool notifyCancellation = true)
        {
            if (notifyCancellation &&
                OnInputCanceled != null)
            {
                foreach (TrackedTouch touch
                         in trackedTouches.Values)
                {
                    FireContextEvent(
                        touch,
                        GestureState.Canceled,
                        OnInputCanceled);
                }
            }

            trackedTouches.Clear();
            activeChargingTouch = null;
        }
    }
}