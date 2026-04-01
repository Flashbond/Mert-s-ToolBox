using Game.Tools;
using System;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region SNAP CONTROL

        /// <summary>Toggles the specified snapping state (Geometry, NetSide, or NetArea) and triggers a preview rebuild.</summary>
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

            ApplyRoadSnapState();
            QueuePreviewRebuild();
        }

        /// <summary>Constructs and returns the active snap mask bitfield based on the currently enabled snapping options.</summary>
        protected Snap BuildCurrentSnapMask()
        {
            Snap mask = Snap.None;

            if (m_SnapGeometryEnabled)
                mask |= Snap.ExistingGeometry;

            if (m_SnapNetSideEnabled)
                mask |= Snap.NetSide;

            if (m_SnapNetAreaEnabled)
                mask |= Snap.NetArea | Snap.NetNode;

            return mask;
        }

        /// <summary>Applies the current snap mask to the active tool, forcefully updating private states if the object tool is active.</summary>
        protected void ApplyRoadSnapState()
        {
            try
            {
                if (m_ToolSystem?.activeTool == null)
                    return;

                Snap applied = m_RoadSnapEnabled ? BuildCurrentSnapMask() : Snap.None;

                // ObjectTool aktifse hem public hem private state'i zorla güncelle.
                if (m_ToolSystem.activeTool == m_ObjectToolSystem)
                {
                    m_ObjectToolSystem.selectedSnap = applied;

                    SetObjectToolPrivateField("m_SelectedSnap", applied);
                    SetObjectToolPrivateField("m_State", ObjectToolSystem.State.Default);
                    SetObjectToolPrivateField("m_ForceUpdate", true);

                    InvokeObjectToolPrivateMethod("InitializeRaycast");
                    return;
                }

                // NetTool / diğer toollar için sadece public selectedSnap yeterli.
                if (m_ToolSystem.activeTool.selectedSnap != applied)
                {
                    m_ToolSystem.activeTool.selectedSnap = applied;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] ApplyRoadSnapState error: {e.Message}");
            }
        }

        /// <summary>Continuously monitors and enforces snap metadata on the stamp entity, correcting any internal tool mismatches.</summary>
        private void EnforceRuntimeStampSnapMetadata()
        {
            try
            {
                if (!ToolEnabled || RuntimeStampEntity == Entity.Null || !EntityManager.Exists(RuntimeStampEntity))
                    return;

                bool changed = false;

                changed |= ApplyStampSnapMetadataToEntity(RuntimeStampEntity);

                bool toolSnapMismatch = false;

                if (m_ToolSystem.activeTool == m_ObjectToolSystem)
                {
                    Snap expectedSnap = m_RoadSnapEnabled ? BuildCurrentSnapMask() : Snap.None;
                    if (m_ObjectToolSystem.selectedSnap != expectedSnap)
                    {
                        toolSnapMismatch = true;
                    }
                }

                if ((changed || toolSnapMismatch) && m_ToolSystem.activeTool == m_ObjectToolSystem)
                {
                    Snap applied = m_RoadSnapEnabled ? BuildCurrentSnapMask() : Snap.None;

                    m_ObjectToolSystem.selectedSnap = applied;
                    SetObjectToolPrivateField("m_SelectedSnap", applied);
                    SetObjectToolPrivateField("m_ForceUpdate", true);

                    InvokeObjectToolPrivateMethod("InitializeRaycast");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] EnforceRuntimeStampSnapMetadata error: {e.Message}");
            }
        }

        /// <summary>Reverts the snap mask back to its originally stored state for the default or network tool upon exit.</summary>
        private void RestoreSnapState()
        {
            try
            {
                if (!m_HasStoredSnapMask || m_ToolSystem?.activeTool == null)
                    return;

                // Restore sadece normal tool tarafında anlamlı
                if (m_ToolSystem.activeTool == m_NetToolSystem ||
                    m_ToolSystem.activeTool.GetType().Name == "DefaultToolSystem")
                {
                    m_ToolSystem.activeTool.selectedSnap = m_StoredSnapMask;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] RestoreSnapState error: {e.Message}");
            }
            finally
            {
                m_HasStoredSnapMask = false;
            }
        }

        #endregion
    }
}