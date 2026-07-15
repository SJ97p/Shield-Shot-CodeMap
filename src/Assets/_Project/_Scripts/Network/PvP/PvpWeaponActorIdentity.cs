using Fusion;
using Shield_Shot.GameplayCore.Network.Match;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PvpWeaponActorIdentity : NetworkBehaviour
    {
        private const string PlayerHitboxName = "Player";

        [Networked] public PlayerRef Owner { get; private set; }
        [Networked] public int SideValue { get; private set; }
        [Networked] public Vector3 SpawnPosition { get; private set; }
        [Networked] public Vector3 SpawnEulerAngles { get; private set; }
        [Networked] public float SpawnScale { get; private set; }
        [Networked] public NetworkBool SpawnPoseInitialized { get; private set; }

        public PlayerSide Side => (PlayerSide)SideValue;
        public bool IsLocalActor => Object != null && Object.HasInputAuthority;

        private bool _spawnPoseApplied;

        public void Initialize(PlayerRef owner, PlayerSide side)
        {
            Initialize(owner, side, new Pose(transform.position, transform.rotation), transform.localScale.x);
        }

        public void Initialize(PlayerRef owner, PlayerSide side, Pose spawnPose)
        {
            Initialize(owner, side, spawnPose, transform.localScale.x);
        }

        public void Initialize(PlayerRef owner, PlayerSide side, Pose spawnPose, float spawnScale)
        {
            Owner = owner;
            SideValue = (int)side;
            SpawnPosition = spawnPose.position;
            SpawnEulerAngles = spawnPose.rotation.eulerAngles;
            SpawnScale = Mathf.Max(0.01f, spawnScale);
            SpawnPoseInitialized = true;
            transform.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);
            transform.localScale = Vector3.one * SpawnScale;
            _spawnPoseApplied = true;
        }

        public override void Spawned()
        {
            ApplyPvpWeaponLayer();
            ApplyNetworkSpawnPose();
            RenameForDebug();

            Debug.Log(
                $"[PvpWeaponActorIdentity] Spawned. " +
                $"Owner: {Owner}, " +
                $"Side: {Side}, " +
                $"HasInputAuthority: {Object.HasInputAuthority}, " +
                $"HasStateAuthority: {Object.HasStateAuthority}, " +
                $"SpawnPoseInitialized: {SpawnPoseInitialized}, " +
                $"Position: {transform.position}, " +
                $"NetworkSpawnPosition: {SpawnPosition}");

            LogColliderDiagnostics();
        }

        public override void Render()
        {
            ApplyNetworkSpawnPose();
        }

        private void ApplyNetworkSpawnPose()
        {
            if (!SpawnPoseInitialized || _spawnPoseApplied)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(SpawnEulerAngles);
            transform.SetPositionAndRotation(SpawnPosition, rotation);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, SpawnScale);
            _spawnPoseApplied = true;
        }

        private void ApplyPvpWeaponLayer()
        {
            int pvpWeaponLayer = LayerMask.NameToLayer("PvpWeapon");
            if (pvpWeaponLayer < 0)
            {
                Debug.LogWarning("[PvpWeaponActorIdentity] PvpWeapon layer is missing.");
                return;
            }

            gameObject.layer = pvpWeaponLayer;
            ApplyPlayerHitboxLayer(pvpWeaponLayer);
        }

        private void ApplyPlayerHitboxLayer(int pvpWeaponLayer)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform hitboxTransform = transforms[i];
                if (hitboxTransform == null || hitboxTransform == transform)
                {
                    continue;
                }

                if (!IsPlayerHitbox(hitboxTransform))
                {
                    continue;
                }

                hitboxTransform.localPosition = Vector3.zero;
                hitboxTransform.localRotation = Quaternion.identity;
                hitboxTransform.localScale = Vector3.one;

                Collider[] colliders = hitboxTransform.GetComponentsInChildren<Collider>(true);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    if (colliders[colliderIndex] == null)
                    {
                        continue;
                    }

                    colliders[colliderIndex].gameObject.layer = pvpWeaponLayer;
                }

                Debug.Log(
                    $"[PvpWeaponActorIdentity] Player hitbox normalized. Actor: {name}, Hitbox: {hitboxTransform.name}, WorldPosition: {hitboxTransform.position}");
            }
        }

        private static bool IsPlayerHitbox(Transform candidate)
        {
            return candidate.name == PlayerHitboxName || candidate.CompareTag(PlayerHitboxName);
        }

        private void LogColliderDiagnostics()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            Debug.Log(
                $"[PvpWeaponActorIdentity] Collider diagnostics. Actor: {name}, Owner/Side: {Owner}/{Side}, Count: {colliders.Length}");

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                int layer = collider.gameObject.layer;
                Debug.Log(
                    $"[PvpWeaponActorIdentity] Collider[{i}]. Actor: {name}, Collider: {collider.name}, Layer: {LayerMask.LayerToName(layer)}({layer}), IsTrigger: {collider.isTrigger}, Enabled: {collider.enabled}, BoundsCenter: {collider.bounds.center}, BoundsSize: {collider.bounds.size}");
            }
        }

        private void RenameForDebug()
        {
            string localLabel = IsLocalActor ? "Local" : "Remote";
            gameObject.name = $"PvpWeaponActor_{Side}_{Owner.PlayerId}_{localLabel}";
        }
    }
}
