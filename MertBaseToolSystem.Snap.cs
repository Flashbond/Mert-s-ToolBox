using Game.Tools;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region Fields & State
        protected bool m_SnapGeometryEnabled = true;
        protected bool m_SnapNetSideEnabled = false;
        protected bool m_SnapNetAreaEnabled = true;

        protected bool m_HasStoredSnapMask;
        protected Snap m_StoredSnapMask;
        #endregion

        #region State Retrieval
        /// <summary>
        /// Gets a value indicating whether geometry snapping is currently enabled.
        /// </summary>
        public bool IsSnapGeometryEnabled() => m_SnapGeometryEnabled;

        /// <summary>
        /// Gets a value indicating whether network side snapping is currently enabled.
        /// </summary>
        public bool IsSnapNetSideEnabled() => m_SnapNetSideEnabled;

        /// <summary>
        /// Gets a value indicating whether network area snapping is currently enabled.
        /// </summary>
        public bool IsSnapNetAreaEnabled() => m_SnapNetAreaEnabled;
        #endregion

        #region Input Queuing & Toggling
        /// <summary>
        /// Queues a toggle action for the specified snap type.
        /// </summary>
        public void QueueSnapToggle(string snapType) => ToggleSnap(snapType);

        /// <summary>
        /// Toggles the specified snap setting and applies the updated mask to the active tool.
        /// </summary>
        public void ToggleSnap(string snapType)
        {
            switch (snapType)
            {
                case "Geometry":
                    m_SnapGeometryEnabled = !m_SnapGeometryEnabled;
                    break;

                case "NetSide":
                    m_SnapNetSideEnabled = !m_SnapNetSideEnabled;
                    break;

                case "NetArea":
                    m_SnapNetAreaEnabled = !m_SnapNetAreaEnabled;
                    break;
            }

            ApplySnapMaskToActiveTool();

            if (ToolEnabled)
            {
                QueuePreviewRebuild();
            }
        }
        #endregion

        #region Mask Computation & Application
        /// <summary>
        /// Builds and returns the current combined snap mask based on active settings.
        /// </summary>
        protected Snap BuildCurrentSnapMask()
        {
            Snap mask = Snap.None;

            if (m_SnapGeometryEnabled) mask |= Snap.ExistingGeometry;
            if (m_SnapNetSideEnabled) mask |= Snap.NetSide;
            if (m_SnapNetAreaEnabled) mask |= Snap.NetArea | Snap.NetNode;

            return mask;
        }

        /// <summary>
        /// Determines the ultimate desired snap mask, considering whether the tool enforces snapping.
        /// </summary>
        private Snap GetDesiredSnapMask()
        {
            return RequiresSnapEnforcement ? BuildCurrentSnapMask() : Snap.None;
        }

        /// <summary>
        /// Applies the calculated snap mask directly to the active tool system.
        /// </summary>
        private void ApplySnapMaskToActiveTool()
        {
            if (m_ToolSystem?.activeTool == null)
                return;
            Snap targetSnap = GetDesiredSnapMask();

            if (m_ToolSystem.activeTool == m_ObjectToolSystem)
            {
                if (m_ObjectToolSystem.selectedSnap != targetSnap)
                {
                    m_ObjectToolSystem.selectedSnap = targetSnap;
                }

                SetObjectToolPrivateField("m_SelectedSnap", targetSnap);
                return;
            }

            if (m_ToolSystem.activeTool.selectedSnap != targetSnap)
            {
                m_ToolSystem.activeTool.selectedSnap = targetSnap;
            }
        }
        #endregion
    }
}