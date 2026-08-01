using Shield_Shot.InputSystem;
using Shield_Shot.InputSystem.Data;
using Shield_Shot.GameplayCore.Weapon.Core;
using Shield_Shot.GameplayCore.Weapon.Shield;
using Shield_Shot.GameplayCore.Augment;
using UnityEngine;

namespace Shield_Shot.InputSystem
{
    public class PlayerInputReceiver : MonoBehaviour
    {
        [Header("Analyzer References")]
        [SerializeField] private GestureAnalyzer attackAnalyzer;
        [SerializeField] private GestureAnalyzer defendAnalyzer;

        [Header("Target References")]
        [SerializeField] private WeaponBase currentWeapon;
        [SerializeField] private ShieldBase currentShield;

        [Header("Weapon Balance Settings")]
        [SerializeField] private float dragThreshold = 15f;

        [Header("Input Transform")]
        [SerializeField] private MonoBehaviour _contextTransformerBehaviour;

        [Header("InputGate")]
        [SerializeField] private MonoBehaviour _inputGateBehaviour;

        private IInputGate _inputGate;
        private IInputContextTransformer _contextTransformer;
        private bool _reportedInvalidInputGate;

        private void Awake()
        {
            if (_contextTransformerBehaviour != null)
            {
                _contextTransformer = _contextTransformerBehaviour as IInputContextTransformer;

                if (_contextTransformer == null)
                {
                    Debug.LogError("[PlayerInputReceiver] Context transformer must implement IInputContextTransformer.");
                }
            }

            ResolveInputGate();
        }

        private void OnEnable()
        {
            // 공격(Weapon) 이벤트 구독
            if (attackAnalyzer != null)
            {
                attackAnalyzer.OnInputStay += HandleAttackStay;
                attackAnalyzer.OnInputUp += HandleAttackUp;
                attackAnalyzer.OnInputCanceled += HandleAttackCancel;
            }

            // 방어(Shield) 이벤트 구독
            if (defendAnalyzer != null)
            {
                defendAnalyzer.OnInputBegan += HandleDefendBegan;
                defendAnalyzer.OnInputStay += HandleDefendStay;
            }
        }

        private void OnDisable()
        {
            // 메모리 누수 방지를 위한 이벤트 해제
            if (attackAnalyzer != null)
            {
                attackAnalyzer.OnInputStay -= HandleAttackStay;
                attackAnalyzer.OnInputUp -= HandleAttackUp;
                attackAnalyzer.OnInputCanceled -= HandleAttackCancel;
            }

            if (defendAnalyzer != null)
            {
                defendAnalyzer.OnInputBegan -= HandleDefendBegan;
                defendAnalyzer.OnInputStay -= HandleDefendStay;
            }
        }

        private bool CanReceiveInput()
        {
            if (AugmentPopupUI.IsOpen)
            {
                return false;
            }

            if (_inputGate == null)
            {
                ResolveInputGate();
            }

            return _inputGate == null || _inputGate.CanReceiveInput;
        }

        private void ResolveInputGate()
        {
            if (_inputGateBehaviour != null)
            {
                _inputGate = _inputGateBehaviour as IInputGate;
                if (_inputGate != null)
                {
                    return;
                }

                if (!_reportedInvalidInputGate)
                {
                    Debug.LogWarning($"[PlayerInputReceiver] Assigned input gate does not implement IInputGate: {_inputGateBehaviour.GetType().Name}. Trying to auto-resolve.");
                    _reportedInvalidInputGate = true;
                }
            }

            _inputGate = GetComponent<IInputGate>();
            if (_inputGate != null)
            {
                return;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInputGate inputGate)
                {
                    _inputGate = inputGate;
                    _inputGateBehaviour = behaviours[i];
                    return;
                }
            }
        }

        public void SetCurrentWeapon(WeaponBase newWeapon)
        {
            if (attackAnalyzer != null)
            {
            }

            currentWeapon = newWeapon;
            Debug.Log($"[PlayerInputReceiver] 입력 타겟 무기 변경 완료: {(newWeapon != null ? newWeapon.Type.ToString() : "None")}");
        }

        public void SetCurrentShield(ShieldBase newShield)
        {
            currentShield = newShield;
            Debug.Log($"[PlayerInputReceiver] 입력 타겟 방패 주입 완료: {(newShield != null ? newShield.gameObject.name : "None")}");
        }

        #region 공격(Weapon) 이벤트 처리
        private void HandleAttackStay(InputContext ctx)
        {
            if (!CanReceiveInput())
            {
                return;
            }
            ctx = TransformInputContext(ctx);

            // 기존 무기 로직에 가공 없이 그대로 주입
            currentWeapon?.HandleInputStay(ctx);
        }

        private void HandleAttackUp(InputContext ctx)
        {
            if (!CanReceiveInput())
            {
                return;
            }

            ctx = TransformInputContext(ctx);
            if (currentWeapon == null) return;

            // 무기 타입별 조건 처리를 오염된 Analyzer가 아닌 '여기서' 분기 처리합니다.
            if (currentWeapon.Type == WeaponType.Bow)
            {
                // 차징 상태였거나, 드래그 거리가 임계값 이상일 때만 발사 성공
                if (ctx.state == GestureState.Charging || ctx.totalDistance >= dragThreshold)
                {
                    currentWeapon.HandleInputUp(ctx);
                }
                else
                {
                    currentWeapon.Deactivate();
                }
            }
            else if (currentWeapon.Type == WeaponType.Rifle 
                || currentWeapon.Type == WeaponType.Sniper 
                || currentWeapon.Type == WeaponType.Shotgun 
                || currentWeapon.Type == WeaponType.Laser)
            {
                // 총은 제약 없이 바로 발사
                currentWeapon.HandleInputUp(ctx);
            }
        }

        private void HandleAttackCancel(InputContext ctx)
        {
            if (!CanReceiveInput())
            {
                return;
            }

            ctx = TransformInputContext(ctx);
            currentWeapon?.Deactivate();
        }
        #endregion

        #region 방어(Shield) 이벤트 처리
        private void HandleDefendBegan(InputContext ctx)
        {
            if (!CanReceiveInput())
            {
                return;
            }
            if (currentShield != null && currentShield.OrbitController != null)
            {
                currentShield.OrbitController.ResetDragOrigin();
            }
        }

        private void HandleDefendStay(InputContext ctx)
        {
            if (!CanReceiveInput())
            {
                return;
            }
            //ctx = TransformInputContext(ctx);
            // 방패는 오직 드래그 벡터값만 사용하여 회전 궤도 갱신
            currentShield?.UpdateFromDrag(ctx.dragVector);
        }
        #endregion

        public void SetContextTransformer(IInputContextTransformer contextTransformer)
        {
            _contextTransformer = contextTransformer;
        }
        private InputContext TransformInputContext(InputContext context)
        {
            return _contextTransformer != null
                ? _contextTransformer.Transform(context)
                : context;
        }

    }
}