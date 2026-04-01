using Colossal.Mathematics;
using Game.Prefabs;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class GridToolSystem : MertBaseToolSystem
    {
        #region 1. STATE & SETTINGS

        private int m_CurrentSessionBlockWidthU = -1;
        private int m_CurrentSessionBlockLengthU = -1;
        private int m_CurrentSessionColumns = -1;
        private int m_CurrentSessionRows = -1;
        private bool m_IsAlternating = false;
        private bool m_IsOrientationLeftBottom = false;

        #endregion

        #region 2. PENDING ACTIONS (MAILBOX)

        private int m_PendingBlockWidthChange = 0;
        private int m_PendingBlockLengthChange = 0;
        private int m_PendingColsChange = 0;
        private int m_PendingRowsChange = 0;

        private bool m_PendingToggleAlternating = false;
        private bool m_PendingToggleOrientation = false;

        private string m_PendingSnapToggle = null;

        #endregion

        #region 3. PROPERTIES & CORE OVERRIDES

        /// <summary>
        /// Gets the internal name of the tool.
        /// </summary>
        protected override string GetToolName() => "Grid";

        /// <summary>
        /// Determines if this tool requires continuous snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => false;

        #endregion

        #region 4. UI EVENT QUEUES (MAILBOX)

        /// <summary>
        /// Queues a toggle action for a specific snap type.
        /// </summary>
        public void QueueSnapToggle(string snapType) => m_PendingSnapToggle = snapType;

        /// <summary>
        /// Queues a change in the block width based on the given direction.
        /// </summary>
        public void QueueBlockWidthChange(int direction) => m_PendingBlockWidthChange += direction;

        /// <summary>
        /// Queues a change in the block length based on the given direction.
        /// </summary>
        public void QueueBlockLengthChange(int direction) => m_PendingBlockLengthChange += direction;

        /// <summary>
        /// Queues a change in the number of columns.
        /// </summary>
        public void QueueColsChange(int direction) => m_PendingColsChange += direction;

        /// <summary>
        /// Queues a change in the number of rows.
        /// </summary>
        public void QueueRowsChange(int direction) => m_PendingRowsChange += direction;

        /// <summary>
        /// Queues a toggle action for the alternating one-way road direction.
        /// </summary>
        public void QueueToggleAlternating() => m_PendingToggleAlternating = true;

        /// <summary>
        /// Queues a toggle action to flip the starting orientation of alternating roads.
        /// </summary>
        public void QueueToggleOrientation() => m_PendingToggleOrientation = true;

        #endregion

        #region 5. GETTERS (UI BINDING)

        /// <summary>
        /// Retrieves the current block width in terms of cell unit, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentBlockWidthU()
        {
            if (m_CurrentSessionBlockWidthU < 0)
                m_CurrentSessionBlockWidthU = Mod.settings != null ? Mod.settings.BlockWidthU : 24;
            return m_CurrentSessionBlockWidthU;
        }

        /// <summary>
        /// Retrieves the current block length in terms of cell unit, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentBlockLengthU()
        {
            if (m_CurrentSessionBlockLengthU < 0)
                m_CurrentSessionBlockLengthU = Mod.settings != null ? Mod.settings.BlockLengthU : 24;
            return m_CurrentSessionBlockLengthU;
        }

        /// <summary>
        /// Retrieves the current number of columns in terms of cell unit, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentColumns()
        {
            if (m_CurrentSessionColumns < 0)
                m_CurrentSessionColumns = Mod.settings != null ? Mod.settings.Columns : 2;
            return m_CurrentSessionColumns;
        }

        /// <summary>
        /// Retrieves the current number of rows in terms of cell unit, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentRows()
        {
            if (m_CurrentSessionRows < 0)
                m_CurrentSessionRows = Mod.settings != null ? Mod.settings.Rows : 2;
            return m_CurrentSessionRows;
        }

        /// <summary>
        /// Checks if the alternating direction mode is enabled.
        /// </summary>
        public bool GetIsAlternating() => m_IsAlternating;

        /// <summary>
        /// Checks if the starting orientation for alternating roads is flipped.
        /// </summary>
        public bool GetIsOrientationLeftBottom() => m_IsOrientationLeftBottom;
        /// <summary>
        /// Evaluates if the currently selected prefab is a valid one-way road that supports alternating grid patterns.
        /// Applies strict blacklist filters to prevent invalid geometry.
        /// </summary>
        public bool IsCurrentPrefabValidForOneWayPattern()
        {
            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null) return false;

            Entity prefabEntity = m_PrefabSystem.GetEntity(roadPrefab);

            if (prefabEntity == Entity.Null || !EntityManager.HasComponent<Game.Prefabs.RoadData>(prefabEntity))
                return false;

            string name = roadPrefab.name.ToLower();

            if (name.Contains("bridge") || name.Contains("quay") || name.Contains("pedestrian") ||
                name.Contains("public transport") || name.Contains("alley") || name.Contains("gravel") ||
                name.Contains("dirt") || name.Contains("roundabout"))
            {
                return false;
            }

            return name.Contains("one-way") || name.Contains("oneway");
        }

        #endregion

        #region 6. INPUT PROCESSING

        /// <summary>
        /// Processes pending UI actions and hardware inputs safely on the main thread.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            if (m_PendingBlockWidthChange != 0) { ChangeBlockWidth(m_PendingBlockWidthChange); m_PendingBlockWidthChange = 0; }
            if (m_PendingBlockLengthChange != 0) { ChangeBlockLength(m_PendingBlockLengthChange); m_PendingBlockLengthChange = 0; }

            if (m_PendingColsChange != 0) { ChangeCols(m_PendingColsChange); m_PendingColsChange = 0; }
            if (m_PendingRowsChange != 0) { ChangeRows(m_PendingRowsChange); m_PendingRowsChange = 0; }

            if (m_PendingToggleAlternating) { m_IsAlternating = !m_IsAlternating; QueuePreviewRebuild(); m_PendingToggleAlternating = false; }
            if (m_PendingToggleOrientation) { m_IsOrientationLeftBottom = !m_IsOrientationLeftBottom; QueuePreviewRebuild(); m_PendingToggleOrientation = false; }

            if (m_PendingSnapToggle != null) { ToggleSnap(m_PendingSnapToggle); m_PendingSnapToggle = null; }
            if (m_PendingToggleAlternating)
            {
                if (IsCurrentPrefabValidForOneWayPattern()) m_IsAlternating = !m_IsAlternating;
                QueuePreviewRebuild();
                m_PendingToggleAlternating = false;
            }
            if (m_PendingToggleOrientation)
            {
                if (IsCurrentPrefabValidForOneWayPattern()) m_IsOrientationLeftBottom = !m_IsOrientationLeftBottom;
                QueuePreviewRebuild();
                m_PendingToggleOrientation = false;
            }
        }

        #endregion

        #region 7. VALUE MUTATION & CLAMPING

        /// <summary>
        /// Changes the block width, clamping it within allowed boundaries.
        /// </summary>
        private void ChangeBlockWidth(int direction)
        {
            int clamped = math.clamp(m_CurrentSessionBlockWidthU + direction, 2, 24);
            if (m_CurrentSessionBlockWidthU == clamped) return;
            m_CurrentSessionBlockWidthU = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Changes the block length, clamping it within allowed boundaries.
        /// </summary>
        private void ChangeBlockLength(int direction)
        {
            int clamped = math.clamp(m_CurrentSessionBlockLengthU + direction, 2, 24);
            if (m_CurrentSessionBlockLengthU == clamped) return;
            m_CurrentSessionBlockLengthU = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Changes the number of columns, clamping it to safe limits.
        /// </summary>
        private void ChangeCols(int direction)
        {
            int clamped = math.clamp(m_CurrentSessionColumns + direction, 1, 12);
            if (m_CurrentSessionColumns == clamped) return;
            m_CurrentSessionColumns = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Changes the number of rows, clamping it to safe limits.
        /// </summary>
        private void ChangeRows(int direction)
        {
            int clamped = math.clamp(m_CurrentSessionRows + direction, 1, 12);
            if (m_CurrentSessionRows == clamped) return;
            m_CurrentSessionRows = clamped;
            QueuePreviewRebuild();
        }

        #endregion

        #region 8. SHAPE GENERATION (STAMP)

        /// <summary>
        /// Attempts to mutate the target stamp by calculating geometry, node indices, sub-nets, and costs for a grid layout.
        /// </summary>
        protected override bool TryMutateTargetStamp()
        {
            if (!EnsureRuntimeStamp()) return false;

            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null) return false;

            float baseElevation = GetCurrentNetToolElevation();
            float roadWidth = EstimateRoadWidth(roadPrefab);

            float stepX = (m_CurrentSessionBlockWidthU * 8f) + roadWidth;
            float stepY = (m_CurrentSessionBlockLengthU * 8f) + roadWidth;

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
                if (m_IsAlternating)
                {
                    isForward = (i % 2 == 0);
                    if (m_IsOrientationLeftBottom) isForward = !isForward;
                }

                for (int j = 0; j < m_CurrentSessionRows; j++)
                {
                    float3 p1 = new(xs[i], baseElevation, ys[j]);
                    float3 p2 = new(xs[i], baseElevation, ys[j + 1]);

                    int n1 = GetNodeIndex(i, j);
                    int n2 = GetNodeIndex(i, j + 1);

                    float3 start = isForward ? p1 : p2;
                    float3 end = isForward ? p2 : p1;
                    int startNode = isForward ? n1 : n2;
                    int endNode = isForward ? n2 : n1;

                    segmentList.Add(CreateStraightSegment(roadPrefab, start, end, startNode, endNode));
                }
            }

            for (int j = 0; j <= m_CurrentSessionRows; j++)
            {
                bool isForward = true;
                if (m_IsAlternating)
                {
                    isForward = (j % 2 == 0);
                    if (m_IsOrientationLeftBottom) isForward = !isForward;
                }

                for (int i = 0; i < m_CurrentSessionColumns; i++)
                {
                    float3 p1 = new(xs[i], baseElevation, ys[j]);
                    float3 p2 = new(xs[i + 1], baseElevation, ys[j]);

                    int n1 = GetNodeIndex(i, j);
                    int n2 = GetNodeIndex(i + 1, j);

                    float3 start = isForward ? p1 : p2;
                    float3 end = isForward ? p2 : p1;
                    int startNode = isForward ? n1 : n2;
                    int endNode = isForward ? n2 : n1;

                    segmentList.Add(CreateStraightSegment(roadPrefab, start, end, startNode, endNode));
                }
            }

            if (!m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets objectSubNets))
            {
                objectSubNets = m_RuntimeStamp.AddComponent<ObjectSubNets>();
            }
            objectSubNets.m_SubNets = segmentList.ToArray();

            int cellsX = (int)math.ceil((m_CurrentSessionColumns * stepX) / 8f);
            int cellsY = (int)math.ceil((m_CurrentSessionRows * stepY) / 8f);
            m_RuntimeStamp.m_Width = math.max(4, cellsX);
            m_RuntimeStamp.m_Depth = math.max(4, cellsY);

            if (!TryCalculateAndApplyCosts(m_RuntimeStamp, objectSubNets.m_SubNets, roadPrefab, baseElevation))
            {
                QueueDeferredCostResolve(m_RuntimeStamp, objectSubNets.m_SubNets, roadPrefab, baseElevation);
            }

            m_RuntimeStamp.asset?.MarkDirty();

            m_LastUsedRoadPrefab = roadPrefab;
            return true;
        }

        /// <summary>
        /// Creates a straight road segment between two points with proper node indices for intersections.
        /// </summary>
        private ObjectSubNetInfo CreateStraightSegment(NetPrefab prefab, float3 start, float3 end, int startNode, int endNode)
        {
            float3 dir = end - start;
            float3 p1 = start + (dir * (1f / 3f));
            float3 p2 = end - (dir * (1f / 3f));

            return new ObjectSubNetInfo
            {
                m_NetPrefab = prefab,
                m_BezierCurve = new Bezier4x3(start, p1, p2, end),
                m_NodeIndex = new int2(startNode, endNode),
                m_ParentMesh = new int2(-1, -1)
            };
        }

        #endregion
    }
}