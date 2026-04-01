using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    #region 1. SUBTRACT MANAGER & DATA STRUCTURES

    /// <summary>
    /// Manages centralized requests for terrain and network cleanup operations.
    /// </summary>
    public static class SubtractManager
    {
        /// <summary>
        /// Defines the boundaries, transformations, and mathematical limits for a subtraction operation.
        /// </summary>
        public struct SubtractRequest
        {
            public float4x4 invMatrix;
            public float3 center;

            public float outerA;
            public float outerB;

            public float innerA;
            public float innerB;

            public float n;
            public int framesLeft;
        }

        public static SubtractRequest CurrentRequest;

        /// <summary>
        /// Queues a new subtraction request with the specified spatial boundaries and rotation.
        /// </summary>
        public static void Request(float3 center, float rotation, float outerA, float outerB, float innerA, float innerB, float nValue)
        {
            float4x4 toolMatrix = float4x4.TRS(
                center,
                quaternion.Euler(0f, rotation, 0f),
                new float3(1f, 1f, 1f)
            );

            CurrentRequest = new SubtractRequest
            {
                invMatrix = math.inverse(toolMatrix),
                center = center,
                outerA = outerA,
                outerB = outerB,
                innerA = innerA,
                innerB = innerB,
                n = nValue,
                framesLeft = 1
            };
        }
    }

    #endregion

    public partial class MertsSubtractCleanupSystem : GameSystemBase
    {
        #region 2. CONSTANTS & THRESHOLDS

        private const float OuterBandTolerance = 0.20f;
        private const float OuterRejectThreshold = 1.05f;

        private const float AreaPaddingMain = 64f;
        private const float AreaPaddingCurveOnly = 20f;

        private const float CurveOnlyMaxDistanceToCenter = 11f;
        private const float CurveOnlyMaxLength = 17f;
        private const int MaxCurveOnlyDeletes = 24;

        #endregion

        #region 3. QUERIES & STATE

        private EntityQuery m_EdgeCurveQuery;
        private EntityQuery m_CurveOnlyQuery;

        #endregion

        #region 4. ECS LIFECYCLE

        /// <summary>
        /// Initializes the required entity queries for edge curves and leftover curve-only entities.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            m_EdgeCurveQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Curve>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            m_CurveOnlyQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Curve>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Deleted>()
                }
            });
        }

        /// <summary>
        /// Executes the subtraction logic over active requests, evaluating and deleting entities that fall within the subtraction core.
        /// </summary>
        protected override void OnUpdate()
        {
            if (SubtractManager.CurrentRequest.framesLeft <= 0)
                return;

            var req = SubtractManager.CurrentRequest;
            SubtractManager.CurrentRequest.framesLeft--;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var edgeEntities = m_EdgeCurveQuery.ToEntityArray(Allocator.Temp);
            using var curveOnlyEntities = m_CurveOnlyQuery.ToEntityArray(Allocator.Temp);

            int scannedCount = 0;
            int protectedRingCount = 0;
            int deletedEdgeCount = 0;
            int deletedCurveOnlyCount = 0;

            foreach (var entity in edgeEntities)
            {
                Curve curve = EntityManager.GetComponentData<Curve>(entity);
                scannedCount++;

                if (!IsNearSubtractArea(curve, req, AreaPaddingMain))
                    continue;

                if (LooksLikeOuterRing(curve, req))
                {
                    protectedRingCount++;
                    continue;
                }

                if (ShouldDeleteMainCurve(curve, req))
                {
                    ecb.AddComponent<Deleted>(entity);
                    ecb.AddComponent<Updated>(entity);
                    deletedEdgeCount++;
                }
            }

            foreach (var entity in curveOnlyEntities)
            {
                if (deletedCurveOnlyCount >= MaxCurveOnlyDeletes)
                    break;

                Curve curve = EntityManager.GetComponentData<Curve>(entity);

                if (!IsNearSubtractArea(curve, req, AreaPaddingCurveOnly))
                    continue;

                if (ShouldDeleteCurveOnly(curve, req))
                {
                    ecb.AddComponent<Deleted>(entity);
                    ecb.AddComponent<Updated>(entity);
                    deletedCurveOnlyCount++;
                }
            }

            ecb.Playback(EntityManager);
        }

        #endregion

        #region 5. MATHEMATICAL EVALUATORS

        /// <summary>
        /// Calculates the mathematical value of a point relative to a super-ellipse equation.
        /// </summary>
        private static float SuperEllipseValue(float3 localPoint, float a, float b, float n)
        {
            float safeA = math.max(a, 0.001f);
            float safeB = math.max(b, 0.001f);
            float safeN = math.max(n, 0.1f);

            float x = math.abs(localPoint.x) / safeA;
            float z = math.abs(localPoint.z) / safeB;

            return math.pow(x, safeN) + math.pow(z, safeN);
        }

        /// <summary>
        /// Transforms a world point to local space and computes its outer boundary value.
        /// </summary>
        private static float GetOuterValue(float3 worldPoint, in SubtractManager.SubtractRequest req)
        {
            float3 local = math.transform(req.invMatrix, worldPoint);
            return SuperEllipseValue(local, req.outerA, req.outerB, req.n);
        }

        /// <summary>
        /// Determines if a specific world point falls strictly inside the inner deletion core.
        /// </summary>
        private static bool IsInsideInner(float3 worldPoint, in SubtractManager.SubtractRequest req)
        {
            float3 local = math.transform(req.invMatrix, worldPoint);
            return SuperEllipseValue(local, req.innerA, req.innerB, req.n) < 1f;
        }

        /// <summary>
        /// Performs a fast distance check to see if a curve is near the general subtraction area.
        /// </summary>
        private static bool IsNearSubtractArea(Curve curve, in SubtractManager.SubtractRequest req, float padding)
        {
            float3 mid = MathUtils.Position(curve.m_Bezier, 0.5f);
            float2 delta = new(mid.x - req.center.x, mid.z - req.center.z);

            float maxReach = math.max(req.outerA, req.outerB) + padding;
            return math.lengthsq(delta) <= (maxReach * maxReach);
        }

        /// <summary>
        /// Approximates the physical length of a bezier curve.
        /// </summary>
        private static float ApproxCurveLength(Curve curve)
        {
            float3 a = curve.m_Bezier.a;
            float3 m = MathUtils.Position(curve.m_Bezier, 0.5f);
            float3 d = curve.m_Bezier.d;

            return math.distance(a, m) + math.distance(m, d);
        }

        #endregion

        #region 6. DELETION LOGIC

        /// <summary>
        /// Evaluates if a curve belongs to the protective outer ring, checking if its endpoints and midpoint reside strictly on the boundary band.
        /// </summary>
        private static bool LooksLikeOuterRing(Curve curve, in SubtractManager.SubtractRequest req)
        {
            float3 a = curve.m_Bezier.a;
            float3 m = MathUtils.Position(curve.m_Bezier, 0.5f);
            float3 d = curve.m_Bezier.d;

            float outerA = GetOuterValue(a, req);
            float outerM = GetOuterValue(m, req);
            float outerD = GetOuterValue(d, req);

            bool aOnBand = math.abs(outerA - 1f) <= OuterBandTolerance;
            bool mOnBand = math.abs(outerM - 1f) <= OuterBandTolerance;
            bool dOnBand = math.abs(outerD - 1f) <= OuterBandTolerance;

            return aOnBand && mOnBand && dOnBand;
        }

        /// <summary>
        /// Determines if a primary edge curve should be deleted based on inner bounds hits or long crossing intersections.
        /// </summary>
        private static bool ShouldDeleteMainCurve(Curve curve, in SubtractManager.SubtractRequest req)
        {
            float3 a = curve.m_Bezier.a;
            float3 m = MathUtils.Position(curve.m_Bezier, 0.5f);
            float3 d = curve.m_Bezier.d;

            bool aInside = IsInsideInner(a, req);
            bool mInside = IsInsideInner(m, req);
            bool dInside = IsInsideInner(d, req);

            int innerHits = 0;
            if (aInside) innerHits++;
            if (mInside) innerHits++;
            if (dInside) innerHits++;

            float outerA = GetOuterValue(a, req);
            float outerM = GetOuterValue(m, req);
            float outerD = GetOuterValue(d, req);

            int outsideHits = 0;
            if (outerA > OuterRejectThreshold) outsideHits++;
            if (outerM > OuterRejectThreshold) outsideHits++;
            if (outerD > OuterRejectThreshold) outsideHits++;

            if (innerHits >= 2 && outsideHits <= 1)
                return true;

            float len = ApproxCurveLength(curve);
            bool longCrossingEdge = mInside && !aInside && !dInside && len > 24f;

            if (longCrossingEdge)
                return true;

            return false;
        }

        /// <summary>
        /// Evaluates if an orphaned curve-only entity should be cleaned up based on its distance to the center, length, and bounds.
        /// </summary>
        private static bool ShouldDeleteCurveOnly(Curve curve, in SubtractManager.SubtractRequest req)
        {
            float3 a = curve.m_Bezier.a;
            float3 m = MathUtils.Position(curve.m_Bezier, 0.5f);
            float3 d = curve.m_Bezier.d;

            float2 toCenter = new(m.x - req.center.x, m.z - req.center.z);
            float distToCenter = math.length(toCenter);

            if (distToCenter > CurveOnlyMaxDistanceToCenter)
                return false;

            float len = ApproxCurveLength(curve);
            if (len > CurveOnlyMaxLength)
                return false;

            int innerHits = 0;
            if (IsInsideInner(a, req)) innerHits++;
            if (IsInsideInner(m, req)) innerHits++;
            if (IsInsideInner(d, req)) innerHits++;

            if (innerHits != 3)
                return false;

            float outerA = GetOuterValue(a, req);
            float outerM = GetOuterValue(m, req);
            float outerD = GetOuterValue(d, req);

            if (outerA > OuterRejectThreshold) return false;
            if (outerM > OuterRejectThreshold) return false;
            if (outerD > OuterRejectThreshold) return false;

            return true;
        }

        #endregion
    }
}