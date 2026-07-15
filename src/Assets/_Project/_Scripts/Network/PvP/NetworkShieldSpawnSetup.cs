using Fusion;
using Shield_Shot.DataManagement;
using Shield_Shot.DataManagement.DataParsing;
using Shield_Shot.DataManagement.InventorySystem;
using Shield_Shot.GameplayCore.Weapon.Shield;
using Shield_Shot.InputSystem;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkShieldActor))]
    public sealed class NetworkShieldSpawnSetup : NetworkBehaviour
    {
        [Header("Shield Resources")]
        [SerializeField] private string _networkShieldResourcesFolder = "Prefabs/Network";
        [SerializeField] private string _networkShieldNameSuffix = "";
        [SerializeField] private bool _allowLocalPrefabFallback;

        private const string DefaultNetworkShieldPrefabName = "Shield1";

        [Header("Mount Points")]
        [Tooltip("Initial shield spawn point, for example ShieldPoint.")]
        [SerializeField] private Transform _shieldMountPoint;

        [Tooltip("Shield orbit center. Usually the WeaponCore_Network root.")]
        [SerializeField] private Transform _playerOrbitCenter;

        [Header("Spawn Visual Scale")]
        [SerializeField, Min(0.01f)] private float _spawnedShieldScale = 0.75f;

        [Networked] private NetworkString<_32> ShieldId { get; set; }
        [Networked] private NetworkBool ShieldLoadoutInitialized { get; set; }

        private NetworkShieldActor _networkShieldActor;
        private ShieldItem _localShieldData;
        private GameObject _spawnedShield;
        private string _builtShieldId = string.Empty;

        private void Awake()
        {
            _networkShieldActor = GetComponent<NetworkShieldActor>();
        }

        public override void Spawned()
        {
            Debug.Log($"[ShieldSpawnSetup] Spawned\n" +
                      $"  HasInputAuthority  = {Object.HasInputAuthority}\n" +
                      $"  HasStateAuthority  = {Object.HasStateAuthority}\n" +
                      $"  LocalPlayer        = {Runner.LocalPlayer}\n" +
                      $"  MountPoint         = {(_shieldMountPoint != null ? _shieldMountPoint.name : "NULL!")}");

            if (_shieldMountPoint == null)
            {
                Debug.LogError("[ShieldSpawnSetup] Shield Mount Point is missing.");
                return;
            }

            if (_playerOrbitCenter == null)
            {
                Debug.LogWarning("[ShieldSpawnSetup] Player Orbit Center is missing. Using root transform.");
                _playerOrbitCenter = transform;
            }

            if (Object.HasInputAuthority)
            {
                LoadLocalShieldData();
                SendShieldLoadoutToStateAuthority();
            }

            RebuildShieldIfNeeded();
        }

        private void Update()
        {
            RebuildShieldIfNeeded();
        }

        private void LoadLocalShieldData()
        {
            _localShieldData = PlayerIngameLoadData.ShieldItem as ShieldItem;
            if (_localShieldData != null)
            {
                return;
            }

            if (PlayerDataManager.Instance == null)
            {
                Debug.LogWarning("[ShieldSpawnSetup] PlayerDataManager is missing.");
                return;
            }

            _localShieldData = PlayerDataManager.Instance.GetShield() as ShieldItem;
        }

        private void SendShieldLoadoutToStateAuthority()
        {
            string shieldId = GetShieldId(_localShieldData);

            if (Object.HasStateAuthority)
            {
                ApplyShieldLoadout(shieldId);
                return;
            }

            RPC_SetShieldLoadout(shieldId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetShieldLoadout(string shieldId)
        {
            ApplyShieldLoadout(shieldId);
        }

        private void ApplyShieldLoadout(string shieldId)
        {
            ShieldId = shieldId ?? string.Empty;
            ShieldLoadoutInitialized = true;
        }

        private void RebuildShieldIfNeeded()
        {
            if (!ShieldLoadoutInitialized)
            {
                return;
            }

            string shieldId = NormalizeShieldId(ShieldId.ToString());
            if (_spawnedShield != null && _builtShieldId == shieldId)
            {
                return;
            }

            ClearShield();
            if (SpawnShield(shieldId))
            {
                _builtShieldId = shieldId;
            }
        }

        private bool SpawnShield(string shieldId)
        {
            shieldId = NormalizeShieldId(shieldId);
            ShieldItem shieldItem = _localShieldData ?? CreateShieldItemFromId(shieldId);
            ShieldItemData shieldData = shieldItem?.ShieldData;
            GameObject prefab = ResolveNetworkShieldPrefab(shieldId, shieldData);
            if (prefab == null && _allowLocalPrefabFallback && shieldData != null)
            {
                prefab = shieldData.ShieldPrefab;
            }

            if (prefab == null)
            {
                Debug.LogError($"[ShieldSpawnSetup] Shield prefab missing. ShieldId: {shieldId}");
                return false;
            }

            // Keep the shield at scene root; ShieldOrbitController updates world position from orbit center.
            GameObject shieldObj = Instantiate(
                prefab,
                _shieldMountPoint.position,
                Quaternion.identity,
                null);
            shieldObj.transform.localScale = Vector3.one * _spawnedShieldScale;
            shieldObj.SetActive(true);
            ApplyLayerRecursively(shieldObj.transform, ResolvePvpShieldLayer());
            _spawnedShield = shieldObj;

            var skillShield = shieldObj.GetComponentInChildren<SkillShield>(true);
            var orbitCtrl = shieldObj.GetComponentInChildren<ShieldOrbitController>(true);
            var netDetector = shieldObj.GetComponentInChildren<NetworkShieldColliderDetector>(true);
            var binder = shieldObj.GetComponentInChildren<ShieldSystemBinder>(true);

            if (skillShield == null)
            {
                Debug.LogError("[ShieldSpawnSetup] SkillShield is missing.");
                ClearShield();
                return false;
            }

            if (netDetector != null)
            {
                netDetector.InjectNetworkShieldActor(_networkShieldActor);
            }

            bool enableCollider = Object.HasStateAuthority || Object.HasInputAuthority;
            foreach (var col in shieldObj.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = enableCollider;
            }

            if (orbitCtrl != null)
            {
                orbitCtrl.SetupOrbitCenter(_playerOrbitCenter);
            }

            if (binder != null)
            {
                binder.InitializeBinding(null, orbitCtrl);
            }

            skillShield.ApplyShieldData(shieldData, _playerOrbitCenter, null);
            skillShield.Initialize();

            if (Object.HasInputAuthority)
            {
                var inputReceiver = FindFirstObjectByType<PlayerInputReceiver>();
                if (inputReceiver != null)
                {
                    inputReceiver.SetCurrentShield(skillShield);
                }
                else
                {
                    Debug.LogWarning("[ShieldSpawnSetup] PlayerInputReceiver is missing.");
                }

                _networkShieldActor?.InjectShieldReferences(skillShield, orbitCtrl);
                Debug.Log("[ShieldSpawnSetup] Local shield initialized.");
            }
            else
            {
                _networkShieldActor?.InjectRemoteShieldReferences(orbitCtrl);
                Debug.Log("[ShieldSpawnSetup] Remote shield visual initialized.");
            }

            return true;
        }

        private GameObject ResolveNetworkShieldPrefab(string shieldId, ShieldItemData shieldData)
        {
            if (string.IsNullOrWhiteSpace(_networkShieldResourcesFolder))
            {
                return null;
            }

            string localPrefabName = shieldData != null && shieldData.ShieldPrefab != null
                ? shieldData.ShieldPrefab.name
                : shieldId;

            if (string.IsNullOrWhiteSpace(localPrefabName))
            {
                localPrefabName = DefaultNetworkShieldPrefabName;
            }

            string networkPrefabName = $"{localPrefabName}{_networkShieldNameSuffix}";
            string resourcePath = $"{_networkShieldResourcesFolder.TrimEnd('/')}/{networkPrefabName}";
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
            {
                return prefab;
            }

            if (!string.Equals(networkPrefabName, DefaultNetworkShieldPrefabName, System.StringComparison.Ordinal))
            {
                string fallbackPath = $"{_networkShieldResourcesFolder.TrimEnd('/')}/{DefaultNetworkShieldPrefabName}";
                return Resources.Load<GameObject>(fallbackPath);
            }

            return null;
        }

        private void ClearShield()
        {
            if (_spawnedShield == null)
            {
                return;
            }

            Destroy(_spawnedShield);
            _spawnedShield = null;
        }

        private static string GetShieldId(ShieldItem item)
        {
            return item != null && item.ShieldData != null
                ? item.ShieldData.ItemID
                : string.Empty;
        }

        private string NormalizeShieldId(string shieldId)
        {
            return !string.IsNullOrWhiteSpace(shieldId)
                ? shieldId
                : DefaultNetworkShieldPrefabName;
        }

        private static ShieldItem CreateShieldItemFromId(string shieldId)
        {
            if (string.IsNullOrWhiteSpace(shieldId))
            {
                return null;
            }

            if (ItemDataParsingManager.Instance == null)
            {
                Debug.LogWarning($"[ShieldSpawnSetup] ItemDataParsingManager is missing. ShieldId: {shieldId}");
                return null;
            }

            ShieldItemData shieldData = ItemDataParsingManager.Instance.GetShieldData(shieldId);
            return shieldData != null ? new ShieldItem(shieldData) : null;
        }

        private static int ResolvePvpShieldLayer()
        {
            int layer = LayerMask.NameToLayer("PvpShield");
            if (layer >= 0)
            {
                return layer;
            }

            layer = LayerMask.NameToLayer("pvpshield");
            if (layer >= 0)
            {
                return layer;
            }

            Debug.LogWarning("[ShieldSpawnSetup] PvpShield layer is missing.");
            return -1;
        }

        private static void ApplyLayerRecursively(Transform target, int layer)
        {
            if (target == null || layer < 0)
            {
                return;
            }

            target.gameObject.layer = layer;
            for (int i = 0; i < target.childCount; i++)
            {
                ApplyLayerRecursively(target.GetChild(i), layer);
            }
        }
    }
}
