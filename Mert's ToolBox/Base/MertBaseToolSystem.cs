using Colossal.Entities;
using Game;
using Game.Prefabs;
using Game.Tools;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.Utilities.Preset;
using MertsToolBox.Utilities.Undo;
using System.IO;
using System.Reflection;
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
        protected static bool s_SharedStampRegistered;

        protected AssetStampPrefab m_RuntimeStamp;

        protected FieldInfo m_SelectedPrefabField;
        protected FieldInfo m_PrefabField;

        protected NetPrefab m_LastUsedRoadPrefab;
        private AssetStampPrefab m_PendingHandoffStamp;
        protected double m_SuppressPlacementUntil;
        private AssetStampPrefab m_LastHandedOffStamp;

        private int m_LastHandedOffRevision = -1;
        private int m_RuntimeStampRevision;
        private bool m_ToolEnabled;
        public bool ToolEnabled
        {
            get => m_ToolEnabled;
            protected set
            {
                m_ToolEnabled = value;

                if (value)
                    MertToolState.FlattenOwner = SupportsFlatten ? this : null;
                else if (MertToolState.FlattenOwner == this)
                    MertToolState.FlattenOwner = null;
            }
        }
        public abstract string ToolId { get; }
        public abstract string ToolName { get; }

        /// <summary>
        /// Indicates whether this tool overrides global snap settings.
        /// </summary>
        ///
        protected bool m_ContextRecipeReady;

        protected Game.Objects.PlacementFlags m_DesiredPlacementFlags =
                    Game.Objects.PlacementFlags.RoadEdge |
                    Game.Objects.PlacementFlags.RoadSide;

        protected virtual bool RequiresSnapEnforcement => true;
        protected virtual bool OverridesObjectToolSnapMask => true;
        protected virtual bool WritesSubNetSnapMetadata => RequiresSnapEnforcement;
        protected virtual bool SuppressCrosswalks => MertToolState.SuppressCrosswalks;

        protected bool m_PendingCreateShape;
        private bool m_IsCreatingShape;
        private bool m_PendingObjectToolHandoff;

        protected bool m_LastOneWayEligible = false;

        /// <summary>Tum araclarin paylastigi runtime stamp (instance'siz erisim).</summary>
        internal static AssetStampPrefab SharedRuntimeStamp => s_SharedRuntimeStamp;

        /// <summary>
        /// ObjectToolSystem'e en son stamp'i veren aracin Flatten'i destekleyip desteklemedigi.
        /// Commit aninda MertToolState.ActiveTool null olabildigi icin handoff aninda yakalaniyor.
        /// </summary>
        internal static bool LastHandoffSupportsFlatten { get; private set; } = true;

        /// <summary>Handoff anindaki NetTool yuksekligi (0 = zeminde).</summary>
        internal static float LastHandoffElevation { get; private set; }

        /// <summary>Helix gibi bilincli olarak 3B olan araclar false dondurur.</summary>
        protected virtual bool SupportsFlatten => true;

        #endregion

        #region Abstract Core
        /// <summary>
        /// Processes custom inputs specific to the active tool implementation.
        /// </summary>
        protected abstract void ProcessToolInput();
        public virtual void QueueToggleMainDirection() { }
        public virtual bool GetMainDirectionState() => true;

        /// <summary>
        /// Attempts to generate the mathematical sub-networks and cells for the selected road prefab.
        /// </summary>
        protected abstract bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation);

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

            m_SelectedPrefabField = typeof(NetToolSystem).GetField("m_SelectedPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            m_PrefabField = typeof(NetToolSystem).GetField("m_Prefab", BindingFlags.Instance | BindingFlags.NonPublic);

            if (Mod.settings != null)
            {
                Mod.settings.OnToolParametersChanged += RetrieveParametersFromSettings;
                Mod.settings.OnSuppressCrosswalkChanged += OnSuppressCrosswalkSettingsChanged;
                OnSuppressCrosswalkSettingsChanged();
            }
        }

        /// <summary>
        /// Executes the main logic loop including input processing, prebaking, and shape handoffs.
        /// </summary>
        protected override void OnUpdate()
        {
            if (!ToolEnabled) return;

            KeepVanillaElevationDisabled();

            ProcessElevationInput();
            ProcessToolInput();
            CheckPlacementInputs();

            if (m_PendingObjectToolHandoff && HandlePendingObjectToolHandoff())
                return;

            if (m_PendingCreateShape)
                HandleExecuteCreateShape();
        }

        protected void OnSuppressCrosswalkSettingsChanged()
        {
            MertToolState.SuppressCrosswalks = Mod.settings?.SuppressCrosswalks ?? false;

            if (!ToolEnabled)
                return;

            QueuePreviewRebuild();
        }

        private void KeepVanillaElevationDisabled()
        {
            try
            {
                if (m_SourceElevationAction != null && m_SourceElevationAction.enabled)
                    m_SourceElevationAction.Disable();
            }
            catch { }
        }

        /// <summary>
        /// Cleans up memory allocations and unbinds event listeners when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            if (Mod.settings != null)
            {
                Mod.settings.OnToolParametersChanged -= RetrieveParametersFromSettings;
                Mod.settings.OnSuppressCrosswalkChanged -= OnSuppressCrosswalkSettingsChanged;
            }

            if (MertToolState.ActiveTool == this) MertToolState.ActiveTool = null;
            if (MertToolState.FlattenOwner == this) MertToolState.FlattenOwner = null;

            m_ToolSystem = null;
            m_ObjectToolSystem = null;
            m_NetToolSystem = null;
            m_PrefabSystem = null;
            m_ToolRaycastSystem = null;
            m_RuntimeStamp = null;

            RestoreVanillaElevation();

            base.OnDestroy();
        }

        protected virtual void RetrieveParametersFromSettings(int toolIndex, int paramIndex) { }
        #endregion

        #region State Management & Handoff
        /// <summary>
        /// Flags the system to rebuild the preview shape on the next update loop.
        /// </summary>
        public void QueuePreviewRebuild() { m_PendingCreateShape = true; }

        /// <summary>
        /// Toggles the global "flatten geometry" mode shared by every shape tool. When active,
        /// the placed stamp's ObjectGeometryData gets GeometryFlags.HasBase (see
        /// ApplyFlattenModeToEntity in the Stamp partial), which makes vanilla's own
        /// ObjectUtils.AdjustPosition place the whole shape as a flat, untilted plane at its
        /// locally highest terrain corner instead of tilting it to match local slope. Mirrors
        /// QueueSnapToggle's shape - adjust to match exactly if that one defers via a pending
        /// flag instead of toggling synchronously.
        /// </summary>
        public void QueueFlattenToggle()
        {
            MertToolState.FlattenGeometryEnabled = !MertToolState.FlattenGeometryEnabled;

            if (ToolEnabled)
                QueuePreviewRebuild();
        }

        public bool IsFlattenGeometryEnabled() => MertToolState.FlattenGeometryEnabled;

        /// <summary>
        /// Attempts to mutate the runtime stamp with newly generated geometry and cost metadata.
        /// </summary>
        protected virtual bool TryMutateTargetStamp()
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            if (roadPrefab == null) return false;

            if (!TryGetSharedRuntimeStamp(out var prebakedStamp)) return false;
            m_RuntimeStamp = prebakedStamp;

            if (!TryGenerateGeometry(
                roadPrefab,
                out ObjectSubNetInfo[] generatedSubNets,
                out int widthCells,
                out int depthCells,
                out float costElevation))
                return false;

            m_RuntimeStamp.m_Width = math.max(4, widthCells);
            m_RuntimeStamp.m_Depth = math.max(4, depthCells);

            if (!m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets objectSubNets) || objectSubNets == null)
                objectSubNets = m_RuntimeStamp.AddComponent<ObjectSubNets>();

            objectSubNets.m_SubNets = generatedSubNets;

            ApplyCostMetadata(m_RuntimeStamp, generatedSubNets, roadPrefab, costElevation);

            m_RuntimeStamp.asset?.MarkDirty();
            m_LastUsedRoadPrefab = roadPrefab;

            return true;
        }
        private bool TryGetSharedRuntimeStamp(out AssetStampPrefab stamp)
        {
            stamp = null;

            if (!EnsureSharedRuntimeStamp())
                return false;

            stamp = s_SharedRuntimeStamp;
            return true;
        }

        private bool EnsureSharedRuntimeStamp()
        {
            if (s_SharedRuntimeStamp != null)
                return true;

            s_SharedRuntimeStamp =
                CreateSharedRuntimeStampPrefab();

            return s_SharedRuntimeStamp != null;
        }
        protected CompositionFlags BuildCommonSuppressionFlags()
        {
            CompositionFlags flags = default;

            if (SuppressCrosswalks)
            {
                flags.m_Left |= CompositionFlags.Side.RemoveCrosswalk;
                flags.m_Right |= CompositionFlags.Side.RemoveCrosswalk;
            }

            return flags;
        }
        #endregion

        #region Data & Prefab Retrieval
        /// <summary>
        /// Estimates the physical width of a given road prefab using its geometry data.
        /// </summary>
        protected float EstimateRoadWidth(NetPrefab roadPrefab)
        {
            if (roadPrefab == null)
                return 8f;

            Entity entity = m_PrefabSystem.GetEntity(roadPrefab);

            if (EntityManager.TryGetComponent(entity, out NetGeometryData geometryData) &&
                geometryData.m_DefaultWidth > 0.1f)
            {
                return geometryData.m_DefaultWidth;
            }

            return EstimateRoadWidthFromComposite(roadPrefab);
        }
        protected float EstimateRoadWidthFromComposite(NetPrefab roadPrefab)
        {
            float estimatedWidth = 0f;

            if (roadPrefab is not NetGeometryPrefab geometryPrefab ||
                geometryPrefab.m_Sections == null)
            {
                return 8f;
            }

            foreach (var sectionInfo in geometryPrefab.m_Sections)
            {
                if (sectionInfo.m_Section?.m_Pieces == null)
                    continue;

                foreach (var pieceInfo in sectionInfo.m_Section.m_Pieces)
                {
                    if (pieceInfo.m_Piece == null)
                        continue;

                    estimatedWidth = math.max(
                        estimatedWidth,
                        pieceInfo.m_Piece.m_Width);
                }
            }

            return estimatedWidth > 0.1f
                ? estimatedWidth
                : 8f;
        }

        /// <summary>
        /// Retrieves the current elevation setting from the base network tool system.
        /// </summary>
        public float GetCurrentNetToolElevation()
        {
            try
            {
                float elevation = m_NetToolSystem == null ? 0f : m_NetToolSystem.elevation;
                return elevation;
            }
            catch { return 0f; }
        }

        public void QueueElevationChangeFromUi(int direction)
        {
            if (!ToolEnabled)
                return;

            RouteElevationToNetTool(direction);
        }

        private void RouteElevationToNetTool(int direction)
        {
            if (m_NetToolSystem == null || direction == 0)
                return;

            float before = m_NetToolSystem.elevation;

            if (direction > 0)
                m_NetToolSystem.ElevationUp();
            else
                m_NetToolSystem.ElevationDown();

            float after = m_NetToolSystem.elevation;

            if (math.abs(after - before) < 0.01f)
                return;

            if (TryMutateTargetStamp())
                QueuePreviewRebuild();
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
        public NetPrefab GetCurrentSelectedNetPrefabForUi()
        {
            return TryGetCurrentSelectedRoadPrefab();
        }
        public static bool IsTrackLikePrefab(NetPrefab prefab)
        {
            if (prefab == null)
                return false;

            string lower = prefab.name?.ToLowerInvariant() ?? string.Empty;

            bool isTrack = prefab is TrackPrefab;

            bool isTransport =
                lower.Contains("transport") ||
                lower.Contains("bus");

            return isTrack || isTransport;
        }
        public bool IsCurrentTrackLikePrefab()
        {
            NetPrefab prefab = TryGetCurrentSelectedRoadPrefab();
            return IsTrackLikePrefab(prefab);
        }

        public static bool IsPierLikePrefab(NetPrefab prefab)
        {
            if (prefab == null || string.IsNullOrEmpty(prefab.name))
                return false;

            string name = prefab.name.ToLowerInvariant();

            return name.Contains("pier");
        }
        protected bool IsCurrentPierLikePrefab()
        {
            NetPrefab current = TryGetCurrentSelectedRoadPrefab();

            return IsPierLikePrefab(current);
        }
        #endregion

        #region Mathematical Utilities
        /// <summary>
        /// Safely cycles downwards through a predefined array of step indices, wrapping to the end.
        /// </summary>
        protected int GetIndexFromValue<T>(T value, T[] steps, int currentIndex) where T : struct
        {
            if (steps == null) return currentIndex;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Equals(value)) return i;
            }
            return currentIndex;
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

        #region Preset Sytem
        public virtual MertToolPreset CreatePresetSnapshot()
        {
            return null;
        }

        public virtual void ApplyPresetSnapshot(MertToolPreset preset)
        {
        }
        protected static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unnamed";

            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char c in invalidChars)
            {
                raw = raw.Replace(c, '_');
            }

            // Optional cleanup polish
            raw = raw.Replace(" ", "_");

            // Collapse accidental duplicates
            while (raw.Contains("__"))
            {
                raw = raw.Replace("__", "_");
            }

            return raw.Trim('_');
        }
        #endregion

        #region Undo System
        private readonly MertToolUndoHistory m_UndoHistory = new();

        protected void ClearUndoHistory()
        {
            m_UndoHistory.Clear();
        }

        protected void RegisterUndoForButton()
        {
            m_UndoHistory.RegisterButton(CreateUndoSnapshot);
        }

        protected void RegisterUndoForWheel()
        {
            m_UndoHistory.RegisterWheel(CreateUndoSnapshot);
        }

        public void BeginSliderUndoTransaction()
        {
            m_UndoHistory.BeginSlider(CreateUndoSnapshot);
        }

        public void EndSliderUndoTransaction()
        {
            m_UndoHistory.EndSlider();
        }

        public void UndoToolParameter()
        {
            m_UndoHistory.Undo(CreateUndoSnapshot, ApplyUndoSnapshot);
        }

        public void RedoToolParameter()
        {
            m_UndoHistory.Redo(CreateUndoSnapshot, ApplyUndoSnapshot);
        }

        public virtual MertToolPreset CreateUndoSnapshot()
        {
            return CreatePresetSnapshot();
        }

        public virtual void ApplyUndoSnapshot(MertToolPreset snapshot)
        {
            ApplyPresetSnapshot(snapshot);
        }
        #endregion
        #region One-Way Pattern Base Logic

        /// <summary>
        /// Determines if the selected road is functionally a one-way street by examining its
        /// internal RoadData flags instead of brittle string-based name checks.
        /// </summary>
        public virtual bool IsCurrentPrefabValidForOneWayPattern()
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            if (roadPrefab == null) return false;

            string name = roadPrefab.name.ToLowerInvariant();
            if (name.Contains("bridge") ||
                name.Contains("quay") ||
                name.Contains("pedestrian") ||
                name.Contains("public transport") ||
                name.Contains("roundabout"))
            {
                return false;
            }

            Unity.Entities.Entity roadEntity = m_PrefabSystem.GetEntity(roadPrefab);
            if (roadEntity == Unity.Entities.Entity.Null) return false;

            var entityManager = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!entityManager.Exists(roadEntity)) return false;

            if (!entityManager.TryGetComponent<Game.Prefabs.RoadData>(roadEntity, out var roadData))
                return false;

            bool hasForward = (roadData.m_Flags & Game.Prefabs.RoadFlags.DefaultIsForward) != 0;
            bool hasBackward = (roadData.m_Flags & Game.Prefabs.RoadFlags.DefaultIsBackward) != 0;

            return hasForward ^ hasBackward;
        }

        protected void EnforceOneWayOnlyOptions()
        {
            bool isEligible = IsCurrentPrefabValidForOneWayPattern();

            if (isEligible == m_LastOneWayEligible)
                return;

            m_LastOneWayEligible = isEligible;

            if (!isEligible)
            {
                bool changed = ResetOneWaySpecificOptions();

                if (changed && ToolEnabled)
                    QueuePreviewRebuild();
            }
        }

        protected virtual bool ResetOneWaySpecificOptions()
        {
            return false;
        }
        #endregion
    }
}