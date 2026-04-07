using Colossal.Entities;
using Game;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem : GameSystemBase
    {
        #region Fields & Properties
        protected ToolSystem m_ToolSystem;
        protected ObjectToolSystem m_ObjectToolSystem;
        protected NetToolSystem m_NetToolSystem;
        protected PrefabSystem m_PrefabSystem;
        protected ToolRaycastSystem m_ToolRaycastSystem;

        protected static AssetStampPrefab s_SharedRuntimeStamp;
        protected static Entity s_SharedRuntimeStampEntity;
        protected static bool s_SharedStampRegistered;
        protected static bool s_PrebakeCompleted;
        protected static bool s_ObjectToolFoundationWarmed;

        protected AssetStampPrefab m_RuntimeStamp;
        public Entity RuntimeStampEntity { get; protected set; }

        protected NetPrefab m_LastUsedRoadPrefab;
        private AssetStampPrefab m_PendingHandoffStamp;
        protected double m_SuppressPlacementUntil;

        public bool ToolEnabled { get; protected set; }

        /// <summary>
        /// Indicates whether overlapping placement is permitted for the active tool.
        /// </summary>
        protected virtual bool AllowOverlapPlacement => false;

        /// <summary>
        /// Indicates whether this tool overrides global snap settings.
        /// </summary>
        protected virtual bool RequiresSnapEnforcement => false;

        protected bool m_PendingCreateShape;
        private bool m_IsCreatingShape;
        private bool m_PendingObjectToolHandoff;

        /// <summary>
        /// Provides the current real-time since startup as a double precision value.
        /// </summary>
        protected double RealtimeNow => UnityEngine.Time.realtimeSinceStartupAsDouble;

        protected bool m_ContextRecipeReady;
        protected bool m_ContextUsesRoadNode = false;

        protected Game.Objects.PlacementFlags m_DesiredPlacementFlags =
                    Game.Objects.PlacementFlags.OnGround |
                    Game.Objects.PlacementFlags.RoadEdge |
                    Game.Objects.PlacementFlags.RoadSide;
        #endregion

        #region Abstract Core
        /// <summary>
        /// Gets the specific name of the tool system.
        /// </summary>
        protected abstract string GetToolName();

        /// <summary>
        /// Processes custom inputs specific to the active tool implementation.
        /// </summary>
        protected abstract void ProcessToolInput();

        /// <summary>
        /// Attempts to generate the mathematical sub-networks and cells for the selected road prefab.
        /// </summary>
        protected abstract bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation);

        /// <summary>
        /// Triggered when the generated shape has been successfully placed in the game world.
        /// </summary>
        protected virtual void OnShapePlaced() { }

        /// <summary>
        /// Triggered when the custom tool is activated and becomes the primary selection.
        /// </summary>
        protected virtual void OnToolActivated() { }

        /// <summary>
        /// Triggered when the custom tool is deactivated or replaced by another tool.
        /// </summary>
        protected virtual void OnToolDeactivated() { }
        #endregion

        #region Lifecycle & Updates
        /// <summary>
        /// Initializes system references and binds event listeners when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_NetToolSystem = World.GetOrCreateSystemManaged<NetToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolRaycastSystem = World.GetOrCreateSystemManaged<ToolRaycastSystem>();

            Settings.OnOptionsChanged += OnSettingsChanged;
            PrebakeRuntimeStamp();
        }

        /// <summary>
        /// Executes the main logic loop including input processing, prebaking, and shape handoffs.
        /// </summary>
        protected override void OnUpdate()
        {
            if (!s_PrebakeCompleted)
                TryLatePrebakeWithRealRoad();
            if (!ToolEnabled)
                return;
            ProcessToolInput();
            CheckExitAndPlacementInputs();

            if (m_PendingObjectToolHandoff && HandlePendingObjectToolHandoff())
                return;

            if (m_PendingCreateShape)
                HandleExecuteCreateShape();
        }

        /// <summary>
        /// Cleans up memory allocations and unbinds event listeners when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            Settings.OnOptionsChanged -= OnSettingsChanged;
            if (s_SharedRuntimeStamp != null)
            {
                UnityEngine.Object.Destroy(s_SharedRuntimeStamp);
                s_SharedRuntimeStamp = null;
            }
            s_SharedStampRegistered = false;
            s_PrebakeCompleted = false;
            s_ObjectToolFoundationWarmed = false;
            m_ToolSystem = null;
            m_ObjectToolSystem = null;
            m_NetToolSystem = null;
            m_PrefabSystem = null;
            m_ToolRaycastSystem = null;
            m_RuntimeStamp = null;

            base.OnDestroy();
        }

        /// <summary>
        /// Handles dynamic updates to the tool state when user settings are modified.
        /// </summary>
        private void OnSettingsChanged()
        {
            if (!ToolEnabled) return;

            ApplySnapMaskToActiveTool();
            QueuePreviewRebuild();
        }
        #endregion

        #region State Management & Handoff
        /// <summary>
        /// Flags the system to rebuild the preview shape on the next update loop.
        /// </summary>
        public void QueuePreviewRebuild() { m_PendingCreateShape = true; }

        /// <summary>
        /// Attempts to mutate the runtime stamp with newly generated geometry and cost metadata.
        /// </summary>
        protected virtual bool TryMutateTargetStamp()
        {
            if (m_RuntimeStamp == null)
                return false;

            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            if (roadPrefab == null)
                return false;

            if (!TryGenerateGeometry(
                roadPrefab,
                out ObjectSubNetInfo[] generatedSubNets,
                out int widthCells,
                out int depthCells,
                out float costElevation))
            {
                return false;
            }

            m_RuntimeStamp.m_Width = math.max(4, widthCells);
            m_RuntimeStamp.m_Depth = math.max(4, depthCells);

            if (!m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets objectSubNets) || objectSubNets == null)
            {
                objectSubNets = m_RuntimeStamp.AddComponent<ObjectSubNets>();
            }

            objectSubNets.m_SubNets = generatedSubNets;

            ApplyCostMetadata(m_RuntimeStamp, generatedSubNets, roadPrefab, costElevation);

            m_RuntimeStamp.asset?.MarkDirty();
            m_LastUsedRoadPrefab = roadPrefab;

            return true;
        }

        /// <summary>
        /// Caches the current road and category context to memory prior to an interface handoff.
        /// </summary>
        protected void PrimeTabHandoffSourceContext()
        {
            NetPrefab road = GetCurrentRealRoadForTabHandoff();
            Entity category = GetCurrentRealCategoryForTabHandoff();

            MertToolState.PrimeTabHandoffSource(road, category);
        }
        #endregion

        #region Data & Prefab Retrieval
        /// <summary>
        /// Safely retrieves the currently selected road prefab from the toolbar UI.
        /// </summary>
        protected NetPrefab TryGetCurrentSelectedRoadPrefab()
        {
            try
            {
                var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
                if (toolbarUISystem != null && m_PrefabSystem != null)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var field = toolbarUISystem.GetType().GetField("m_SelectedAssetBinding", flags);
                    if (field != null)
                    {
                        var binding = field.GetValue(toolbarUISystem);
                        if (binding != null)
                        {
                            var valueProp = binding.GetType().GetProperty("value");
                            if (valueProp != null)
                            {
                                var entityObj = valueProp.GetValue(binding);
                                if (entityObj is Entity entity && entity != Entity.Null)
                                {
                                    if (m_PrefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefabBase) && prefabBase is NetPrefab netPrefab)
                                        return netPrefab;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return MertToolState.LastResolvedRoadPrefab;
        }

        /// <summary>
        /// Estimates the physical width of a given road prefab using its geometry data.
        /// </summary>
        protected float EstimateRoadWidth(NetPrefab roadPrefab)
        {
            if (roadPrefab == null) return 8f;
            Entity entity = m_PrefabSystem.GetEntity(roadPrefab);
            if (EntityManager.TryGetComponent(entity, out NetGeometryData geometryData) && geometryData.m_DefaultWidth > 0.1f)
                return geometryData.m_DefaultWidth;
            return 8f;
        }

        /// <summary>
        /// Retrieves the current elevation setting from the base network tool system.
        /// </summary>
        protected float GetCurrentNetToolElevation()
        {
            try { return m_NetToolSystem == null ? 0f : m_NetToolSystem.elevation; }
            catch { return 0f; }
        }

        /// <summary>
        /// Retrieves the currently selected road prefab specifically for UI synchronization purposes.
        /// </summary>
        public NetPrefab GetToolbarSelectedRoadPrefabForUiSync()
        {
            return TryGetCurrentSelectedRoadPrefab();
        }

        /// <summary>
        /// Gets the active road prefab or falls back to the last resolved road for seamless tab transitions.
        /// </summary>
        protected NetPrefab GetCurrentRealRoadForTabHandoff()
        {
            return TryGetCurrentSelectedRoadPrefab() ?? MertToolState.LastResolvedRoadPrefab;
        }

        /// <summary>
        /// Resolves the active category entity required for seamless UI tab handoffs.
        /// </summary>
        protected Entity GetCurrentRealCategoryForTabHandoff()
        {
            Entity category = GetCurrentlySelectedCategoryEntity();

            if (category == Entity.Null)
            {
                NetPrefab road = GetCurrentRealRoadForTabHandoff();
                category = ResolveCategoryFromRoadPrefab(road);
            }

            if (category == Entity.Null)
                category = MertToolState.LastResolvedCategory;

            return category;
        }
        #endregion

        #region Mathematical Utilities
        /// <summary>
        /// Safely cycles downwards through a predefined array of step indices, wrapping to the end.
        /// </summary>
        protected int CycleIndex<T>(int currentIndex, T[] steps)
        {
            if (steps == null || steps.Length == 0) return 0;
            int nextIndex = currentIndex - 1;
            return nextIndex < 0 ? steps.Length - 1 : nextIndex;
        }

        /// <summary>
        /// Retrieves the specific float value from an array using a clamped index.
        /// </summary>
        protected float GetCurrentStepValue(int currentIndex, float[] steps)
        {
            if (steps == null || steps.Length == 0) return 0f;
            return steps[math.clamp(currentIndex, 0, steps.Length - 1)];
        }

        /// <summary>
        /// Retrieves the specific integer value from an array using a clamped index.
        /// </summary>
        protected int GetCurrentStepValue(int currentIndex, int[] steps)
        {
            if (steps == null || steps.Length == 0) return 0;
            return steps[math.clamp(currentIndex, 0, steps.Length - 1)];
        }

        /// <summary>
        /// Calculates the next float value strictly aligned to the defined step grid.
        /// </summary>
        protected float GetNextStepAlignedValue(float currentValue, float stepSize, int direction)
        {
            if (stepSize <= 0f || direction == 0) return currentValue;
            const float epsilon = 0.0001f;
            if (direction > 0)
            {
                float next = math.floor(currentValue / stepSize) * stepSize + stepSize;
                if (next <= currentValue + epsilon) next += stepSize;
                return next;
            }
            else
            {
                float prev = math.ceil(currentValue / stepSize) * stepSize - stepSize;
                if (prev >= currentValue - epsilon) prev -= stepSize;
                return prev;
            }
        }

        /// <summary>
        /// Calculates the next integer value strictly aligned to the defined step grid.
        /// </summary>
        protected int GetNextStepAlignedInt(int currentValue, int stepSize, int direction)
        {
            if (stepSize <= 0 || direction == 0) return currentValue;
            if (direction > 0) return ((currentValue / stepSize) + 1) * stepSize;
            return ((currentValue - 1) / stepSize) * stepSize;
        }
        #endregion
    }
}