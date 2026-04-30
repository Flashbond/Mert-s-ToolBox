using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace MertsToolBox.Management
{
    #region Enums
    public enum ToolExitMode
    {
        None,
        SilentTabClose,
        UserSelectionClose,
        RestoreFromEscape,
        RestoreFromPlacement
    }
    #endregion

    public static class MertToolState
    {
        #region Events & Global Context States
        public static Action<ToolExitMode> OnToolAbortedByUI;
        public static NetPrefab LastResolvedRoadPrefab { get; set; } = null;
        public static Entity LastResolvedCategory { get; set; } = Entity.Null;

        public static NetPrefab LaunchRoadPrefab { get; set; } = null;
        public static Entity LaunchCategory { get; set; } = Entity.Null;

        public static NetPrefab LiveUiRoadPrefab { get; set; } = null;
        public static Entity LiveUiCategory { get; set; } = Entity.Null;

        private static readonly Dictionary<Entity, NetPrefab> s_LastRoadPerCategory = new();
        public static MertBaseToolSystem ActiveTool { get; set; } = null;
        public static bool IsMertToolActive =>
            ActiveTool != null && ActiveTool.ToolEnabled;
        #endregion

        #region Suppression & Control Flags
        public static bool HelixCleanupRequested { get; set; } = false;
        public static bool BlockRoadPrefabFallbackUntilNextRealSelection { get; set; } = false;
        public static bool SuppressUiMemoryCapture { get; set; } = false;
        public static bool SuppressCategoryCapture { get; set; } = false;
        public static bool HasReleasedStaleObjectToolThisFrame { get; set; } = false;
        public static bool UserJustChangedAssetCategory { get; set; } = false;
        public static bool SuppressUiAbortDuringRestore { get; set; } = false;
        public static bool SuppressLiveUiCapture { get; set; } = false;
        public static bool SuppressToolChangedDuringColdstart { get; set; } = false;
        public static bool SuppressToolbarCaptureDuringColdstart { get; set; } = false;
        #endregion

        #region Handoff & Restore States
        public static bool TabHandoffActive { get; set; } = false;
        public static NetPrefab TabHandoffFromRoad { get; set; } = null;
        public static Entity TabHandoffFromCategory { get; set; } = Entity.Null;
        public static Entity TabHandoffToCategory { get; set; } = Entity.Null;

        public static bool PendingRestore { get; private set; } = false;
        public static ToolExitMode PendingRestoreMode { get; private set; } = ToolExitMode.None;
        public static NetPrefab PendingRestoreRoad { get; private set; } = null;
        public static Entity PendingRestoreCategory { get; private set; } = Entity.Null;
        #endregion

        #region Context Management
        /// <summary>
        /// Caches the initial road and category context when a tool is launched.
        /// </summary>
        public static void CaptureLaunchContext(NetPrefab road, Entity category)
        {
            LaunchRoadPrefab = road;
            LaunchCategory = category;
        }

        /// <summary>
        /// Stores the most recently selected road prefab for a specific asset category.
        /// </summary>
        public static void RememberRoadForCategory(Entity category, NetPrefab road)
        {
            if (category == Entity.Null || road == null)
                return;

            s_LastRoadPerCategory[category] = road;
        }
        #endregion

        #region Restore Queue Management
        /// <summary>
        /// Queues a specific exit mode and context for restoration on the next update frame.
        /// </summary>
        public static void QueueRestore(ToolExitMode mode, NetPrefab road, Entity category)
        {
            PendingRestore = true;
            PendingRestoreMode = mode;
            PendingRestoreRoad = road;
            PendingRestoreCategory = category;
        }

        /// <summary>
        /// Clears any pending restore state and resets associated variables.
        /// </summary>
        public static void ClearPendingRestore()
        {
            PendingRestore = false;
            PendingRestoreMode = ToolExitMode.None;
            PendingRestoreRoad = null;
            PendingRestoreCategory = Entity.Null;
        }
        #endregion

        #region Tab Handoff Management
        /// <summary>
        /// Prepares the source context before initiating a UI tab handoff.
        /// </summary>
        public static void PrimeTabHandoffSource(NetPrefab fromRoad, Entity fromCategory)
        {
            TabHandoffFromRoad = fromRoad;
            TabHandoffFromCategory = fromCategory;
            TabHandoffToCategory = Entity.Null;
            TabHandoffActive = false;
        }

        /// <summary>
        /// Resets all variables related to the tab handoff process.
        /// </summary>
        public static void ClearTabHandoff()
        {
            TabHandoffActive = false;
            TabHandoffFromRoad = null;
            TabHandoffFromCategory = Entity.Null;
            TabHandoffToCategory = Entity.Null;
        }
        #endregion
    }
}