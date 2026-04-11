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

        private static AssetStampPrefab s_WarmupRuntimeStamp;
        private static Entity s_WarmupRuntimeStampEntity;
        private static bool s_WarmupStampRegistered;
        #endregion

        #region Initialization & Prebaking
        /// <summary>
        /// Initializes the session state variables required to track and manage the per-road stamp baking process.
        /// </summary>
        private void EnsurePerRoadBakeSessionStarted()
        {
            if (s_StampBakeSessionStarted)
                return;

            s_StampBakeSessionStarted = true;
            s_StampBakeSessionSealed = false;
            s_BakeStablePasses = 0;
            s_LastDiscoveredRoadCount = -1;
        }

        /// <summary>
        /// Instantiates and configures a new standalone asset stamp prefab specifically tailored for the given road.
        /// </summary>
        private AssetStampPrefab CreatePerRoadStampPrefab(NetPrefab roadPrefab)
        {
            if (roadPrefab == null)
                return null;

            var stamp = UnityEngine.ScriptableObject.CreateInstance<AssetStampPrefab>();
            stamp.name = $"MertsToolBox_RoadStamp_{roadPrefab.name}_{DateTime.Now.Ticks}";

            if (!stamp.Has<ObjectSubNets>())
                stamp.AddComponent<ObjectSubNets>();

            if (!stamp.Has<PlaceableObject>())
                stamp.AddComponent<PlaceableObject>();

            if (!stamp.Has<Game.Prefabs.PlaceableNet>())
                stamp.AddComponent<Game.Prefabs.PlaceableNet>();

            m_PrefabSystem.AddPrefab(stamp);
            return stamp;
        }

        /// <summary>
        /// Creates a detached warmup stamp prefab that is never stored in the per-road registry.
        /// This isolates ObjectTool foundation warmup from real gameplay stamps.
        /// </summary>
        private AssetStampPrefab CreateWarmupStampPrefab()
        {
            var stamp = UnityEngine.ScriptableObject.CreateInstance<AssetStampPrefab>();
            stamp.name = $"MertsToolBox_WarmupStamp_{DateTime.Now.Ticks}";

            if (!stamp.Has<ObjectSubNets>())
                stamp.AddComponent<ObjectSubNets>();

            if (!stamp.Has<PlaceableObject>())
                stamp.AddComponent<PlaceableObject>();

            if (!stamp.Has<Game.Prefabs.PlaceableNet>())
                stamp.AddComponent<Game.Prefabs.PlaceableNet>();

            m_PrefabSystem.AddPrefab(stamp);
            return stamp;
        }

        /// <summary>
        /// Builds or refreshes the isolated warmup stamp using a deterministic road prefab.
        /// This must never touch the per-road registry.
        /// </summary>
        private bool TryPrepareWarmupStamp(NetPrefab roadPrefab)
        {
            if (roadPrefab == null)
                return false;

            try
            {
                if (!s_WarmupStampRegistered || s_WarmupRuntimeStamp == null)
                {
                    s_WarmupRuntimeStamp = CreateWarmupStampPrefab();
                    if (s_WarmupRuntimeStamp == null)
                        return false;

                    s_WarmupRuntimeStampEntity = m_PrefabSystem.GetEntity(s_WarmupRuntimeStamp);
                    s_WarmupStampRegistered = true;
                }

                if (!s_WarmupRuntimeStamp.TryGet<ObjectSubNets>(out var subNets) || subNets == null)
                    subNets = s_WarmupRuntimeStamp.AddComponent<ObjectSubNets>();

                subNets.m_SubNets = new[]
                {
            new ObjectSubNetInfo
            {
                m_NetPrefab = roadPrefab,
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

                s_WarmupRuntimeStamp.m_Width = 4;
                s_WarmupRuntimeStamp.m_Depth = 4;
                s_WarmupRuntimeStamp.asset?.MarkDirty();

                if (s_WarmupRuntimeStampEntity == Entity.Null || !EntityManager.Exists(s_WarmupRuntimeStampEntity))
                {
                    s_WarmupRuntimeStampEntity = m_PrefabSystem.GetEntity(s_WarmupRuntimeStamp);
                }

                if (s_WarmupRuntimeStampEntity == Entity.Null || !EntityManager.Exists(s_WarmupRuntimeStampEntity))
                    return false;

                m_PrefabSystem.UpdatePrefab(s_WarmupRuntimeStamp, s_WarmupRuntimeStampEntity);
                s_WarmupRuntimeStampEntity = m_PrefabSystem.GetEntity(s_WarmupRuntimeStamp);

                if (s_WarmupRuntimeStampEntity == Entity.Null || !EntityManager.Exists(s_WarmupRuntimeStampEntity))
                    return false;

                PrepareRuntimeStampSnapMetadata(s_WarmupRuntimeStampEntity);
                return true;
            }
            catch (Exception e)
            {
                ModRuntime.Warn($"[MertsToolBox][WARMUP] TryPrepareWarmupStamp error: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to perform a late prebake using a real road prefab once the game data is loaded.
        /// </summary>
        private void TryLatePrebakeWithRealRoad()
        {
            if (m_WaitCounter++ < 60)
                return;

            m_WaitCounter = 0;

            EnsurePerRoadBakeSessionStarted();

            if (!s_StampBakeSessionSealed)
            {
                RunPerRoadStampBakePass();
            }

            if (s_ObjectToolFoundationWarmed)
                return;

            EntityQuery query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Prefabs.NetData>(),
                ComponentType.ReadOnly<Game.Prefabs.PrefabData>()
            );

            if (query.IsEmptyIgnoreFilter)
                return;

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            NetPrefab warmupRoad = null;

            foreach (var entity in entities)
            {
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefab))
                    continue;

                if (prefab is not NetPrefab net)
                    continue;

                if (string.Equals(net.name, "Small Road", StringComparison.OrdinalIgnoreCase))
                {
                    warmupRoad = net;
                    break;
                }
            }

            if (warmupRoad == null)
                return;

            if (!TryPrepareWarmupStamp(warmupRoad))
                return;

            m_RuntimeStamp = s_WarmupRuntimeStamp;
            RuntimeStampEntity = s_WarmupRuntimeStampEntity;

            TryWarmObjectToolPreviewFoundationOnce();
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

                s_ObjectToolFoundationWarmed = true;
            }
            catch (Exception e)
            {
                ModRuntime.Warn($"[MertsToolBox][COLDSTART] Warm foundation error: {e.Message}");
            }
            finally
            {
                MertToolState.SuppressToolbarCaptureDuringColdstart = false;
                MertToolState.SuppressToolChangedDuringColdstart = false;
            }
        }

        /// <summary>
        /// Executes a processing pass to discover eligible roads and iteratively bake their stamp prefabs until the session stabilizes.
        /// </summary>
        private void RunPerRoadStampBakePass()
        {
            if (s_StampBakeSessionSealed)
                return;

            EntityQuery query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Prefabs.NetData>(),
                ComponentType.ReadOnly<Game.Prefabs.PrefabData>()
            );

            if (query.IsEmptyIgnoreFilter)
                return;

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            int discoveredCount = 0;
            int pendingCount = 0;

            foreach (var entity in entities)
            {
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefab))
                    continue;

                if (prefab is not NetPrefab roadPrefab)
                    continue;

                if (!IsEligibleRoadForPrebake(roadPrefab))
                    continue;

                discoveredCount++;

                if (!s_BakeStateByRoadEntity.TryGetValue(entity, out var state))
                {
                    s_BakeStateByRoadEntity[entity] = StampBakeState.Pending;
                    state = StampBakeState.Pending;
                }

                if (state == StampBakeState.Ready || state == StampBakeState.Failed)
                    continue;

                pendingCount++;

                bool baked = TryBakeStampForRoad(roadPrefab);
                if (!baked && s_BakeStateByRoadEntity[entity] != StampBakeState.Failed)
                {
                    s_BakeStateByRoadEntity[entity] = StampBakeState.Pending;
                }
            }

            int unresolved = 0;
            foreach (var kv in s_BakeStateByRoadEntity)
            {
                if (kv.Value == StampBakeState.Pending)
                    unresolved++;
            }

            if (discoveredCount == s_LastDiscoveredRoadCount && unresolved == 0)
                s_BakeStablePasses++;
            else
                s_BakeStablePasses = 0;

            s_LastDiscoveredRoadCount = discoveredCount;

            if (s_BakeStablePasses >= BakeStablePassesRequired)
            {
                s_StampBakeSessionSealed = true;
            }
        }

        /// <summary>
        /// Determines whether the specified road prefab meets the criteria required for generating a dedicated prebaked stamp.
        /// </summary>
        private bool IsEligibleRoadForPrebake(NetPrefab roadPrefab)
        {
            if (roadPrefab == null)
                return false;

            if (!IsStandardRoadPrefab(roadPrefab))
                return false;

            string name = roadPrefab.name ?? string.Empty;

            if (name.StartsWith("MertsToolBox_SharedPrebakedStamp", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Attempts to safely retrieve the cached prebaked stamp prefab and its corresponding runtime entity for the specified road.
        /// </summary>
        private bool TryGetPrebakedStampForRoad(NetPrefab roadPrefab, out AssetStampPrefab stamp, out Entity stampEntity)
        {
            stamp = null;
            stampEntity = Entity.Null;

            if (roadPrefab == null)
                return false;

            Entity roadEntity = m_PrefabSystem.GetEntity(roadPrefab);
            if (roadEntity == Entity.Null)
                return false;

            if (!s_StampByRoadEntity.TryGetValue(roadEntity, out stamp) || stamp == null)
                return false;

            if (!s_StampEntityByRoadEntity.TryGetValue(roadEntity, out stampEntity))
                stampEntity = Entity.Null;

            if (stampEntity == Entity.Null || !EntityManager.Exists(stampEntity))
            {
                stampEntity = RefreshRuntimeStampEntity(stamp);
                if (stampEntity == Entity.Null || !EntityManager.Exists(stampEntity))
                    return false;

                s_StampEntityByRoadEntity[roadEntity] = stampEntity;
            }

            return true;
        }

        /// <summary>
        /// Generates, registers, and caches a functional stamp prefab with baseline geometry and metadata for the specified road.
        /// </summary>
        private bool TryBakeStampForRoad(NetPrefab roadPrefab)
        {
            if (roadPrefab == null)
                return false;

            Entity roadEntity = m_PrefabSystem.GetEntity(roadPrefab);
            if (roadEntity == Entity.Null)
                return false;

            try
            {
                AssetStampPrefab stamp;
                if (!s_StampByRoadEntity.TryGetValue(roadEntity, out stamp) || stamp == null)
                {
                    stamp = CreatePerRoadStampPrefab(roadPrefab);
                    if (stamp == null)
                    {
                        s_BakeStateByRoadEntity[roadEntity] = StampBakeState.Failed;
                        return false;
                    }

                    s_StampByRoadEntity[roadEntity] = stamp;
                }

                if (!stamp.TryGet<ObjectSubNets>(out var subNets) || subNets == null)
                    subNets = stamp.AddComponent<ObjectSubNets>();

                subNets.m_SubNets = new[]
                {
                new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
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

                stamp.m_Width = 4;
                stamp.m_Depth = 4;
                stamp.asset?.MarkDirty();

                Entity stampEntity = m_PrefabSystem.GetEntity(stamp);
                if (stampEntity == Entity.Null || !EntityManager.Exists(stampEntity))
                {
                    s_BakeStateByRoadEntity[roadEntity] = StampBakeState.Pending;
                    return false;
                }

                m_PrefabSystem.UpdatePrefab(stamp, stampEntity);
                stampEntity = m_PrefabSystem.GetEntity(stamp);

                if (stampEntity == Entity.Null || !EntityManager.Exists(stampEntity))
                {
                    s_BakeStateByRoadEntity[roadEntity] = StampBakeState.Pending;
                    return false;
                }

                PrepareRuntimeStampSnapMetadata(stampEntity);

                s_StampByRoadEntity[roadEntity] = stamp;
                s_StampEntityByRoadEntity[roadEntity] = stampEntity;
                s_BakeStateByRoadEntity[roadEntity] = StampBakeState.Ready;

                return true;
            }
            catch
            {
                s_BakeStateByRoadEntity[roadEntity] = StampBakeState.Failed;
                return false;
            }
        }

        /// <summary>
        /// Prepares the context and queues a preview rebuild when the tool is enabled.
        /// </summary>
        private void PrimeAndShowPreviewOnEnable()
        {
            EnsureContextRecipeReady();

            NetPrefab currentRoad = TryGetCurrentSelectedRoadPrefab();
            if (currentRoad != null &&
                TryGetPrebakedStampForRoad(currentRoad, out var prebakedStamp, out var prebakedEntity))
            {
                m_RuntimeStamp = prebakedStamp;
                RuntimeStampEntity = prebakedEntity;
            }
            else
            {
                ModRuntime.Warn(
                    $"[MertsToolBox][ROAD-STAMP] MISSING | road={currentRoad?.name ?? "NULL"}");
                return;
            }

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
                ModRuntime.Warn($"[MertsToolBox] PrepareRuntimeStampSnapMetadata error: {e.Message}");
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

                ModRuntime.TrySetField(m_ObjectToolSystem, "m_SelectedPrefab", null);
                ModRuntime.TrySetField(m_ObjectToolSystem, "m_Prefab", null);
                bool setOk = m_ObjectToolSystem.TrySetPrefab(stamp);
                if (!setOk)
                    return;

            }
            catch (Exception e)
            {
                ModRuntime.Warn($"[MertsToolBox] HandoffToObjectTool error: {e.Message}");
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
        #endregion
    }
}