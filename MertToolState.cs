using System;
using Game.Prefabs;
using Unity.Entities;

namespace MertsToolBox
{
    #region 1. TOOL EXIT MODES

    /// <summary>
    /// Defines the common language for tool exit and pipeline restoration scenarios.
    /// </summary>
    public enum ToolExitMode
    {
        None,
        SilentTabClose,
        UserSelectionClose,
        RestoreFromEscape,
        RestoreFromPlacement
    }

    #endregion

    /// <summary>
    /// A global state manager that acts as a blackboard between UI events, tool lifecycles, and memory restoration.
    /// </summary>
    public static class MertToolState
    {
        #region 2. GLOBAL EVENTS

        /// <summary>
        /// Triggered when the active tool is forcefully aborted by a UI interaction.
        /// </summary>
        public static Action<ToolExitMode> OnToolAbortedByUI;

        #endregion

        #region 3. TOOL SPECIFIC STATES

        /// <summary>
        /// Indicates if the Helix tool is currently active and requesting cleanup of overlap errors.
        /// </summary>
        public static bool HelixCleanupRequested { get; set; } = false;

        #endregion

        #region 4. MEMORY & RESTORATION

        /// <summary>
        /// Stores the last successfully resolved road prefab to fall back on when needed.
        /// </summary>
        public static NetPrefab LastResolvedRoadPrefab { get; set; } = null;

        /// <summary>
        /// Stores the last accessed asset category entity for seamless UI restoration.
        /// </summary>
        public static Entity LastResolvedCategory { get; set; } = Entity.Null;

        /// <summary>
        /// If true, prevents falling back to stored memory until the user makes a real prefab selection.
        /// </summary>
        public static bool BlockRoadPrefabFallbackUntilNextRealSelection { get; set; } = false;

        #endregion

        #region 5. UI SUPPRESSION SHIELDS

        /// <summary>
        /// Suppresses capturing the current UI state into memory (e.g., during programmatic selections).
        /// </summary>
        public static bool SuppressUiMemoryCapture { get; set; } = false;

        /// <summary>
        /// Suppresses capturing the current category into memory.
        /// </summary>
        public static bool SuppressCategoryCapture { get; set; } = false;

        /// <summary>
        /// Prevents UI abort events from triggering while the tool is restoring a previous state.
        /// </summary>
        public static bool SuppressUiAbortDuringRestore { get; set; } = false;

        /// <summary>
        /// Blocks the next auto-select operation in the UI.
        /// </summary>
        public static bool BlockNextAutoSelect { get; set; } = false;

        #endregion

        #region 6. FRAME TRANSIENT STATES

        /// <summary>
        /// Flag indicating if a stale object tool was released during the current frame.
        /// </summary>
        public static bool HasReleasedStaleObjectToolThisFrame { get; set; } = false;

        /// <summary>
        /// Flag indicating if the user has just changed the asset category in the UI.
        /// </summary>
        public static bool UserJustChangedAssetCategory { get; set; } = false;

        #endregion

    }
}