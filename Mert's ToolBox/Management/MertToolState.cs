using Game.Prefabs;
using Game.Tools;
using Unity.Entities;

namespace MertsToolBox.Management
{
    public enum ToolExitMode
    {
        None,
        UserSelectionClose,
        RestoreFromPlacement,
        VanillaToolbarClear
    }

    public static class MertToolState
    {
        public static MertBaseToolSystem ActiveTool { get; set; }

        public static NetPrefab LastResolvedRoadPrefab { get; set; }
        public static Entity LastResolvedCategory { get; set; } = Entity.Null;

        public static NetPrefab LaunchRoadPrefab { get; set; }
        public static Entity LaunchCategory { get; set; } = Entity.Null;

        public static bool ControlledSelectAssetReplay { get; set; }
        public static bool ControlledSelectCategoryReplay { get; set; }
        public static bool ControlledClearSelectionReplay { get; set; }
        public static bool SuppressToolbarCaptureDuringColdstart { get; set; }
        public static bool HasReleasedStaleObjectToolThisFrame { get; set; }

        #region System States
        public static bool HelixCleanupRequested { get; set; } = false;
        public static bool ActiveHelixUsesPierLikePrefab;
        public static bool SuppressCrosswalks { get; set; }
        public static bool SuppressTrafficLights { get; set; }
        public static float ActiveHelixBaseElevation = 0f;
        public static float ActiveHelixClearance =0f;
        #endregion
        #region Flatten States
        /// <summary>The flatten-capable tool that currently owns the session (null when none).</summary>
        public static MertBaseToolSystem FlattenOwner { get; set; }

        /// <summary>True while the flatten system still has work in flight (bake, overlay, preview or splat refresh).</summary>
        public static bool FlattenBusy { get; set; }

        public static bool FlattenToolActive => FlattenOwner != null;
        #endregion
        #region Traffic Lights
        public static MertBaseToolSystem TrafficLightOwner { get; set; }
        public static bool TrafficLightToolActive => TrafficLightOwner != null;
        #endregion
        #region Sanp States
        public static bool SnapGeometryEnabled = true;
        public static bool FlattenGeometryEnabled = false;

        public static Snap BuildGlobalSnapMask()
        {
            Snap mask = Snap.NetArea | Snap.NetNode;

            if (SnapGeometryEnabled) mask |= Snap.ExistingGeometry;

            return mask;
        }
        #endregion
        public static void CaptureLaunchContext(
            NetPrefab road,
            Entity category)
        {
            LaunchRoadPrefab = road;
            LaunchCategory = category;
        }

        public static void CaptureResolvedRoadContext(
            NetPrefab road,
            Entity category)
        {
            if (road != null)
                LastResolvedRoadPrefab = road;

            if (category != Entity.Null)
                LastResolvedCategory = category;
        }

        public static void ClearLaunchContext()
        {
            LaunchRoadPrefab = null;
            LaunchCategory = Entity.Null;
        }

        public static void ClearControlledReplayFlags()
        {
            ControlledSelectAssetReplay = false;
            ControlledSelectCategoryReplay = false;
            ControlledClearSelectionReplay = false;
        }
    }
}