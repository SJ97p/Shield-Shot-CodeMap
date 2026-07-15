using Shield_Shot.GameplayCore.Weapon.Core;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Weapon.Aim
{
    [RequireComponent(typeof(LineRenderer))]
    public class AimLineRenderer : MonoBehaviour
    {
        #region Inspector
        [Header("Distance Settings")]
        [Tooltip("차징 0% 일 때 조준선 최소 거리")]
        [SerializeField] private float minLineDistance = 0.1f;

        [Tooltip("차징 100% 일 때 조준선 최대 거리")]
        [SerializeField] private float maxLineDistance = 99f;

        [Header("Collision Settings")]
        [Tooltip("기본 벽 반사 횟수 (특성이 없을 때의 기초값)")]
        [SerializeField] private int defaultBounceCount = 0;

        [Tooltip("벽으로 인식할 레이어 마스크")]
        [SerializeField] private LayerMask wallLayerMask;

        [Header("Player Status Link")]
        [Tooltip("플레이어 리스트에서 찾을 반사 특성 SO 에셋")]
        [SerializeField] private ProjectileBehaviorSO reflectBehaviorSO;

        [Tooltip("반사 특성 1레벨당 추가될 반사 횟수")]
        [SerializeField] private int bounceCountPerLevel = 1;

        [Header("Line Shape")]
        [SerializeField, Min(0)] private int cornerVertices = 6;
        [SerializeField, Min(0)] private int capVertices = 4;
        [SerializeField, Min(0f)] private float bounceCornerInset = 0.06f;

        [Header("Collision Prediction")]
        [SerializeField] private bool useProjectileCollisionRadius = true;
        [SerializeField, Min(0f)] private float fallbackCastRadius;
        [SerializeField, Min(0.01f)] private float castRadiusMultiplier = 1f;
        [SerializeField, Min(0f)] private float maxPredictionCastRadius = 0.18f;
        #endregion

        #region 내부 참조
        private LineRenderer lineRenderer;
        private WeaponBase currentWeapon;

        public float MaxLineDistance => maxLineDistance;
        public LayerMask WallLayerMask => wallLayerMask;
        public int DefaultBounceCount => defaultBounceCount;
        #endregion

        #region Unity 생명주기
        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;
            EnsureWallLayerMask();
        }
        #endregion

        #region 공개 메서드

        // 조준선 활성화
        public void Show(WeaponBase activeWeapon)
        {
            if (activeWeapon == null || lineRenderer == null) return;

            if (currentWeapon == activeWeapon && lineRenderer.enabled) return;

            // 무기가 들고 있는 고유 비주얼 데이터를 통째로 가로채서 라인 렌더러에 주입
            currentWeapon = activeWeapon;
            WeaponAimVisualData visual = activeWeapon.AimVisualData;

            lineRenderer.material = visual.LineMaterial;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = visual.StartWidth;
            lineRenderer.endWidth = visual.EndWidth;
            lineRenderer.textureMode = visual.TextureMode;
            lineRenderer.numCornerVertices = cornerVertices;
            lineRenderer.numCapVertices = capVertices;

            // 유니티 내부 셰이더 프로퍼티 명칭(_MainTex)에 타일링 스케일 적용
            if (lineRenderer.material != null && lineRenderer.material.HasProperty("_MainTex"))
            {
                lineRenderer.material.SetTextureScale("_MainTex", visual.TextureScale);
            }

            lineRenderer.enabled = true;
        }

        // 조준선 비활성화
        public void Hide()
        {
            currentWeapon = null;
            if (lineRenderer != null) lineRenderer.enabled = false;
        }

        // 조준선 경로를 갱신
        public void UpdateLine(Vector3 aimDirection, float chargeRatio, Vector3 origin)
        {
            // 스크린 XY → 월드 XZ 변환
            Vector3 worldDir = AimDirectionUtility.ToWorldXZ(aimDirection);
            if (worldDir == Vector3.zero) return;

            int currentMaxBounce = CalculateDynamicBounceCount();
            float castRadius = ResolveCastRadius();
            Vector3 predictionOrigin = ResolvePredictionOrigin(origin, worldDir);

            float totalDistance = Mathf.Lerp(minLineDistance, maxLineDistance, chargeRatio);
            var points = CalculateBouncePoints(predictionOrigin, worldDir, totalDistance, currentMaxBounce, castRadius);

            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
        }

        private Vector3 ResolvePredictionOrigin(Vector3 firePointOrigin, Vector3 worldDirection)
        {
            if (currentWeapon != null &&
                currentWeapon.ProjectileFireHandler is IProjectileAimPredictionProvider predictionProvider)
            {
                return predictionProvider.GetPredictedProjectileOrigin(firePointOrigin, worldDirection);
            }

            return firePointOrigin;
        }
        #endregion

        #region 내부 메서드
        private int CalculateDynamicBounceCount()
        {
            // 방어 코드: 기준이 될 SO가 인스펙터에 할당 안 되었거나 싱글톤이 비어있으면 기본값 반환
            if (!TryGetPlayerStatus(out PlayerStatus playerStatus))
                return defaultBounceCount;

            // PlayerStatus의 CurrentBehaviors 리스트에서 ID가 일치하는 특성이 있는지 매칭 시도
            int index = playerStatus.CurrentBehaviors.FindIndex(IsReflectBehavior);

            if (index >= 0)
            {
                // 특성을 보유 중이라면: 기본 반사 횟수 + (특성 레벨 * 레벨당 증가량)
                int reflectLevel = playerStatus.CurrentBehaviors[index].Level;
                return defaultBounceCount + (reflectLevel * bounceCountPerLevel);
            }

            // 특성이 리스트에 없다면 0번 혹은 세팅된 기본값 반환
            return defaultBounceCount;
        }

        private float ResolveCastRadius()
        {
            float radius = fallbackCastRadius;

            if (useProjectileCollisionRadius &&
                currentWeapon != null &&
                currentWeapon.ProjectileFireHandler is IProjectileAimPredictionProvider predictionProvider &&
                predictionProvider.TryGetProjectileCollisionRadius(currentWeapon.Type, out float predictedRadius))
            {
                radius = predictedRadius;
                return ClampPredictionCastRadius(radius * castRadiusMultiplier);
            }

            if (useProjectileCollisionRadius &&
                currentWeapon != null &&
                ProjectileManager.Instance != null &&
                ProjectileManager.Instance.TryGetProjectileCollisionRadius(currentWeapon.Type, out float projectileRadius))
            {
                radius = projectileRadius;
                return ClampPredictionCastRadius(radius * castRadiusMultiplier);
            }

            return ClampPredictionCastRadius(radius);
        }

        private float ClampPredictionCastRadius(float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            return maxPredictionCastRadius > 0f
                ? Mathf.Min(safeRadius, maxPredictionCastRadius)
                : safeRadius;
        }

        // 인자값 맨 끝에 bounceCount를 동적으로 받도록 수정
        private bool TryGetPlayerStatus(out PlayerStatus playerStatus)
        {
            playerStatus = null;

            if (currentWeapon != null && currentWeapon.TryGetComponent(out playerStatus))
            {
                return true;
            }

            return LocalPlayerStatusContext.TryGet(out playerStatus);
        }

        private bool IsReflectBehavior(ActiveBehavior behavior)
        {
            ProjectileBehaviorSO behaviorSO = behavior.BehaviorSO;
            if (behaviorSO == null)
            {
                return false;
            }

            if (reflectBehaviorSO != null &&
                string.Equals(behaviorSO.BehaviorID, reflectBehaviorSO.BehaviorID, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string id = string.IsNullOrWhiteSpace(behaviorSO.BehaviorID)
                ? string.Empty
                : behaviorSO.BehaviorID.Trim();
            string typeName = behaviorSO.GetType().Name;

            return id == "1" ||
                   string.Equals(id, "StandardReflect", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "RandomReflect", System.StringComparison.OrdinalIgnoreCase) ||
                   typeName.IndexOf("StandardReflect", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("RandomReflect", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Vector3[] CalculateBouncePoints(
            Vector3 origin,
            Vector3 direction,
            float remainingDistance,
            int bounceCount,
            float castRadius)
        {
            var points = new System.Collections.Generic.List<Vector3> { origin };
            var curOrigin = origin;
            var curDir = direction;
            int remainingBounceCount = Mathf.Max(0, bounceCount);
            bool stoppedByWall = false;

            // 고정된 maxBounceCount 대신 매개변수로 들어온 동적 bounceCount를 루프 한계로 설정함
            while (remainingDistance > 0f)
            {
                if (TryCastPrediction(curOrigin, curDir, remainingDistance, castRadius, out RaycastHit hit))
                {
                    Vector3 incomingDir = curDir;
                    remainingDistance -= Mathf.Max(hit.distance, 0.001f);
                    Vector3 bouncePoint = GetBouncePoint(hit, castRadius);

                    if (remainingBounceCount <= 0)
                    {
                        points.Add(bouncePoint);
                        stoppedByWall = true;
                        break;
                    }

                    remainingBounceCount--;
                    curDir = Vector3.Reflect(incomingDir, hit.normal);

                    curDir.y = 0f;
                    curDir = curDir.normalized;
                    AddBounceCornerPoints(points, bouncePoint, incomingDir, curDir);
                    curOrigin = bouncePoint + curDir * 0.01f;
                }
                else
                {
                    // 레이캐스트가 벽에 닿지 않고 공간이 남았을 때 직선 처리 후 탈출
                    points.Add(curOrigin + curDir * remainingDistance);
                    return points.ToArray();
                }
            }

            // 만약 반사 횟수를 다 채웠는데도 남은 거리가 있다면 마지막 튕긴 지점에서 남은 거리만큼 최종 연장선 추가
            if (!stoppedByWall && remainingDistance > 0f && points.Count > 0)
            {
                points.Add(curOrigin + curDir * remainingDistance);
            }

            return points.ToArray();
        }

        private bool TryCastPrediction(
            Vector3 origin,
            Vector3 direction,
            float distance,
            float castRadius,
            out RaycastHit hit)
        {
            if (castRadius > 0f)
            {
                return Physics.SphereCast(origin, castRadius, direction, out hit, distance, wallLayerMask);
            }

            return Physics.Raycast(origin, direction, out hit, distance, wallLayerMask);
        }

        private static Vector3 GetBouncePoint(RaycastHit hit, float castRadius)
        {
            if (castRadius <= 0f || hit.normal.sqrMagnitude <= 0.0001f)
            {
                return hit.point;
            }

            return hit.point + hit.normal.normalized * castRadius;
        }

        private void EnsureWallLayerMask()
        {
            IncludeWallLayer("Wall");
            IncludeWallLayer("PvpWall");
        }

        private void IncludeWallLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return;
            }

            wallLayerMask |= 1 << layer;
        }

        private void AddBounceCornerPoints(System.Collections.Generic.List<Vector3> points, Vector3 hitPoint, Vector3 incomingDir, Vector3 reflectedDir)
        {
            float inset = Mathf.Max(0f, bounceCornerInset);

            if (inset <= 0f)
            {
                points.Add(hitPoint);
                return;
            }

            Vector3 beforeCorner = hitPoint - incomingDir.normalized * inset;
            Vector3 afterCorner = hitPoint + reflectedDir.normalized * inset;

            if (points.Count > 0 && Vector3.Distance(points[^1], beforeCorner) > 0.001f)
            {
                points.Add(beforeCorner);
            }

            points.Add(hitPoint);

            if (Vector3.Distance(hitPoint, afterCorner) > 0.001f)
            {
                points.Add(afterCorner);
            }
        }

        #endregion
    }
}
