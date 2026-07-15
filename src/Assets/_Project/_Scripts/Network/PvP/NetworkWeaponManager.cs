using Fusion;
using Shield_Shot.DataManagement;
using Shield_Shot.DataManagement.DataParsing;
using Shield_Shot.DataManagement.InventorySystem;
using Shield_Shot.GameplayCore.Weapon.Aim;
using Shield_Shot.GameplayCore.Weapon.Core;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using Shield_Shot.InputSystem;
using Shield_Shot.InputSystem.Data;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkProjectileFireHandler))]
    [RequireComponent(typeof(StatCalculator))]
    public sealed class NetworkWeaponManager : NetworkBehaviour
    {
        [System.Serializable]
#pragma warning disable 0649
        private struct NetworkWeaponPrefabEntry
        {
            public WeaponType Type;
            public GameObject Prefab;
        }
#pragma warning restore 0649

        [Header("Weapon Slots")]
        [SerializeField] private Transform _weaponMountPoint;
        [SerializeField] private NetworkWeaponPrefabEntry[] _weaponPrefabs;
        [SerializeField] private string _networkWeaponResourcesFolder = "Prefabs/Network";
        [SerializeField] private string _networkWeaponNameSuffix = "";
        [SerializeField] private bool _allowLocalPrefabFallback;

        [Header("Spawn Visual Scale")]
        [SerializeField, Min(0.01f)] private float _spawnedWeaponScale = 1f;

        [Header("Input")]
        [SerializeField] private PlayerInputReceiver _inputReceiver;

        [Networked] private int CurrentWeaponIndex { get; set; }
        [Networked] private int MainWeaponTypeValue { get; set; }
        [Networked] private int SubWeaponTypeValue { get; set; }
        [Networked] private NetworkString<_32> MainWeaponId { get; set; }
        [Networked] private NetworkString<_32> SubWeaponId { get; set; }
        [Networked] private float MainDamage { get; set; }
        [Networked] private float MainSpeed { get; set; }
        [Networked] private float MainCriticalMultiplier { get; set; }
        [Networked] private float MainFireRate { get; set; }
        [Networked] private float SubDamage { get; set; }
        [Networked] private float SubSpeed { get; set; }
        [Networked] private float SubCriticalMultiplier { get; set; }
        [Networked] private float SubFireRate { get; set; }
        [Networked] private NetworkBool LoadoutInitialized { get; set; }
        [Networked] private Vector3 NetworkAimDirection { get; set; }

        private const string DefaultNetworkMainWeaponPrefabName = "Bow1_1_1";
        private const WeaponType DefaultMainWeaponType = WeaponType.Bow;

        private readonly WeaponBase[] _equippedWeapons = new WeaponBase[2];
        private readonly WeaponItem[] _localWeaponData = new WeaponItem[2];
        private StatCalculator _statCalculator;
        private NetworkProjectileFireHandler _fireHandler;
        private int _appliedWeaponIndex = -1;
        private int _builtMainTypeValue = int.MinValue;
        private int _builtSubTypeValue = int.MinValue;
        private string _builtMainWeaponId = string.Empty;
        private string _builtSubWeaponId = string.Empty;
        private Vector3 _smoothedRemoteAimDirection = Vector3.right;

        public WeaponBase CurrentWeapon => _equippedWeapons[Mathf.Clamp(CurrentWeaponIndex, 0, 1)];

        private void Awake()
        {
            _statCalculator = GetComponent<StatCalculator>();
            _fireHandler = GetComponent<NetworkProjectileFireHandler>();

            if (_weaponMountPoint == null)
            {
                _weaponMountPoint = transform;
            }
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                LoadLocalWeaponData();
                SendLoadoutToStateAuthority();
            }

            RebuildWeaponsIfNeeded();
            ApplyCurrentWeapon();
        }

        private void Update()
        {
            RebuildWeaponsIfNeeded();
            ApplyCurrentWeapon();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority)
            {
                return;
            }

            Vector3 aimDirection = NormalizeAimDirection(CurrentWeapon != null ? CurrentWeapon.AimDirection : Vector3.right);
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (Object.HasStateAuthority)
            {
                NetworkAimDirection = aimDirection;
                return;
            }

            RPC_SetAimDirection(aimDirection);
        }

        private void LateUpdate()
        {
            ApplyRemoteWeaponRotation();
        }

        public void SwapWeapon()
        {
            if (!Object.HasInputAuthority)
            {
                return;
            }

            int nextIndex = CurrentWeaponIndex == 0 ? 1 : 0;

            if (_equippedWeapons[nextIndex] == null)
            {
                Debug.LogWarning("[NetworkWeaponManager] Swap target weapon is missing.");
                return;
            }

            if (Object.HasStateAuthority)
            {
                CurrentWeaponIndex = nextIndex;
                ApplyCurrentWeapon();
                return;
            }

            RPC_RequestSwapWeapon(nextIndex);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestSwapWeapon(int nextIndex)
        {
            CurrentWeaponIndex = Mathf.Clamp(nextIndex, 0, 1);
            ApplyCurrentWeapon();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetAimDirection(Vector3 aimDirection)
        {
            NetworkAimDirection = NormalizeAimDirection(aimDirection);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetLoadout(
            int mainType,
            string mainWeaponId,
            float mainDamage,
            float mainSpeed,
            float mainCriticalMultiplier,
            float mainFireRate,
            int subType,
            string subWeaponId,
            float subDamage,
            float subSpeed,
            float subCriticalMultiplier,
            float subFireRate)
        {
            ApplyLoadout(
                mainType,
                mainWeaponId,
                mainDamage,
                mainSpeed,
                mainCriticalMultiplier,
                mainFireRate,
                subType,
                subWeaponId,
                subDamage,
                subSpeed,
                subCriticalMultiplier,
                subFireRate);
        }

        private void LoadLocalWeaponData()
        {
            _localWeaponData[0] = PlayerIngameLoadData.MainWeaponItem as WeaponItem;
            _localWeaponData[1] = PlayerIngameLoadData.SubWeaponItem as WeaponItem;

            if (_localWeaponData[0] != null || _localWeaponData[1] != null)
            {
                return;
            }

            if (PlayerDataManager.Instance == null)
            {
                Debug.LogWarning("[NetworkWeaponManager] PlayerDataManager is missing.");
                return;
            }

            _localWeaponData[0] = PlayerDataManager.Instance.GetMainWeapon() as WeaponItem;
            _localWeaponData[1] = PlayerDataManager.Instance.GetSubWeapon() as WeaponItem;
        }

        private void SendLoadoutToStateAuthority()
        {
            WeaponItem main = _localWeaponData[0];
            WeaponItem sub = _localWeaponData[1];

            int mainType = ToWeaponTypeValue(main);
            int subType = ToWeaponTypeValue(sub);
            string mainWeaponId = NormalizeMainWeaponId(GetWeaponId(main));
            string subWeaponId = GetWeaponId(sub);

            if (mainType == (int)WeaponType.None)
            {
                mainType = (int)DefaultMainWeaponType;
            }

            if (Object.HasStateAuthority)
            {
                ApplyLoadout(
                    mainType,
                    mainWeaponId,
                    GetDamage(main),
                    GetSpeed(main),
                    GetCriticalMultiplier(main),
                    GetFireRate(main),
                    subType,
                    subWeaponId,
                    GetDamage(sub),
                    GetSpeed(sub),
                    GetCriticalMultiplier(sub),
                    GetFireRate(sub));
                return;
            }

            RPC_SetLoadout(
                mainType,
                mainWeaponId,
                GetDamage(main),
                GetSpeed(main),
                GetCriticalMultiplier(main),
                GetFireRate(main),
                subType,
                subWeaponId,
                GetDamage(sub),
                GetSpeed(sub),
                GetCriticalMultiplier(sub),
                GetFireRate(sub));
        }

        private void ApplyLoadout(
            int mainType,
            string mainWeaponId,
            float mainDamage,
            float mainSpeed,
            float mainCriticalMultiplier,
            float mainFireRate,
            int subType,
            string subWeaponId,
            float subDamage,
            float subSpeed,
            float subCriticalMultiplier,
            float subFireRate)
        {
            MainWeaponTypeValue = mainType;
            MainWeaponId = mainWeaponId ?? string.Empty;
            MainDamage = mainDamage;
            MainSpeed = mainSpeed;
            MainCriticalMultiplier = mainCriticalMultiplier;
            MainFireRate = mainFireRate;
            SubWeaponTypeValue = subType;
            SubWeaponId = subWeaponId ?? string.Empty;
            SubDamage = subDamage;
            SubSpeed = subSpeed;
            SubCriticalMultiplier = subCriticalMultiplier;
            SubFireRate = subFireRate;
            CurrentWeaponIndex = 0;
            LoadoutInitialized = true;
        }

        private void RebuildWeaponsIfNeeded()
        {
            if (!LoadoutInitialized)
            {
                return;
            }

            string mainWeaponId = MainWeaponId.ToString();
            string subWeaponId = SubWeaponId.ToString();

            if (_builtMainTypeValue == MainWeaponTypeValue &&
                _builtSubTypeValue == SubWeaponTypeValue &&
                _builtMainWeaponId == mainWeaponId &&
                _builtSubWeaponId == subWeaponId)
            {
                return;
            }

            ClearWeapon(0);
            ClearWeapon(1);

            _equippedWeapons[0] = CreateWeapon((WeaponType)MainWeaponTypeValue, mainWeaponId, _localWeaponData[0]);
            _equippedWeapons[1] = CreateWeapon((WeaponType)SubWeaponTypeValue, subWeaponId, _localWeaponData[1]);

            _builtMainTypeValue = MainWeaponTypeValue;
            _builtSubTypeValue = SubWeaponTypeValue;
            _builtMainWeaponId = mainWeaponId;
            _builtSubWeaponId = subWeaponId;
            _appliedWeaponIndex = -1;
        }

        private WeaponBase CreateWeapon(WeaponType type, string weaponId, WeaponItem localData)
        {
            if (type == WeaponType.None)
            {
                return null;
            }

            WeaponItem weaponData = localData ?? CreateWeaponItemFromId(weaponId);
            GameObject prefab = ResolveNetworkWeaponPrefab(type, weaponId, weaponData);
            if (prefab == null && _allowLocalPrefabFallback && localData != null && localData.WeaponData != null)
            {
                prefab = localData.WeaponData.WeaponPrefab;
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[NetworkWeaponManager] Network weapon prefab is missing for type: {type}, id: {weaponId}");
                return null;
            }

            GameObject instance = Instantiate(prefab, _weaponMountPoint);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * _spawnedWeaponScale;

            PvpWeaponActorIdentity identity = GetComponent<PvpWeaponActorIdentity>();
            string ownerText = identity != null ? $"{identity.Owner}/{identity.Side}" : "Unknown";
            Debug.Log(
                $"[NetworkWeaponManager] Created weapon prefab. " +
                $"Actor: {gameObject.name}, Owner/Side: {ownerText}, Type: {type}, Id: {weaponId}, " +
                $"Prefab: {prefab.name}, Instance: {instance.name}, " +
                $"LocalPosition: {instance.transform.localPosition}, LocalScale: {instance.transform.localScale}, " +
                $"WorldPosition: {instance.transform.position}");

            WeaponBase weapon = instance.GetComponentInChildren<WeaponBase>();
            if (weapon == null)
            {
                Debug.LogWarning($"[NetworkWeaponManager] WeaponBase is missing on prefab: {prefab.name}");
                return null;
            }

            weapon.SetProjectileFireHandler(_fireHandler);
            ApplyWeaponDataIfAvailable(instance, weapon, weaponData);
            weapon.Initialize();
            instance.SetActive(false);
            return weapon;
        }

        private void ApplyWeaponDataIfAvailable(GameObject instance, WeaponBase weapon, WeaponItem weaponData)
        {
            if (weaponData == null)
            {
                return;
            }

            StatCalculator weaponStatCalculator = instance.GetComponentInChildren<StatCalculator>();
            if (weaponStatCalculator != null)
            {
                weaponStatCalculator.ApplyWeaponData(weaponData);
            }

            weapon.ApplyWeaponData(weaponData);
        }

        private void ApplyCurrentWeapon()
        {
            int weaponIndex = Mathf.Clamp(CurrentWeaponIndex, 0, 1);
            if (_appliedWeaponIndex == weaponIndex)
            {
                return;
            }

            for (int i = 0; i < _equippedWeapons.Length; i++)
            {
                if (_equippedWeapons[i] == null)
                {
                    continue;
                }

                _equippedWeapons[i].Deactivate();
                _equippedWeapons[i].gameObject.SetActive(i == weaponIndex);
            }

            ApplyNetworkStats(weaponIndex);
            BindLocalInput(weaponIndex);
            _appliedWeaponIndex = weaponIndex;

            PvpWeaponActorIdentity identity = GetComponent<PvpWeaponActorIdentity>();
            string ownerText = identity != null ? $"{identity.Owner}/{identity.Side}" : "Unknown";
            WeaponBase currentWeapon = _equippedWeapons[weaponIndex];
            Debug.Log(
                $"[NetworkWeaponManager] Applied current weapon. " +
                $"Actor: {gameObject.name}, Owner/Side: {ownerText}, Index: {weaponIndex}, " +
                $"Weapon: {(currentWeapon != null ? currentWeapon.gameObject.name : "None")}, " +
                $"Active: {(currentWeapon != null && currentWeapon.gameObject.activeSelf)}");
        }

        private void ApplyNetworkStats(int weaponIndex)
        {
            float damage = weaponIndex == 0 ? MainDamage : SubDamage;
            float speed = weaponIndex == 0 ? MainSpeed : SubSpeed;
            float criticalMultiplier = weaponIndex == 0 ? MainCriticalMultiplier : SubCriticalMultiplier;

            if (_statCalculator != null)
            {
                _statCalculator.ApplyRawStats(damage, speed, 1f, 1f, criticalMultiplier);
            }
        }

        private void BindLocalInput(int weaponIndex)
        {
            if (!Object.HasInputAuthority)
            {
                return;
            }

            WeaponBase weapon = _equippedWeapons[weaponIndex];
            if (weapon == null)
            {
                return;
            }

            if (_inputReceiver == null)
            {
                _inputReceiver = FindFirstObjectByType<PlayerInputReceiver>();
            }

            if (_inputReceiver != null)
            {
                _inputReceiver.SetCurrentWeapon(weapon);
            }
        }

        private void ApplyRemoteWeaponRotation()
        {
            if (Object == null || Object.HasInputAuthority)
            {
                return;
            }

            WeaponBase weapon = CurrentWeapon;
            if (weapon == null)
            {
                return;
            }

            Vector3 targetDirection = NormalizeAimDirection(NetworkAimDirection);
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            WeaponRotationController rotationController = weapon.GetComponent<WeaponRotationController>();
            if (rotationController == null)
            {
                return;
            }

            _smoothedRemoteAimDirection = Vector3.Slerp(
                NormalizeAimDirection(_smoothedRemoteAimDirection),
                targetDirection,
                Time.deltaTime * 20f);

            rotationController.SyncRotation(_smoothedRemoteAimDirection);
        }

        private GameObject ResolveNetworkWeaponPrefab(WeaponType type, string weaponId, WeaponItem weaponData)
        {
            GameObject prefab = ResolveNetworkWeaponPrefabFromResources(type, weaponId, weaponData);
            if (prefab != null)
            {
                return prefab;
            }

            if (type == WeaponType.None || _weaponPrefabs == null)
            {
                return null;
            }

            for (int i = 0; i < _weaponPrefabs.Length; i++)
            {
                if (_weaponPrefabs[i].Type == type)
                {
                    return _weaponPrefabs[i].Prefab;
                }
            }

            return null;
        }

        private GameObject ResolveNetworkWeaponPrefabFromResources(WeaponType type, string weaponId, WeaponItem weaponData)
        {
            if (string.IsNullOrWhiteSpace(_networkWeaponResourcesFolder))
            {
                return null;
            }

            string localPrefabName = weaponData != null &&
                weaponData.WeaponData != null &&
                weaponData.WeaponData.WeaponPrefab != null
                    ? weaponData.WeaponData.WeaponPrefab.name
                    : weaponId;

            if (string.IsNullOrWhiteSpace(localPrefabName))
            {
                return null;
            }

            string networkPrefabName = $"{localPrefabName}{_networkWeaponNameSuffix}";
            string resourcePath = $"{_networkWeaponResourcesFolder.TrimEnd('/')}/{networkPrefabName}";
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
            {
                return prefab;
            }

            if (type == DefaultMainWeaponType &&
                !string.Equals(networkPrefabName, DefaultNetworkMainWeaponPrefabName, System.StringComparison.Ordinal))
            {
                string fallbackPath = $"{_networkWeaponResourcesFolder.TrimEnd('/')}/{DefaultNetworkMainWeaponPrefabName}";
                return Resources.Load<GameObject>(fallbackPath);
            }

            return null;
        }

        private void ClearWeapon(int index)
        {
            if (_equippedWeapons[index] == null)
            {
                return;
            }

            Destroy(_equippedWeapons[index].gameObject);
            _equippedWeapons[index] = null;
        }

        private static int ToWeaponTypeValue(WeaponItem item)
        {
            if (item == null || item.WeaponData == null)
            {
                return (int)WeaponType.None;
            }

            WeaponType type = item.WeaponData.weaponType;
            if (type == WeaponType.None)
            {
                type = item.WeaponData.Type;
            }

            return (int)type;
        }

        private static string GetWeaponId(WeaponItem item)
        {
            return item != null && item.WeaponData != null
                ? item.WeaponData.ItemID
                : string.Empty;
        }

        private static string NormalizeMainWeaponId(string weaponId)
        {
            return !string.IsNullOrWhiteSpace(weaponId)
                ? weaponId
                : DefaultNetworkMainWeaponPrefabName;
        }

        private static WeaponItem CreateWeaponItemFromId(string weaponId)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                return null;
            }

            if (ItemDataParsingManager.Instance == null)
            {
                Debug.LogWarning($"[NetworkWeaponManager] ItemDataParsingManager is missing. WeaponId: {weaponId}");
                return null;
            }

            WeaponItemData weaponData = ItemDataParsingManager.Instance.GetWeaponData(weaponId);
            return weaponData != null ? new WeaponItem(weaponData) : null;
        }

        private static float GetDamage(WeaponItem item) => item != null ? item.FinalDamage : 10f;
        private static float GetSpeed(WeaponItem item) => item != null ? item.FinalSpeed : 15f;
        private static float GetCriticalMultiplier(WeaponItem item) => item != null ? item.FinalCriticalDamageMultiplier : 2f;
        private static float GetFireRate(WeaponItem item) => item != null ? item.FinalFireRate : 0.5f;

        private static Vector3 NormalizeAimDirection(Vector3 direction)
        {
            direction.z = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.right;
            }

            return direction.normalized;
        }
    }
}
