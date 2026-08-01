using Shield_Shot.GameplayCore.Weapon;
using Shield_Shot.GameplayCore.Weapon.Shield;
using Shield_Shot.InputSystemV2.Combat.Application;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Integration
{
    public sealed class ShieldDefenseInputAdapter
        : MonoBehaviour,
          IDefenseInputSink
    {
        [SerializeField]
        private WeaponManager weaponManager;

        private bool hasActivePointer;
        private PointerKey activePointer;

        private ShieldOrbitController activeOrbitController;

        public void Receive(
            in DefenseInputSample sample)
        {
            switch (sample.Phase)
            {
                case DefenseInputPhase.Began:
                    Begin(in sample);
                    break;

                case DefenseInputPhase.DirectionChanged:
                    ApplyDirection(in sample);
                    break;

                case DefenseInputPhase.Released:
                case DefenseInputPhase.Canceled:
                    End(sample.Pointer);
                    break;
            }
        }

        public void ResetInput()
        {
            hasActivePointer = false;
            activePointer = default;
            activeOrbitController = null;
        }

        private void OnDisable()
        {
            ResetInput();
        }

        private void Begin(
            in DefenseInputSample sample)
        {
            if (hasActivePointer ||
                weaponManager == null)
            {
                return;
            }

            SkillShield currentShield =
                weaponManager.CurrentShield;

            ShieldOrbitController orbitController =
                currentShield != null
                    ? currentShield.OrbitController
                    : null;

            if (orbitController == null)
            {
                return;
            }

            activePointer = sample.Pointer;
            activeOrbitController = orbitController;
            hasActivePointer = true;

            activeOrbitController.ResetDragOrigin();
        }

        private void ApplyDirection(
            in DefenseInputSample sample)
        {
            if (!IsActivePointer(sample.Pointer) ||
                activeOrbitController == null)
            {
                return;
            }

            activeOrbitController.UpdateOrbitFromDrag(
                sample.Displacement);
        }

        private void End(
            PointerKey pointer)
        {
            if (!IsActivePointer(pointer))
            {
                return;
            }

            ResetInput();
        }

        private bool IsActivePointer(
            PointerKey pointer)
        {
            return
                hasActivePointer &&
                activePointer == pointer;
        }
    }
}