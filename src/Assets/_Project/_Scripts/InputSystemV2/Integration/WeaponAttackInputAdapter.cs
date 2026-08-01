using System.Collections.Generic;
using Shield_Shot.GameplayCore.Augment;
using Shield_Shot.GameplayCore.Weapon;
using Shield_Shot.GameplayCore.Weapon.Core;
using Shield_Shot.InputSystem.Data;
using Shield_Shot.InputSystemV2.Combat.Application;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Integration
{
    public sealed class WeaponAttackInputAdapter
        : MonoBehaviour, IAttackInputSink
    {
        [SerializeField]
        private WeaponManager weaponManager;

        private readonly Dictionary<PointerKey, ActiveWeaponInput>
            activeInputs =
                new Dictionary<PointerKey, ActiveWeaponInput>(2);

        public void Receive(
            in AttackInputSample sample)
        {
            switch (sample.Phase)
            {
                case AttackInputPhase.Began:
                    Begin(in sample);
                    break;

                case AttackInputPhase.AimChanged:
                case AttackInputPhase.ChargeChanged:
                    ApplyState(in sample);
                    break;

                case AttackInputPhase.Released:
                    Release(in sample);
                    break;

                case AttackInputPhase.Canceled:
                    Cancel(sample.Pointer);
                    break;
            }
        }

        public void ResetInput()
        {
            foreach (KeyValuePair<PointerKey, ActiveWeaponInput> entry
                     in activeInputs)
            {
                entry.Value.Weapon?.Deactivate();
            }

            activeInputs.Clear();
        }

        private void Update()
        {
            if (AugmentPopupUI.IsOpen ||
                Time.timeScale <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<PointerKey, ActiveWeaponInput> entry
                     in activeInputs)
            {
                ActiveWeaponInput activeInput = entry.Value;
                WeaponBase weapon = activeInput.Weapon;

                if (weapon == null ||
                    weapon.Type != WeaponType.Rifle)
                {
                    continue;
                }

                activeInput.ElapsedTime += Time.unscaledDeltaTime;
                weapon.HandleInputStay(
                    CreateLegacyStayContext(
                        entry.Key,
                        activeInput));
            }
        }

        private void OnDisable()
        {
            ResetInput();
        }

        private void Begin(
            in AttackInputSample sample)
        {
            if (weaponManager == null)
            {
                return;
            }

            if (activeInputs.TryGetValue(
                    sample.Pointer,
                    out ActiveWeaponInput previousInput))
            {
                previousInput.Weapon?.Deactivate();
                activeInputs.Remove(sample.Pointer);
            }

            WeaponBase weapon =
                weaponManager.CurrentWeapon;

            if (weapon == null)
            {
                return;
            }

            ActiveWeaponInput activeInput =
                new ActiveWeaponInput(
                    weapon,
                    sample.Timestamp);
            activeInput.UpdateState(in sample);

            activeInputs.Add(
                sample.Pointer,
                activeInput);

            weapon.ApplyAttackInputState(
                sample.AimVector,
                sample.ChargeRatio);
        }

        private void ApplyState(
            in AttackInputSample sample)
        {
            if (!activeInputs.TryGetValue(
                    sample.Pointer,
                    out ActiveWeaponInput activeInput))
            {
                return;
            }

            activeInput.Weapon.ApplyAttackInputState(
                sample.AimVector,
                sample.ChargeRatio);

            activeInput.UpdateState(in sample);
        }

        private void Release(
            in AttackInputSample sample)
        {
            if (!activeInputs.TryGetValue(
                    sample.Pointer,
                    out ActiveWeaponInput activeInput))
            {
                return;
            }

            activeInputs.Remove(
                sample.Pointer);

            WeaponBase weapon =
                activeInput.Weapon;

            if (weapon == null)
            {
                return;
            }

            weapon.ApplyAttackInputState(
                sample.AimVector,
                sample.ChargeRatio);

            if (weapon.Type == WeaponType.Bow &&
                !sample.IsAimEligible)
            {
                weapon.Deactivate();
                return;
            }

            InputContext legacyContext =
                CreateLegacyReleaseContext(
                    in sample,
                    activeInput.StartTimestamp);

            weapon.HandleInputUp(
                legacyContext);
        }

        private void Cancel(
            PointerKey pointer)
        {
            if (!activeInputs.TryGetValue(
                    pointer,
                    out ActiveWeaponInput activeInput))
            {
                return;
            }

            activeInputs.Remove(pointer);
            activeInput.Weapon?.Deactivate();
        }

        private static InputContext CreateLegacyReleaseContext(
            in AttackInputSample sample,
            double startTimestamp)
        {
            return new InputContext
            {
                fingerId =
                    sample.Pointer.PointerId,

                state =
                    ConvertChargeState(
                        sample.ChargeState),

                holdTime =
                    Mathf.Max(
                        0f,
                        (float)(
                            sample.Timestamp -
                            startTimestamp)),

                dragVector =
                    sample.AimVector,

                totalDistance =
                    sample.AimVector.magnitude
            };
        }

        private static InputContext CreateLegacyStayContext(
            PointerKey pointer,
            ActiveWeaponInput activeInput)
        {
            return new InputContext
            {
                fingerId = pointer.PointerId,
                state = ConvertChargeState(
                    activeInput.ChargeState),
                holdTime = activeInput.ElapsedTime,
                dragVector = activeInput.AimVector,
                totalDistance =
                    activeInput.AimVector.magnitude
            };
        }

        private static GestureState ConvertChargeState(
            AttackChargeState chargeState)
        {
            switch (chargeState)
            {
                case AttackChargeState.Charging:
                    return GestureState.Charging;

                case AttackChargeState.FullyCharged:
                    return GestureState.ChargedComplete;

                default:
                    return GestureState.Released;
            }
        }

        private sealed class ActiveWeaponInput
        {
            public WeaponBase Weapon { get; }
            public double StartTimestamp { get; }
            public Vector2 AimVector { get; private set; }
            public float ChargeRatio { get; private set; }
            public AttackChargeState ChargeState { get; private set; }
            public float ElapsedTime { get; set; }

            public ActiveWeaponInput(
                WeaponBase weapon,
                double startTimestamp)
            {
                Weapon = weapon;
                StartTimestamp = startTimestamp;
                AimVector = Vector2.zero;
                ChargeRatio = 0f;
                ChargeState = AttackChargeState.Aiming;
                ElapsedTime = 0f;
            }

            public void UpdateState(
                in AttackInputSample sample)
            {
                AimVector = sample.AimVector;
                ChargeRatio = sample.ChargeRatio;
                ChargeState = sample.ChargeState;
            }
        }
    }
}
