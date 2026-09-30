using Colossal.Entities;
using Game.Prefabs;
using Game.Tools;
using MertsToolBox.Core;
using MertsToolBox.Management;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {

        #region Initialization
        /// <summary>
        /// Creates the shared runtime stamp prefab used for handing generated geometry off to
        /// ObjectTool. Lazily created the first time a real TryMutateTargetStamp /
        /// PrimeAndShowPreviewOnEnable happens - i.e. the first genuine tool use - never during
        /// game load.
        /// </summary>
        private AssetStampPrefab CreateSharedRuntimeStampPrefab()
        {
            var stamp = ScriptableObject.CreateInstance<AssetStampPrefab>();

            stamp.name = "MertsToolBox_RuntimeStamp";

            if (!stamp.Has<ObjectSubNets>())
                stamp.AddComponent<ObjectSubNets>();

            if (!stamp.Has<PlaceableObject>())
                stamp.AddComponent<PlaceableObject>();

            if (!stamp.Has<PlaceableNet>())
                stamp.AddComponent<PlaceableNet>();

            m_PrefabSystem.AddPrefab(stamp);

            return stamp;
        }

        /// <summary>
        /// Prepares the context and queues a preview rebuild when the tool is enabled.
        /// </summary>
        private void PrimeAndShowPreviewOnEnable()
        {
            EnsureContextRecipeReady();

            if (!TryGetSharedRuntimeStamp(out var stamp))
            {
                ModRuntime.Warn("[ROAD-STAMP] Shared runtime stamp missing");
                return;
            }

            m_RuntimeStamp = stamp;

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
            m_DesiredPlacementFlags = Game.Objects.PlacementFlags.RoadEdge | Game.Objects.PlacementFlags.RoadSide;
        }

        /// <summary>
        /// Wraps the application of snapping metadata to the target entity in a safe try-catch block.
        /// </summary>
        private void PrepareRuntimeStampPlacementMetadata(Entity targetEntity)
        {
            try
            {
                ApplyStampSnapMetadataToEntity(targetEntity);
                ApplyFlattenModeToEntity(targetEntity);
            }
            catch (Exception e)
            {
                ModRuntime.Warn($"PrepareRuntimeStampSnapMetadata error: {e.Message}");
            }
        }

        /// <summary>
        /// Applies the "flatten" placement mode to the stamp entity. When
        /// MertToolState.FlattenGeometryEnabled is on, forces GeometryFlags.HasBase onto the
        /// entity's ObjectGeometryData - vanilla's Game.Objects.ObjectUtils.AdjustPosition then
        /// places the whole object as a flat, untilted plane at the height of its locally
        /// highest terrain corner, instead of averaging + tilting it to match local slope
        /// (confirmed via decompiled AdjustPosition: without HasBase, it 4-corner-samples the
        /// object's ObjectGeometryData.m_Bounds and builds a LookRotationSafe tilt from the
        /// local gradient - the source of the "engebeli" look on grid/ring shapes). Off, the
        /// flag is cleared and vanilla's default terrain-adaptive tilt returns.
        /// </summary>
        private void ApplyFlattenModeToEntity(Entity targetEntity)
        {
            if (targetEntity == Entity.Null || !EntityManager.Exists(targetEntity)) return;

            if (!EntityManager.TryGetComponent(targetEntity, out ObjectGeometryData geometryData)) return;

            Game.Objects.GeometryFlags newFlags = MertToolState.FlattenGeometryEnabled
                ? geometryData.m_Flags | Game.Objects.GeometryFlags.HasBase
                : geometryData.m_Flags & ~Game.Objects.GeometryFlags.HasBase;

            if (newFlags == geometryData.m_Flags)
                return;

            geometryData.m_Flags = newFlags;
            EntityManager.SetComponentData(targetEntity, geometryData);

            bool verifyOk = EntityManager.TryGetComponent(targetEntity, out ObjectGeometryData verify) && verify.m_Flags == newFlags;
        }

        /// <summary>
        /// Applies detailed snapping metadata and placement flags to the ECS entity representing the stamp.
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

            placeable.m_Flags |= m_DesiredPlacementFlags;

            bool shouldTouchSnapMetadata = WritesSubNetSnapMetadata;

            bool isAnySnapActive = shouldTouchSnapMetadata && IsAnyGlobalSnapEnabled();

            if (shouldTouchSnapMetadata)
            {
                if (isAnySnapActive)
                    placeable.m_Flags |= Game.Objects.PlacementFlags.SubNetSnap;
                else
                    placeable.m_Flags &= ~Game.Objects.PlacementFlags.SubNetSnap;
            }

            if (oldFlags != placeable.m_Flags || changed)
            {
                EntityManager.SetComponentData(targetEntity, placeable);
                changed = true;
            }

            if (EntityManager.HasBuffer<Game.Prefabs.SubNet>(targetEntity))
            {
                bool2 dynamicSubNetSnapping = new(isAnySnapActive, isAnySnapActive);

                DynamicBuffer<Game.Prefabs.SubNet> subNets = EntityManager.GetBuffer<Game.Prefabs.SubNet>(targetEntity);

                CompositionFlags suppressionFlags = BuildCommonSuppressionFlags();

                for (int i = 0; i < subNets.Length; i++)
                {
                    Game.Prefabs.SubNet subNet = subNets[i];

                    if (shouldTouchSnapMetadata)
                    {
                        if (subNet.m_Snapping.x != dynamicSubNetSnapping.x ||
                            subNet.m_Snapping.y != dynamicSubNetSnapping.y)
                        {
                            subNet.m_Snapping = dynamicSubNetSnapping;
                            changed = true;
                        }
                    }

                    if (suppressionFlags != default)
                    {
                        subNet.m_Upgrades.m_Left |= suppressionFlags.m_Left;
                        subNet.m_Upgrades.m_Right |= suppressionFlags.m_Right;

                        changed = true;
                    }

                    subNets[i] = subNet;
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

            Entity entity = m_PrefabSystem.GetEntity(m_RuntimeStamp);

            if (entity != Entity.Null && EntityManager.Exists(entity))
            {
                m_PrefabSystem.UpdatePrefab(m_RuntimeStamp, entity);
            }

            MarkRuntimeStampChanged();
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
        /// Returns the distinct REAL net prefabs (actual selected roads) referenced by this
        /// tool's current runtime stamp's Game.Prefabs.SubNet buffer - i.e. exactly the
        /// prefab(s) whose NetGeometryData vanilla's ObjectToolBaseSystem.CreateDefinitionsJob.
        /// CreateSubNet reads to decide whether to terrain-sample each segment
        /// (NetUtils.AdjustPosition against m_TerrainHeightData - the source of the terrain-
        /// conforming look) or, when GeometryFlags.FlattenTerrain is set on that prefab's
        /// NetGeometryData, use our authored local curve heights as-is with no terrain lookup
        /// at all. NOT the same entity as our own AssetStampPrefab wrapper - GeometryFlags.
        /// FlattenTerrain lives on the real road prefab's NetGeometryData, one level down.
        /// Used by MertToolBoxFlattenTerrainSpoofSystem to know which real prefab(s) to
        /// temporarily flag for the current frame.
        /// </summary>
        internal bool TryGetActiveSubNetRoadPrefabs(NativeList<Entity> results)
        {
            if (m_RuntimeStamp == null) return false;

            if (!TryResolveRuntimeStampEntity(m_RuntimeStamp, out Entity stampEntity)) return false;

            if (!EntityManager.HasBuffer<Game.Prefabs.SubNet>(stampEntity)) return false;

            DynamicBuffer<Game.Prefabs.SubNet> subNets = EntityManager.GetBuffer<Game.Prefabs.SubNet>(stampEntity);
            for (int i = 0; i < subNets.Length; i++)
            {
                Entity prefab = subNets[i].m_Prefab;
                if (prefab != Entity.Null && !results.Contains(prefab))
                    results.Add(prefab);
            }

            return results.Length > 0;
        }

        /// <summary>
        /// Exposes the shared runtime stamp's own PREFAB entity (the one
        /// m_PrefabSystem/ObjectToolSystem use to spawn placed instances via
        /// PrefabRef.m_Prefab) - NOT a placed instance, NOT the real road sub-net prefabs
        /// (see TryGetActiveSubNetRoadPrefabs for those). Lets non-tool-specific systems
        /// (e.g. MertToolBoxTerrainFlattenSystem) recognize which placed Game.Objects.Transform
        /// entities are actual committed instances of THIS tool's stamp, by comparing their
        /// PrefabRef.m_Prefab against this value, without needing direct access to
        /// m_RuntimeStamp/m_PrefabSystem.
        /// </summary>
        internal bool TryGetRuntimeStampPrefabEntity(out Entity prefabEntity)
        {
            prefabEntity = Entity.Null;

            if (m_RuntimeStamp == null)
                return false;

            return TryResolveRuntimeStampEntity(m_RuntimeStamp, out prefabEntity);
        }

        /// <summary>
        /// Retrieves and updates the current entity representation of the given stamp prefab.
        /// </summary>
        private bool TryResolveRuntimeStampEntity(AssetStampPrefab stamp, out Entity entity)
        {
            entity = Entity.Null;

            if (stamp == null) return false;

            entity = m_PrefabSystem.GetEntity(stamp);

            return entity != Entity.Null &&
                   EntityManager.Exists(entity);
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

            PrepareRuntimeStampPlacementMetadata(refreshedEntity);

            AssetStampPrefab stampToHandOff = m_PendingHandoffStamp;

            if (stampToHandOff == null)
                return false;

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

            if (!TryResolveRuntimeStampEntity(m_PendingHandoffStamp, out refreshedEntity)) return false;

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
                LastHandoffSupportsFlatten = SupportsFlatten;
                LastHandoffElevation = GetCurrentNetToolElevation();

                bool toolSwitchNeeded = m_ToolSystem.activeTool != m_ObjectToolSystem;
                bool stampChanged = m_LastHandedOffStamp != stamp;
                bool geometryChanged = m_LastHandedOffRevision != m_RuntimeStampRevision;

                if (!toolSwitchNeeded && !stampChanged && !geometryChanged)
                    return;

                if (toolSwitchNeeded)
                {
                    m_ToolSystem.selected = Entity.Null;
                    m_ToolSystem.activeTool = m_ObjectToolSystem;
                }

                if (stampChanged || geometryChanged)
                {
                    ModRuntime.TrySetField(m_ObjectToolSystem, "m_SelectedPrefab", null);
                    ModRuntime.TrySetField(m_ObjectToolSystem, "m_Prefab", null);

                    bool setOk = m_ObjectToolSystem.TrySetPrefab(stamp);
                    if (!setOk)
                        return;

                    //m_ObjectToolSystem.InitializeRaycast();

                    m_LastHandedOffStamp = stamp;
                    m_LastHandedOffRevision = m_RuntimeStampRevision;
                }

                if (m_ObjectToolSystem.mode != ObjectToolSystem.Mode.Stamp)
                    m_ObjectToolSystem.mode = ObjectToolSystem.Mode.Stamp;

                if (OverridesObjectToolSnapMask)
                    ApplySnapMaskToActiveTool();

                // FLICKER FIX: only needed on an actual tool switch. Proven from the decompiled
                // ToolSystem.ToolUpdate(): the activeTool != m_LastTool check runs ONCE, at the
                // top of that method, BEFORE the nested SystemUpdatePhase.ToolUpdate dispatch
                // that our own OnUpdate() (and ObjectToolSystem's) live inside. We flip
                // activeTool from INSIDE that same nested dispatch, so ObjectToolSystem.Enabled
                // never gets set true this frame - the engine's own per-phase dispatch
                // deterministically skips it until next frame. That gap was the flicker.
                // On a stampChanged/geometryChanged-ONLY frame (no switch), ObjectToolSystem is
                // already the active/enabled tool, so the natural dispatch calls it later this
                // same frame regardless - forcing it here would fire Update() twice in one frame
                // and desync ObjectToolSystem's internal raycast state-diffing, which is why
                // this is guarded to only the switch frame.
                if (toolSwitchNeeded)
                    ForceCompleteObjectToolUpdate();
            }
            catch (Exception e)
            {
                ModRuntime.Warn($"HandoffToObjectTool error: {e}");
            }
        }

        /// <summary>
        /// Compensates for the one-frame ToolUpdate dispatch gap explained above by invoking
        /// the exact same PUBLIC API the engine itself uses when handing off the OUTGOING tool
        /// inside ToolSystem.ToolUpdate() (`this.m_LastTool.Update()`) - NOT reflection into the
        /// private OnUpdate(JobHandle) with a synthetic default(JobHandle). ComponentSystemBase
        /// .Update() reads/writes `this.Dependency` correctly on its own, so unlike a
        /// reflection-based version, this can't race against whatever job chain
        /// PrefabSystem/ReplacePrefabSystem left in flight for the freshly-mutated stamp.
        /// </summary>
        private void ForceCompleteObjectToolUpdate()
        {
            try
            {
                if (m_ObjectToolSystem == null)
                    return;

                m_ObjectToolSystem.Enabled = true;
                m_ObjectToolSystem.Update();
            }
            catch (Exception e)
            {
                ModRuntime.Warn("ForceCompleteObjectToolUpdate error: " + e.Message);
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Clears out any pending handoff flags and cached stamp data.
        /// </summary>
        private void ClearPendingHandoff()
        {
            m_PendingObjectToolHandoff = false;
            m_PendingHandoffStamp = null;
        }
        private void MarkRuntimeStampChanged()
        {
            m_RuntimeStampRevision++;
        }
        private bool IsAnyGlobalSnapEnabled()
        {
            return MertToolState.SnapGeometryEnabled;
        }
        #endregion
    }
}