namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region UI State Management
        /// <summary>
        /// Enables or disables the tool state, triggering context captures, activations, or graceful exits accordingly.
        /// </summary>
        public virtual void SetToolState(bool isEnabled)
        {
            if (ToolEnabled == isEnabled) return;

            ToolEnabled = isEnabled;

            if (isEnabled)
            {
                CaptureLaunchRestoreContext();
                PrimeTabHandoffSourceContext();

                m_SuppressPlacementUntil = RealtimeNow + 0.06;
                OnToolActivated();
                PrimeAndShowPreviewOnEnable();
            }
            else
            {
                OnToolDeactivated();
                ExecuteGracefulExit(ToolExitMode.UserSelectionClose);
            }
        }

        /// <summary>
        /// Requests the active tool to disable and executes a graceful exit using the specified mode.
        /// </summary>
        public virtual void RequestDisable(ToolExitMode exitMode)
        {
            if (!ToolEnabled) return;

            OnToolDeactivated();
            ExecuteGracefulExit(exitMode);
        }
        #endregion
    }
}