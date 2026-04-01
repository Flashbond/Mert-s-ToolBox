using Colossal.Entities;
using Game.Prefabs;
using Game.Tools;
using System;
using System.Reflection;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region STAMP GENERATION & HANDOFF

        private static readonly BindingFlags PrivateInstanceFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// Queues a preview rebuild. Rebuild requests are never swallowed; the latest request wins.
        /// </summary>
        protected void QueuePreviewRebuild()
        {
            m_PreviewRebuildQueued = true;
            m_PreviewRebuildAt = RealtimeNow + PreviewRebuildDelay;
        }

        /// <summary>
        /// Promotes a queued preview rebuild into an executable create request.
        /// </summary>
        private void UpdateQueuedPreviewRebuild()
        {
            if (m_PreviewRebuildQueued && RealtimeNow >= m_PreviewRebuildAt)
            {
                m_PreviewRebuildQueued = false;
                m_ExecuteCreateShape = true;
            }
        }

        /// <summary>
        /// Clears the pending handoff state.
        /// </summary>
        private void ClearPendingHandoff()
        {
            m_PendingObjectToolHandoff = false;
            m_PendingHandoffStamp = null;
            m_PendingHandoffTimeoutAt = 0;
        }

        /// <summary>
        /// Gets the latest entity for the given runtime stamp and stores it in RuntimeStampEntity.
        /// </summary>
        private Entity RefreshRuntimeStampEntity(AssetStampPrefab stamp)
        {
            if (stamp == null)
                return Entity.Null;

            Entity refreshed = m_PrefabSystem.GetEntity(stamp);
            if (refreshed != Entity.Null && EntityManager.Exists(refreshed))
            {
                RuntimeStampEntity = refreshed;
                return refreshed;
            }

            return Entity.Null;
        }

        /// <summary>
        /// Ensures the runtime stamp exists. The same stamp is intentionally recycled.
        /// </summary>
        protected void InvalidateRuntimeStamp()
        {
            if (m_RuntimeStamp != null)
            {
                UnityEngine.Object.Destroy(m_RuntimeStamp);
                m_RuntimeStamp = null;
            }
            m_RuntimeStampCreated = false;
            RuntimeStampEntity = Entity.Null;
        }

        protected bool EnsureRuntimeStamp()
        {
            // DİKKAT: Yıkmak yok, aynı mührü geri dönüştürüyoruz!
            if (m_RuntimeStamp == null)
            {
                m_RuntimeStamp = UnityEngine.ScriptableObject.CreateInstance<AssetStampPrefab>();
                m_RuntimeStamp.name = "MertStamp_" + DateTime.Now.Ticks;

                m_PrefabSystem.AddPrefab(m_RuntimeStamp);
                RuntimeStampEntity = m_PrefabSystem.GetEntity(m_RuntimeStamp);
                m_RuntimeStampCreated = true;
            }
            return true;
        }

        /// <summary>
        /// Executes a pending shape build request.
        /// </summary>
        private void HandleExecuteCreateShape()
        {
            if (!m_ExecuteCreateShape) return;
            if (RealtimeNow - m_LastCreateTime < 0.03) return;
            if (m_IsCreatingShape) return;

            m_ExecuteCreateShape = false;
            m_LastCreateTime = RealtimeNow;
            m_IsCreatingShape = true;

            try
            {
                if (!EnsureRuntimeStamp()) return;

                if (RuntimeStampEntity != Entity.Null && EntityManager.Exists(RuntimeStampEntity))
                {
                    if (EntityManager.TryGetComponent(RuntimeStampEntity, out ObjectGeometryData oldGeom))
                    {
                        oldGeom.m_Size = float3.zero;
                        EntityManager.SetComponentData(RuntimeStampEntity, oldGeom);
                    }
                }

                if (!TryMutateTargetStamp()) return;

                if (m_RuntimeStamp != null)
                {
                    if (m_RuntimeStamp.m_Width <= 0) m_RuntimeStamp.m_Width = 16;
                    if (m_RuntimeStamp.m_Depth <= 0) m_RuntimeStamp.m_Depth = 16;
                    m_RuntimeStamp.asset?.MarkDirty();
                }

                RuntimeStampEntity = RefreshRuntimeStampEntity(m_RuntimeStamp);

                if (RuntimeStampEntity != Entity.Null && EntityManager.Exists(RuntimeStampEntity))
                {
                    m_PrefabSystem.UpdatePrefab(m_RuntimeStamp, RuntimeStampEntity);
                }

                m_PendingObjectToolHandoff = true;
                m_PendingHandoffStamp = m_RuntimeStamp;
                m_PendingHandoffTimeoutAt = RealtimeNow + 1.0;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] HandleExecuteCreateShape error: {e.Message}");
            }
            finally
            {
                m_IsCreatingShape = false;
            }
        }

        /// <summary>
        /// Lightweight readiness check: geometry and subnet buffer must exist.
        /// </summary>
        protected virtual bool IsRuntimeStampEntityReady(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return false;

            if (!EntityManager.TryGetComponent(entity, out ObjectGeometryData geom))
                return false;

            if (geom.m_Size.x <= 0.1f || geom.m_Size.z <= 0.1f)
                return false;

            if (!EntityManager.HasBuffer<Game.Prefabs.SubNet>(entity))
                return false;

            DynamicBuffer<Game.Prefabs.SubNet> subNets = EntityManager.GetBuffer<Game.Prefabs.SubNet>(entity);
            if (subNets.Length == 0)
                return false;

            return true;
        }

        /// <summary>
        /// Writes placement and subnet snap metadata to the entity. Returns true if anything changed.
        /// </summary>
        private bool ApplyStampSnapMetadataToEntity(Entity targetEntity)
        {
            bool changed = false;

            if (targetEntity == Entity.Null || !EntityManager.Exists(targetEntity))
                return false;

            if (!EntityManager.TryGetComponent(targetEntity, out PlaceableObjectData placeable))
            {
                placeable = new PlaceableObjectData();
                EntityManager.AddComponentData(targetEntity, placeable);
                changed = true;
            }

            var oldFlags = placeable.m_Flags;

            placeable.m_Flags |= Game.Objects.PlacementFlags.RoadEdge;
            placeable.m_Flags |= Game.Objects.PlacementFlags.RoadNode;
            placeable.m_Flags |= Game.Objects.PlacementFlags.RoadSide;
            placeable.m_Flags |= Game.Objects.PlacementFlags.SubNetSnap;
            placeable.m_Flags |= Game.Objects.PlacementFlags.OnGround;

            placeable.m_Flags = AllowOverlapPlacement
                ? placeable.m_Flags | Game.Objects.PlacementFlags.CanOverlap
                : placeable.m_Flags & ~Game.Objects.PlacementFlags.CanOverlap;

            if (oldFlags != placeable.m_Flags || changed)
            {
                EntityManager.SetComponentData(targetEntity, placeable);
                changed = true;
            }

            if (EntityManager.HasBuffer<Game.Prefabs.SubNet>(targetEntity))
            {
                DynamicBuffer<Game.Prefabs.SubNet> subNets = EntityManager.GetBuffer<Game.Prefabs.SubNet>(targetEntity);
                for (int i = 0; i < subNets.Length; i++)
                {
                    Game.Prefabs.SubNet subNet = subNets[i];
                    if (!subNet.m_Snapping.x || !subNet.m_Snapping.y)
                    {
                        subNet.m_Snapping = new bool2(true, true);
                        subNets[i] = subNet;
                        changed = true;
                    }
                }
            }

            return changed;
        }
        /// <summary>
        /// Applies stamp snap metadata to the given entity.
        /// </summary>
        private void PrepareRuntimeStampSnapMetadata(Entity targetEntity)
        {
            try
            {
                ApplyStampSnapMetadataToEntity(targetEntity);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] PrepareRuntimeStampSnapMetadata(Entity) error: {e.Message}");
            }
        }

        /// <summary>
        /// Waits until the runtime stamp entity is baked enough for ObjectTool handoff.
        /// </summary>
        private bool HandlePendingObjectToolHandoff()
        {
            if (!m_PendingObjectToolHandoff) return false;

            if (m_PendingCostResolve) return false;

            if (m_PendingHandoffStamp == null || RealtimeNow >= m_PendingHandoffTimeoutAt)
            {
                ClearPendingHandoff();
                return false;
            }

            Entity refreshedEntity = RefreshRuntimeStampEntity(m_PendingHandoffStamp);
            if (refreshedEntity == Entity.Null) return false;

            if (!IsRuntimeStampEntityReady(refreshedEntity))
                return false;

            if (RequiresSnapEnforcement)
            {
                PrepareRuntimeStampSnapMetadata(refreshedEntity);
            }

            if (EntityManager.TryGetComponent(refreshedEntity, out ObjectGeometryData geom))
            {
                geom.m_Flags |= Game.Objects.GeometryFlags.Circular;
                EntityManager.SetComponentData(refreshedEntity, geom);
            }

            AssetStampPrefab stampToHandOff = m_PendingHandoffStamp;
            ClearPendingHandoff();

            HandoffToObjectTool(stampToHandOff);
            return true;
        }
        /// <summary>
        /// Reflection helper for private ObjectTool fields.
        /// </summary>
        private void SetObjectToolPrivateField(string fieldName, object value)
        {
            try
            {
                m_ObjectToolSystem?.GetType().GetField(fieldName, PrivateInstanceFlags)?.SetValue(m_ObjectToolSystem, value);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] SetObjectToolPrivateField({fieldName}) error: {e.Message}");
            }
        }

        /// <summary>
        /// Reflection helper for private ObjectTool methods.
        /// </summary>
        private void InvokeObjectToolPrivateMethod(string methodName)
        {
            try
            {
                m_ObjectToolSystem?.GetType().GetMethod(methodName, PrivateInstanceFlags)?.Invoke(m_ObjectToolSystem, null);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] InvokeObjectToolPrivateMethod({methodName}) error: {e.Message}");
            }
        }

        /// <summary>
        /// Runs the full ObjectTool OnUpdate pipeline once.
        /// </summary>
        private void ForceCompleteObjectToolUpdate()
        {
            try
            {
                if (m_ObjectToolSystem == null)
                    return;

                MethodInfo onUpdateMethod = m_ObjectToolSystem.GetType().GetMethod(
                    "OnUpdate",
                    PrivateInstanceFlags,
                    null,
                    new Type[] { typeof(Unity.Jobs.JobHandle) },
                    null);

                if (onUpdateMethod == null)
                    return;

                onUpdateMethod.Invoke(m_ObjectToolSystem, new object[] { default(Unity.Jobs.JobHandle) });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] ForceCompleteObjectToolUpdate error: {e.Message}");
            }
        }

        /// <summary>
        /// Hands the ready runtime stamp over to ObjectTool.
        /// </summary>
        protected void HandoffToObjectTool(AssetStampPrefab stamp)
        {
            if (m_ObjectToolSystem == null || m_ToolSystem == null || stamp == null)
                return;
            try
            {
                if (m_ToolSystem.activeTool != m_ObjectToolSystem)
                {
                    m_ToolSystem.selected = Entity.Null;
                    m_ToolSystem.activeTool = m_ObjectToolSystem;
                }

                bool setOk = m_ObjectToolSystem.TrySetPrefab(stamp);

                if (!setOk)
                    return;
                Entity refreshedEntity = RefreshRuntimeStampEntity(stamp);
                if (refreshedEntity != Entity.Null && EntityManager.Exists(refreshedEntity))
                {
                    RuntimeStampEntity = refreshedEntity;

                    if (RequiresSnapEnforcement)
                    {
                        PrepareRuntimeStampSnapMetadata(refreshedEntity);
                    }
                }

                SetObjectToolPrivateField("m_ForceUpdate", true);
                SetObjectToolPrivateField("m_State", ObjectToolSystem.State.Default);

                InvokeObjectToolPrivateMethod("InitializeRaycast");
                ApplyRoadSnapState();

                if (RequiresSnapEnforcement)
                {
                    EnforceRuntimeStampSnapMetadata();
                }

                ForceCompleteObjectToolUpdate();

            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] HandoffToObjectTool error: {e.Message}");
            }
        }

        #endregion
    }
}