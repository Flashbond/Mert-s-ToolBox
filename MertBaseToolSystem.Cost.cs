using Colossal.Entities;
using Game.Prefabs;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region COST ENGINE

        /// <summary>
        /// Calculates the construction and upkeep costs based on the road's length and elevation, and applies them to the stamp.
        /// </summary>
        protected bool TryCalculateAndApplyCosts(AssetStampPrefab stamp, ObjectSubNetInfo[] subNets, NetPrefab roadPrefab, float highestElevation)
        {
            if (stamp == null || subNets == null || subNets.Length == 0 || roadPrefab == null) return false;

            if (!TryGetBestMatchingCompositionEntity(roadPrefab, highestElevation, out Entity compositionEntity)) return false;
            if (compositionEntity == Entity.Null || !EntityManager.Exists(compositionEntity)) return false;
            if (!EntityManager.TryGetComponent(compositionEntity, out Game.Prefabs.PlaceableNetComposition comp)) return false;

            float totalLength = CalculateTotalLength(subNets);
            float absElevation = math.abs(highestElevation);

            int cells = math.max(1, Mathf.RoundToInt(totalLength / 8f));
            int elevationSteps = math.max(0, Mathf.RoundToInt(absElevation / 10f));

            int construction = cells * ((int)comp.m_ConstructionCost + elevationSteps * (int)comp.m_ElevationCost);
            int upkeep = cells * (int)comp.m_UpkeepCost;

            stamp.m_ConstructionCost = (uint)math.max(100, construction);
            stamp.m_UpKeepCost = (uint)math.max(10, upkeep);

            return true;
        }

        /// <summary>
        /// Finds the most appropriate net composition entity based on the given elevation and road prefab.
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

        /// <summary>
        /// Calculates the total approximate length of all sub-nets.
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
        /// Approximates the length of a given Bezier curve by dividing it into line segments.
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
        /// Calculates a specific point on a Bezier curve given a parameter t between 0 and 1.
        /// </summary>
        private float3 BezierPosition(Colossal.Mathematics.Bezier4x3 c, float t)
        {
            float u = 1f - t;
            return u * u * u * c.a + 3f * u * u * t * c.b + 3f * u * t * t * c.c + t * t * t * c.d;
        }

        /// <summary>
        /// Queues a cost calculation to be resolved in future frames if the entity is not fully initialized yet.
        /// </summary>
        protected void QueueDeferredCostResolve(AssetStampPrefab stamp, ObjectSubNetInfo[] subNets, NetPrefab roadPrefab, float highestElevation)
        {
            m_PendingCostResolve = true;
            m_CostResolveRetries = 10;
            m_PendingCostStamp = stamp;
            m_PendingCostSubNets = subNets;
            m_PendingCostRoadPrefab = roadPrefab;
            m_PendingCostHighestElevation = highestElevation;
        }

        /// <summary>Processes any pending deferred cost calculations, retrying a set number of times before giving up.</summary>
        private void HandlePendingCostResolve()
        {
            if (!m_PendingCostResolve) return;

            if (m_PendingCostStamp != null && m_PendingCostSubNets != null && m_PendingCostRoadPrefab != null)
            {
                if (TryCalculateAndApplyCosts(m_PendingCostStamp, m_PendingCostSubNets, m_PendingCostRoadPrefab, m_PendingCostHighestElevation))
                {
                    m_PrefabSystem.UpdatePrefab(m_PendingCostStamp, RuntimeStampEntity);

                    m_PendingCostResolve = false;
                    m_CostResolveRetries = 0;
                    m_PendingCostStamp = null;
                    m_PendingCostSubNets = null;
                    m_PendingCostRoadPrefab = null;

                    return;
                }

                m_CostResolveRetries--;
                if (m_CostResolveRetries <= 0)
                {
                    m_PendingCostResolve = false;
                    m_PendingCostStamp = null;
                    m_PendingCostSubNets = null;
                    m_PendingCostRoadPrefab = null;
                }
            }
        }
        #endregion
    }
}