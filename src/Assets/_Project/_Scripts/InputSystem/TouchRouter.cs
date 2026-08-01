using System.Collections.Generic;
using Shield_Shot.InputSystem.Data;
using Shield_Shot.Settings;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

// 💡 [핵심] 이 스크립트 내에서 모든 'TouchPhase'는 신형 인풋시스템 것으로 고정합니다.
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Shield_Shot.InputSystem
{
    public class TouchRouter : MonoBehaviour
    {
        [SerializeField] private GestureAnalyzer attackAnalyzer;
        [SerializeField] private GestureAnalyzer defendAnalyzer;

        private Dictionary<int, InputZone> fingerRoutingTable = new Dictionary<int, InputZone>();

        // 매개변수 타입도 자동으로 신형 TouchPhase로 인식됩니다.
        public void ProcessTouch(int fingerId, Vector2 touchPos, TouchPhase phase)
        {
            if (GameSettingsManager.Instance == null) return;

            InputSettings settings = GameSettingsManager.Instance.Input;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            InputZone targetZone;

            // 1. 터치 시작(Began) 시 배송지 판정 및 등록
            if (phase == TouchPhase.Began)
            {
                bool isFirstHalf = false;

                float splitRatio =
                    Mathf.Clamp(
                        settings.splitRatio,
                        0.01f,
                        0.99f);

                if (settings.splitMode == SplitMode.LeftRight)
                {
                    float boundary =
                        screenWidth * splitRatio;

                    isFirstHalf =
                        touchPos.x < boundary;
                }
                else if (settings.splitMode == SplitMode.TopBottom)
                {
                    float boundary =
                        screenHeight * splitRatio;

                    isFirstHalf =
                        touchPos.y < boundary;
                }

                if (isFirstHalf)
                {
                    targetZone = settings.isInverted ? InputZone.Attack : InputZone.Defend;
                }
                else
                {
                    targetZone = settings.isInverted ? InputZone.Defend : InputZone.Attack;
                }

                fingerRoutingTable[fingerId] = targetZone;
            }
            // 2. 터치 유지/이동 시 기존 배송지 추적
            else
            {
                if (!fingerRoutingTable.TryGetValue(fingerId, out targetZone))
                {
                    return;
                }
            }

            // 3. 결과 로그 출력 및 해당 Analyzer로 배송 (에러 났던 67번째 줄 부근)
            //Debug.Log($"[Router] 손가락ID: {fingerId} | 상태: {phase} -> 최종 배송지: {targetZone}");

            if (targetZone == InputZone.Attack)
                attackAnalyzer.ReceiveRawTouch(fingerId, touchPos, phase);
            else
                defendAnalyzer.ReceiveRawTouch(fingerId, touchPos, phase);

            // 4. 터치 종료 시 라우팅 테이블에서 제거
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
            {
                fingerRoutingTable.Remove(fingerId);
            }
        }
        public void ResetRouting(
    bool notifyCancellation = true)
        {
            attackAnalyzer?.ResetTracking(
                notifyCancellation);

            defendAnalyzer?.ResetTracking(
                notifyCancellation);

            fingerRoutingTable.Clear();
        }
    }
}