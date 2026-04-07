using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region Cost Calculation & Metadata
        /// <summary>
        /// Calculates and applies the construction and upkeep cost metadata to the given stamp based on road composition and elevation.
        /// </summary>
        protected void ApplyCostMetadata(AssetStampPrefab stamp, ObjectSubNetInfo[] subNets, NetPrefab roadPrefab, float costElevation)
        {
            if (stamp == null || subNets == null || roadPrefab == null) return;
            if (TryGetBestMatchingCompositionEntity(roadPrefab, costElevation, out Entity compositionEntity) &&
                EntityManager.TryGetComponent(compositionEntity, out Game.Prefabs.PlaceableNetComposition comp))
            {
                float totalLength = CalculateTotalLength(subNets);
                float absElevation = math.abs(costElevation);
                int cells = math.max(1, Mathf.RoundToInt(totalLength / 8f));
                int elevationSteps = math.max(0, Mathf.RoundToInt(absElevation / 10f));

                uint finalConstruction = (uint)math.max(100, cells * ((int)comp.m_ConstructionCost + elevationSteps * (int)comp.m_ElevationCost));
                uint finalUpkeep = (uint)math.max(10, cells * (int)comp.m_UpkeepCost);

                if (stamp.m_ConstructionCost != finalConstruction || stamp.m_UpKeepCost != finalUpkeep)
                {
                    stamp.m_ConstructionCost = finalConstruction;
                    stamp.m_UpKeepCost = finalUpkeep;
                    stamp.asset?.MarkDirty();
                }
            }
            else
            {
                uint fallbackCost = (uint)math.max(100, (subNets.Length * 100));
                if (stamp.m_ConstructionCost != fallbackCost)
                {
                    stamp.m_ConstructionCost = fallbackCost;
                    stamp.m_UpKeepCost = (uint)math.max(10, (subNets.Length * 10));
                    stamp.asset?.MarkDirty();
                }
            }
        }

        /// <summary>
        /// Attempts to find the most appropriate net composition entity matching the given road prefab and elevation.
        /// </summary>
        private bool TryGetBestMatchingCompositionEntity(NetPrefab roadPrefab, float y, out Entity compositionEntity)
        {
            compositionEntity = Entity.Null;
            if (roadPrefab == null) return false;

            Entity roadEntity = m_PrefabSystem.GetEntity(roadPrefab);
            if (roadEntity == Entity.Null || !EntityManager.Exists(roadEntity)) return false;

            if (!EntityManager.TryGetComponent(roadEntity, out NetGeometryData netGeometryData)) return false;
            if (!EntityManager.HasBuffer<Game.Prefabs.NetGeometryComposition>(roadEntity)) return false;

            Game.Net.Elevation startElevation = default;
            Game.Net.Elevation middleElevation = default;
            Game.Net.Elevation endElevation = default;

            startElevation.m_Elevation = new float2(y, y);
            middleElevation.m_Elevation = new float2(y, y);
            endElevation.m_Elevation = new float2(y, y);

            Game.Prefabs.CompositionFlags targetFlags = Game.Prefabs.NetCompositionHelpers.GetElevationFlags(startElevation, middleElevation, endElevation, netGeometryData);
            var compBuffer = EntityManager.GetBuffer<Game.Prefabs.NetGeometryComposition>(roadEntity);

            for (int i = 0; i < compBuffer.Length; i++)
            {
                var candidate = compBuffer[i].m_Mask;
                bool leftMatches = candidate.m_Left == targetFlags.m_Left;
                bool rightMatches = candidate.m_Right == targetFlags.m_Right;
                bool generalMatches = targetFlags.m_General == 0 || (candidate.m_General & targetFlags.m_General) == targetFlags.m_General;

                if (leftMatches && rightMatches && generalMatches)
                {
                    compositionEntity = compBuffer[i].m_Composition;
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Curve & Length Mathematics
        /// <summary>
        /// Calculates the total approximate length of all bezier curves within the provided sub-networks.
        /// </summary>
        private float CalculateTotalLength(ObjectSubNetInfo[] subNets)
        {
            float total = 0f;
            for (int i = 0; i < subNets.Length; i++)
            {
                if (subNets[i] != null) total += ApproximateBezierLength(subNets[i].m_BezierCurve, 12);
            }
            return total;
        }

        /// <summary>
        /// Approximates the arc length of a single bezier curve using segmented straight lines.
        /// </summary>
        private float ApproximateBezierLength(Colossal.Mathematics.Bezier4x3 curve, int steps)
        {
            float total = 0f;
            float3 prev = curve.a;

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float3 p = BezierPosition(curve, t);
                total += math.distance(prev, p);
                prev = p;
            }
            return total;
        }

        /// <summary>
        /// Computes the specific coordinate position along a cubic bezier curve at a given time value t.
        /// </summary>
        private float3 BezierPosition(Colossal.Mathematics.Bezier4x3 c, float t)
        {
            float u = 1f - t;
            return u * u * u * c.a + 3f * u * u * t * c.b + 3f * u * t * t * c.c + t * t * t * c.d;
        }
        #endregion
    }
}