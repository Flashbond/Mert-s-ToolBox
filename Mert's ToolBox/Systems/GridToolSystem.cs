using Colossal.Entities;
using Colossal.Mathematics;
using Game.Prefabs;
using MertsToolBox.Core;
using System.Collections.Generic;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class GridToolSystem : MertBaseToolSystem
    {
        #region Fields & Properties
        private int m_CurrentSessionBlockWidthU = -1;
        private int m_CurrentSessionBlockLengthU = -1;
        private int m_CurrentSessionColumns = -1;
        private int m_CurrentSessionRows = -1;
        private bool m_IsAlternating = false;
        private bool m_IsOrientationLeftBottom = false;

        private int m_PendingBlockWidthChange = 0;
        private int m_PendingBlockLengthChange = 0;
        private int m_PendingColsChange = 0;
        private int m_PendingRowsChange = 0;
        private bool m_PendingToggleAlternating = false;
        private bool m_PendingToggleOrientation = false;

        private bool m_LastOneWayEligible = false;

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        public override string ToolId => "Grid";
        public override string ToolName => "Smart Grid";

        /// <summary>
        /// Indicates whether this tool requires snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => Mod.settings?.EnableGridSnap ?? false;
        protected override bool HandlesOwnElevationInput => true;
        #endregion

        #region Input Queuing % State
        protected override void OnSettingsChanged()
        {
            if (Mod.settings != null)
            {
                m_CurrentSessionBlockWidthU = Mod.settings.BlockWidthU;
                m_CurrentSessionBlockLengthU = Mod.settings.BlockLengthU;
                m_CurrentSessionColumns = Mod.settings.Columns;
                m_CurrentSessionRows = Mod.settings.Rows;
            }

            base.OnSettingsChanged();
        }
        /// <summary>
        /// Queues a change in the block width based on the given direction.
        /// </summary>
        public void QueueBlockWidthChange(int direction) => m_PendingBlockWidthChange += direction;

        /// <summary>
        /// Queues a change in the block length based on the given direction.
        /// </summary>
        public void QueueBlockLengthChange(int direction) => m_PendingBlockLengthChange += direction;

        /// <summary>
        /// Queues a change in the number of columns based on the given direction.
        /// </summary>
        public void QueueColsChange(int direction) => m_PendingColsChange += direction;

        /// <summary>
        /// Queues a change in the number of rows based on the given direction.
        /// </summary>
        public void QueueRowsChange(int direction) => m_PendingRowsChange += direction;

        /// <summary>
        /// Queues a toggle action for the alternating one-way road pattern.
        /// </summary>
        public void QueueToggleAlternating() => m_PendingToggleAlternating = true;

        /// <summary>
        /// Queues a toggle action for the one-way road orientation.
        /// </summary>
        public void QueueToggleOrientation() => m_PendingToggleOrientation = true;
        #endregion

        #region Metrics & State Retrieval
        /// <summary>
        /// Retrieves the current block width in units, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentBlockWidthU() { if (m_CurrentSessionBlockWidthU < 0) m_CurrentSessionBlockWidthU = Mod.settings != null ? Mod.settings.BlockWidthU : 6; return m_CurrentSessionBlockWidthU; }

        /// <summary>
        /// Retrieves the current block length in units, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentBlockLengthU() { if (m_CurrentSessionBlockLengthU < 0) m_CurrentSessionBlockLengthU = Mod.settings != null ? Mod.settings.BlockLengthU : 6; return m_CurrentSessionBlockLengthU; }

        /// <summary>
        /// Retrieves the current number of columns, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentColumns() { if (m_CurrentSessionColumns < 0) m_CurrentSessionColumns = Mod.settings != null ? Mod.settings.Columns : 2; return m_CurrentSessionColumns; }

        /// <summary>
        /// Retrieves the current number of rows, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentRows() { if (m_CurrentSessionRows < 0) m_CurrentSessionRows = Mod.settings != null ? Mod.settings.Rows : 2; return m_CurrentSessionRows; }

        /// <summary>
        /// Gets a value indicating whether the alternating road pattern is active.
        /// </summary>
        public bool GetIsAlternating() => m_IsAlternating;

        /// <summary>
        /// Gets a value indicating whether the road orientation starts from the left-bottom.
        /// </summary>
        public bool GetIsOrientationLeftBottom() => m_IsOrientationLeftBottom;

        /// <summary>
        /// Determines if the selected road is functionally a one-way street by examining its 
        /// internal RoadData flags instead of brittle string-based name checks.
        /// </summary>
        public bool IsCurrentPrefabValidForOneWayPattern()
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

        private void EnforceOneWayOnlyOptions()
        {
            bool isEligible = IsCurrentPrefabValidForOneWayPattern();

            if (isEligible == m_LastOneWayEligible)
                return;

            m_LastOneWayEligible = isEligible;

            if (!isEligible)
            {
                bool changed = false;

                if (m_IsAlternating)
                {
                    m_IsAlternating = false;
                    changed = true;
                }

                if (m_IsOrientationLeftBottom)
                {
                    m_IsOrientationLeftBottom = false;
                    changed = true;
                }

                if (changed && ToolEnabled)
                    QueuePreviewRebuild();
            }
        }
        #endregion

        #region Core Tool Processing
        /// <summary>
        /// Processes user inputs and applies queued changes to the grid tool state.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            EnforceOneWayOnlyOptions();

            if (m_PendingBlockWidthChange != 0) { ChangeBlockWidth(m_PendingBlockWidthChange); m_PendingBlockWidthChange = 0; }
            if (m_PendingBlockLengthChange != 0) { ChangeBlockLength(m_PendingBlockLengthChange); m_PendingBlockLengthChange = 0; }
            if (m_PendingColsChange != 0) { ChangeCols(m_PendingColsChange); m_PendingColsChange = 0; }
            if (m_PendingRowsChange != 0) { ChangeRows(m_PendingRowsChange); m_PendingRowsChange = 0; }

            if (m_PendingToggleAlternating)
            {
                if (IsCurrentPrefabValidForOneWayPattern()) ToggleAlternating();
                m_PendingToggleAlternating = false;
            }
            if (m_PendingToggleOrientation)
            {
                if (IsCurrentPrefabValidForOneWayPattern()) ToggleOrientation();
                m_PendingToggleOrientation = false;
            }
        }

        // --- PUBLIC CHANGE METHODS---
        public void ChangeBlockWidth(int direction)
        {
            int nextValue = GetCurrentBlockWidthU() + direction;
            SetCurrentBlockWidthU(nextValue);
        }

        public void ChangeBlockLength(int direction)
        {
            int nextValue = GetCurrentBlockLengthU() + direction;
            SetCurrentBlockLengthU(nextValue);
        }

        public void ChangeCols(int direction)
        {
            int nextValue = GetCurrentColumns() + direction;
            SetCurrentColumns(nextValue);
        }

        public void ChangeRows(int direction)
        {
            int nextValue = GetCurrentRows() + direction;
            SetCurrentRows(nextValue);
        }

        public void ToggleAlternating()
        {
            SetIsAlternating(!m_IsAlternating);
        }

        public void ToggleOrientation()
        {
            SetIsOrientationLeftBottom(!m_IsOrientationLeftBottom);
        }

        // --- PRIVATE SETTER METHODS  ---
        private void SetCurrentBlockWidthU(int value)
        {
            int clamped = math.clamp(value, 2, 24);
            if (m_CurrentSessionBlockWidthU == clamped) return;

            m_CurrentSessionBlockWidthU = clamped;
            QueuePreviewRebuild();
        }

        private void SetCurrentBlockLengthU(int value)
        {
            int clamped = math.clamp(value, 2, 24);
            if (m_CurrentSessionBlockLengthU == clamped) return;

            m_CurrentSessionBlockLengthU = clamped;
            QueuePreviewRebuild();
        }

        private void SetCurrentColumns(int value)
        {
            int clamped = math.clamp(value, 1, 12);
            if (m_CurrentSessionColumns == clamped) return;

            m_CurrentSessionColumns = clamped;
            QueuePreviewRebuild();
        }

        private void SetCurrentRows(int value)
        {
            int clamped = math.clamp(value, 1, 12);
            if (m_CurrentSessionRows == clamped) return;

            m_CurrentSessionRows = clamped;
            QueuePreviewRebuild();
        }

        private void SetIsAlternating(bool value)
        {
            if (m_IsAlternating == value) return;

            m_IsAlternating = value;
            QueuePreviewRebuild();
        }

        private void SetIsOrientationLeftBottom(bool value)
        {
            if (m_IsOrientationLeftBottom == value) return;

            m_IsOrientationLeftBottom = value;
            QueuePreviewRebuild();
        }
        #endregion

        #region Geometry Generation
        /// <summary>
        /// Attempts to generate the sub-networks and cells for the grid geometry.
        /// </summary>
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            float baseElevation = GetCurrentNetToolElevation();

            float stepX = (m_CurrentSessionBlockWidthU * 8f) + m_CurrentWidth;
            float stepY = (m_CurrentSessionBlockLengthU * 8f) + m_CurrentWidth;

            float originX = -0.5f * m_CurrentSessionColumns * stepX;
            float originY = -0.5f * m_CurrentSessionRows * stepY;

            float[] xs = new float[m_CurrentSessionColumns + 1];
            float[] ys = new float[m_CurrentSessionRows + 1];

            for (int i = 0; i <= m_CurrentSessionColumns; i++) xs[i] = originX + i * stepX;
            for (int j = 0; j <= m_CurrentSessionRows; j++) ys[j] = originY + j * stepY;

            List<ObjectSubNetInfo> segmentList = new();
            int GetNodeIndex(int c, int r) => r * (m_CurrentSessionColumns + 1) + c;

            for (int i = 0; i <= m_CurrentSessionColumns; i++)
            {
                bool isForward = true;
                if (m_IsAlternating) { isForward = (i % 2 == 0); if (m_IsOrientationLeftBottom) isForward = !isForward; }
                for (int j = 0; j < m_CurrentSessionRows; j++)
                {
                    float3 p1 = new(xs[i], baseElevation, ys[j]);
                    float3 p2 = new(xs[i], baseElevation, ys[j + 1]);
                    int n1 = GetNodeIndex(i, j); int n2 = GetNodeIndex(i, j + 1);
                    segmentList.Add(CreateStraightSegment(roadPrefab, isForward ? p1 : p2, isForward ? p2 : p1, isForward ? n1 : n2, isForward ? n2 : n1));
                }
            }

            for (int j = 0; j <= m_CurrentSessionRows; j++)
            {
                bool isForward = true;
                if (m_IsAlternating) { isForward = (j % 2 == 0); if (m_IsOrientationLeftBottom) isForward = !isForward; }
                for (int i = 0; i < m_CurrentSessionColumns; i++)
                {
                    float3 p1 = new(xs[i], baseElevation, ys[j]);
                    float3 p2 = new(xs[i + 1], baseElevation, ys[j]);
                    int n1 = GetNodeIndex(i, j); int n2 = GetNodeIndex(i + 1, j);
                    segmentList.Add(CreateStraightSegment(roadPrefab, isForward ? p1 : p2, isForward ? p2 : p1, isForward ? n1 : n2, isForward ? n2 : n1));
                }
            }

            subNets = segmentList.ToArray();
            widthCells = (int)math.ceil((m_CurrentSessionColumns * stepX) / 8f);
            depthCells = (int)math.ceil((m_CurrentSessionRows * stepY) / 8f);
            costElevation = baseElevation;

            return true;
        }

        /// <summary>
        /// Creates a straight sub-network segment between two points with proper node indexing.
        /// </summary>
        private ObjectSubNetInfo CreateStraightSegment(NetPrefab prefab, float3 start, float3 end, int startNode, int endNode)
        {
            float3 dir = end - start;
            return new ObjectSubNetInfo
            {
                m_NetPrefab = prefab,
                m_BezierCurve = new Bezier4x3(start, start + (dir * (1f / 3f)), end - (dir * (1f / 3f)), end),
                m_NodeIndex = new int2(startNode, endNode),
                m_ParentMesh = new int2(-1, -1)
            };
        }
        #endregion
    }
}