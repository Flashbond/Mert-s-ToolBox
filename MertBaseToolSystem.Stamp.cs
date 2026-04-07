using Colossal.Entities;
using Game.Prefabs;
using Game.Tools;
using System;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region Fields & Constants
        private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private int m_WaitCounter = 0;
        #endregion

        #region Initialization & Prebaking
        /// <summary>
        /// Prebakes the shared runtime stamp prefab needed for shape generation.
        /// </summary>
        protected void PrebakeRuntimeStamp()
        {
            if (s_SharedStampRegistered)
            {
                m_RuntimeStamp = s_SharedRuntimeStamp;
                RuntimeStampEntity = s_SharedRuntimeStampEntity;
                return;
            }

            s_SharedRuntimeStamp = UnityEngine.ScriptableObject.CreateInstance<AssetStampPrefab>();
            s_SharedRuntimeStamp.name = "MertsToolBox_SharedPrebakedStamp_" + DateTime.Now.Ticks;

            if (!s_SharedRuntimeStamp.Has<ObjectSubNets>())
                s_SharedRuntimeStamp.AddComponent<ObjectSubNets>();

            if (!s_SharedRuntimeStamp.Has<PlaceableObject>())
                s_SharedRuntimeStamp.AddComponent<PlaceableObject>();

            if (!s_SharedRuntimeStamp.Has<Game.Prefabs.PlaceableNet>())
                s_SharedRuntimeStamp.AddComponent<Game.Prefabs.PlaceableNet>();

            m_PrefabSystem.AddPrefab(s_SharedRuntimeStamp);
            s_SharedRuntimeStampEntity = m_PrefabSystem.GetEntity(s_SharedRuntimeStamp);

            m_RuntimeStamp = s_SharedRuntimeStamp;
            RuntimeStampEntity = s_SharedRuntimeStampEntity;
            s_SharedStampRegistered = true;
            s_PrebakeCompleted = false;
        }

        /// <summary>
        /// Attempts to perform a late prebake using a real road prefab once the game data is loaded.
        /// </summary>
        private void TryLatePrebakeWithRealRoad()
        {
            if (m_WaitCounter++ < 60)
                return;
            m_WaitCounter = 0;
            if (!s_SharedStampRegistered || s_SharedRuntimeStamp == null)
                return;

            try
            {
                EntityQuery query = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Prefabs.NetData>(),
                    ComponentType.ReadOnly<Game.Prefabs.PrefabData>()
                );

                if (query.IsEmptyIgnoreFilter)
                    return;

                using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

                NetPrefab realRoad = null;

                foreach (var entity in entities)
                {
                    if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefab))
                        continue;

                    if (prefab is not NetPrefab net)
                        continue;

                    if (string.Equals(net.name, "Small Road", StringComparison.OrdinalIgnoreCase))
                    {
                        realRoad = net;
                        break;
                    }
                }

                if (realRoad == null)
                    return;

                if (!s_SharedRuntimeStamp.TryGet<ObjectSubNets>(out var subNets) || subNets == null)
                {
                    subNets = s_SharedRuntimeStamp.AddComponent<ObjectSubNets>();
                }

                subNets.m_SubNets = new[]
                {
            new ObjectSubNetInfo
            {
                m_NetPrefab = realRoad,
                m_BezierCurve = new Colossal.Mathematics.Bezier4x3(
                            new float3(0f, 0f, 0f),
                            new float3(2f, 0f, 0f),
                            new float3(4f, 0f, 0f),
                            new float3(6f, 0f, 0f)
                        ),
                        m_NodeIndex = new int2(0, 1),
                        m_ParentMesh = new int2(-1, -1)
                    }
                };

                s_SharedRuntimeStamp.m_Width = 4;
                s_SharedRuntimeStamp.m_Depth = 4;
                s_SharedRuntimeStamp.asset?.MarkDirty();

                if (s_SharedRuntimeStampEntity == Entity.Null || !EntityManager.Exists(s_SharedRuntimeStampEntity))
                {
                    s_SharedRuntimeStampEntity = m_PrefabSystem.GetEntity(s_SharedRuntimeStamp);
                }

                if (s_SharedRuntimeStampEntity == Entity.Null || !EntityManager.Exists(s_SharedRuntimeStampEntity))
                    return;

                m_PrefabSystem.UpdatePrefab(s_SharedRuntimeStamp, s_SharedRuntimeStampEntity);
                s_SharedRuntimeStampEntity = m_PrefabSystem.GetEntity(s_SharedRuntimeStamp);

                if (s_SharedRuntimeStampEntity != Entity.Null && EntityManager.Exists(s_SharedRuntimeStampEntity))
                {
                    PrepareRuntimeStampSnapMetadata(s_SharedRuntimeStampEntity);
                }

                m_RuntimeStamp = s_SharedRuntimeStamp;
                RuntimeStampEntity = s_SharedRuntimeStampEntity;

                s_PrebakeCompleted = true;
                TryWarmObjectToolPreviewFoundationOnce();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] TryLatePrebakeWithRealRoad error {e.Message}");
            }
        }

        /// <summary>
        /// Warms up the object tool preview foundation once to prevent cold start issues.
        /// </summary>
        private void TryWarmObjectToolPreviewFoundationOnce()
        {
            if (s_ObjectToolFoundationWarmed)
                return;

            if (m_ObjectToolSystem == null || m_RuntimeStamp == null)
                return;

            try
            {
                MertToolState.SuppressToolChangedDuringColdstart = true;
                MertToolState.SuppressToolbarCaptureDuringColdstart = true;

                if (m_ToolSystem.activeTool != m_ObjectToolSystem)
                {
                    m_ToolSystem.selected = Entity.Null;
                    m_ToolSystem.activeTool = m_ObjectToolSystem;
                }

                bool setOk = m_ObjectToolSystem.TrySetPrefab(m_RuntimeStamp);
                if (!setOk)
                    return;

                RefreshObjectToolPreviewState();
                s_ObjectToolFoundationWarmed = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox][COLDSTART] Warm foundation error: {e.Message}");
            }
            finally
            {
                MertToolState.SuppressToolbarCaptureDuringColdstart = false;
                MertToolState.SuppressToolChangedDuringColdstart = false;
            }
        }

        /// <summary>
        /// Prepares the context and queues a preview rebuild when the tool is enabled.
        /// </summary>
        private void PrimeAndShowPreviewOnEnable()
        {
            EnsureContextRecipeReady();

            if (m_RuntimeStamp == null)
                return;

            m_PendingCreateShape = false;
            QueuePreviewRebuild();
        }
        #endregion

        #region Context & Metadata Configuration
        /// <summary>
        /// Ensures the baseline context recipe and placement flags are prepared.
        /// </summary>
        private void EnsureContextRecipeReady()
        {
            if (m_ContextRecipeReady)
                return;

            PrepareManualIntersectionLikeContextRecipe();
            m_ContextRecipeReady = true;
        }

        /// <summary>
        /// Prepares the foundational placement flags resembling manual intersection creation.
        /// </summary>
        private void PrepareManualIntersectionLikeContextRecipe()
        {
            m_DesiredPlacementFlags =
                Game.Objects.PlacementFlags.OnGround |
                Game.Objects.PlacementFlags.RoadEdge |
                Game.Objects.PlacementFlags.RoadSide;
        }

        /// <summary>
        /// Wraps the application of snapping metadata to the target entity in a safe try-catch block.
        /// </summary>
        private void PrepareRuntimeStampSnapMetadata(Entity targetEntity)
        {
            try
            {
                ApplyStampSnapMetadataToEntity(targetEntity);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] PrepareRuntimeStampSnapMetadata error: {e.Message}");
            }
        }

        /// <summary>
        /// Applies detailed snapping metadata and placement flags to the ECS entity representing the stamp.
        /// </summary>
        private bool ApplyStampSnapMetadataToEntity(Entity targetEntity)
        {
            bool changed = false;

            if (targetEntity == Entity.Null || !EntityManager.Exists(targetEntity)) return false;

            if (!EntityManager.TryGetComponent(targetEntity, out PlaceableObjectData placeable))
            {
                placeable = new PlaceableObjectData();
                EntityManager.AddComponentData(targetEntity, placeable);
                changed = true;
            }

            var oldFlags = placeable.m_Flags;

            placeable.m_Flags |= m_DesiredPlacementFlags;

            if (m_ContextUsesRoadNode)
                placeable.m_Flags |= Game.Objects.PlacementFlags.RoadNode;
            else
                placeable.m_Flags &= ~Game.Objects.PlacementFlags.RoadNode;

            placeable.m_Flags = AllowOverlapPlacement
                ? placeable.m_Flags | Game.Objects.PlacementFlags.CanOverlap
                : placeable.m_Flags & ~Game.Objects.PlacementFlags.CanOverlap;

            bool isAnySnapActive = RequiresSnapEnforcement && (m_SnapGeometryEnabled || m_SnapNetSideEnabled || m_SnapNetAreaEnabled);

            if (isAnySnapActive)
                placeable.m_Flags |= Game.Objects.PlacementFlags.SubNetSnap;
            else
                placeable.m_Flags &= ~Game.Objects.PlacementFlags.SubNetSnap;

            if (oldFlags != placeable.m_Flags || changed)
            {
                EntityManager.SetComponentData(targetEntity, placeable);
                changed = true;
            }

            if (EntityManager.HasBuffer<Game.Prefabs.SubNet>(targetEntity))
            {
                bool2 dynamicSubNetSnapping = new bool2(isAnySnapActive, isAnySnapActive);
                DynamicBuffer<Game.Prefabs.SubNet> subNets = EntityManager.GetBuffer<Game.Prefabs.SubNet>(targetEntity);

                for (int i = 0; i < subNets.Length; i++)
                {
                    Game.Prefabs.SubNet subNet = subNets[i];
                    if (subNet.m_Snapping.x != dynamicSubNetSnapping.x || subNet.m_Snapping.y != dynamicSubNetSnapping.y)
                    {
                        subNet.m_Snapping = dynamicSubNetSnapping;
                        subNets[i] = subNet;
                        changed = true;
                    }
                }
            }
            return changed;
        }
        #endregion

        #region Mutation & Shape Generation
        /// <summary>
        /// Handles the execution of the queued shape creation process safely.
        /// </summary>
        private void HandleExecuteCreateShape()
        {
            if (!m_PendingCreateShape)
                return;

            if (m_IsCreatingShape)
                return;

            m_PendingCreateShape = false;
            m_IsCreatingShape = true;

            try
            {
                TryCommitRuntimeStampMutation();
            }
            finally
            {
                m_IsCreatingShape = false;
            }
        }

        /// <summary>
        /// Commits the newly generated geometry to the runtime stamp and updates the prefab system.
        /// </summary>
        private bool TryCommitRuntimeStampMutation()
        {
            if (m_RuntimeStamp == null)
                return false;

            if (!TryMutateTargetStamp())
                return false;

            Entity refreshedEntity = RefreshRuntimeStampEntity(m_RuntimeStamp);
            if (refreshedEntity != Entity.Null && EntityManager.Exists(refreshedEntity))
            {
                RuntimeStampEntity = refreshedEntity;
                m_PrefabSystem.UpdatePrefab(m_RuntimeStamp, refreshedEntity);
            }

            m_PendingObjectToolHandoff = true;
            m_PendingHandoffStamp = m_RuntimeStamp;

            return true;
        }

        /// <summary>
        /// Validates whether the runtime stamp entity has been fully constructed with required geometry and network buffers.
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
        /// Retrieves and updates the current entity representation of the given stamp prefab.
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
        #endregion

        #region Handoff & Tool Execution
        /// <summary>
        /// Processes a queued handoff operation, transferring the generated stamp to the object tool.
        /// </summary>
        private bool HandlePendingObjectToolHandoff()
        {
            if (!m_PendingObjectToolHandoff)
                return false;

            if (!TryResolvePendingHandoffEntity(out Entity refreshedEntity))
                return false;

            if (RequiresSnapEnforcement)
            {
                PrepareRuntimeStampSnapMetadata(refreshedEntity);
            }

            AssetStampPrefab stampToHandOff = m_PendingHandoffStamp;
            ClearPendingHandoff();

            HandoffToObjectTool(stampToHandOff);
            return true;
        }

        /// <summary>
        /// Attempts to resolve and validate the pending handoff entity before transferring control.
        /// </summary>
        private bool TryResolvePendingHandoffEntity(out Entity refreshedEntity)
        {
            refreshedEntity = Entity.Null;

            if (m_PendingHandoffStamp == null)
            {
                ClearPendingHandoff();
                return false;
            }

            refreshedEntity = RefreshRuntimeStampEntity(m_PendingHandoffStamp);
            if (refreshedEntity == Entity.Null)
                return false;

            if (!IsRuntimeStampEntityReady(refreshedEntity))
                return false;

            return true;
        }

        /// <summary>
        /// Hands off the constructed asset stamp to the active object tool system for preview and placement.
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
                }
                RefreshObjectToolPreviewState();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] HandoffToObjectTool error: {e.Message}");
            }
        }

        /// <summary>
        /// Clears out any pending handoff flags and cached stamp data.
        /// </summary>
        private void ClearPendingHandoff()
        {
            m_PendingObjectToolHandoff = false;
            m_PendingHandoffStamp = null;
        }
        #endregion

        #region Reflection Utilities
        /// <summary>
        /// Sets a private field value within the object tool system using reflection.
        /// </summary>
        private void SetObjectToolPrivateField(string fieldName, object value)
        {
            try { m_ObjectToolSystem?.GetType().GetField(fieldName, PrivateInstanceFlags)?.SetValue(m_ObjectToolSystem, value); }
            catch { }
        }

        /// <summary>
        /// Invokes a private parameterless method within the object tool system using reflection.
        /// </summary>
        private void InvokeObjectToolPrivateMethod(string methodName)
        {
            try { m_ObjectToolSystem?.GetType().GetMethod(methodName, PrivateInstanceFlags)?.Invoke(m_ObjectToolSystem, null); }
            catch { }
        }

        /// <summary>
        /// Forces the object tool system to reinitialize its raycast and update its state via reflection.
        /// </summary>
        private void RefreshObjectToolPreviewState()
        {
            InvokeObjectToolPrivateMethod("InitializeRaycast");
            SetObjectToolPrivateField("m_ForceUpdate", true);
            SetObjectToolPrivateField("m_State", ObjectToolSystem.State.Default);
            ForceCompleteObjectToolUpdate();
        }

        /// <summary>
        /// Forces a complete update cycle on the object tool system via reflection.
        /// </summary>
        private void ForceCompleteObjectToolUpdate()
        {
            try
            {
                if (m_ObjectToolSystem == null) return;
                MethodInfo onUpdateMethod = m_ObjectToolSystem.GetType().GetMethod("OnUpdate", PrivateInstanceFlags, null, new Type[] { typeof(Unity.Jobs.JobHandle) }, null);
                onUpdateMethod?.Invoke(m_ObjectToolSystem, new object[] { default(Unity.Jobs.JobHandle) });
            }
            catch { }
        }
        #endregion
    }
}