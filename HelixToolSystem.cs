using Game.Prefabs;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class HelixToolSystem : MertBaseToolSystem
    {
        #region Fields & Properties
        private int m_CurrentSessionDiameter = -1;
        private readonly int[] m_DiameterSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentDiameterStepIndex = 3;

        private float m_CurrentSessionTurns = -1f;
        private readonly float[] m_TurnSteps = new float[] { 0.25f, 0.50f, 1f, 2f };
        private int m_CurrentTurnStepIndex = 3;

        private float m_CurrentSessionClearance = -1f;
        private readonly float[] m_ClearanceSteps = new float[] { 0.25f, 0.50f, 1f, 2f };
        private int m_CurrentClearanceStepIndex = 3;

        private int m_PendingDiameterChange = 0;
        private bool m_PendingDiameterStepCycle = false;
        private int m_PendingTurnChange = 0;
        private bool m_PendingTurnStepCycle = false;
        private int m_PendingClearanceChange = 0;
        private bool m_PendingClearanceStepCycle = false;

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        protected override string GetToolName() => "Helix";

        /// <summary>
        /// Indicates whether this tool allows overlapping placements.
        /// </summary>
        protected override bool AllowOverlapPlacement => true;

        /// <summary>
        /// Indicates whether this tool requires snap enforcement based on user settings.
        /// </summary>
        protected override bool RequiresSnapEnforcement => Mod.settings != null && Mod.settings.EnableHelixSnap;
        #endregion

        #region Input Queuing
        /// <summary>
        /// Queues a change in the diameter based on the given direction.
        /// </summary>
        public void QueueDiameterChange(int direction) => m_PendingDiameterChange += direction;

        /// <summary>
        /// Queues a step cycle for the diameter adjustment.
        /// </summary>
        public void QueueDiameterStepCycle() => m_PendingDiameterStepCycle = true;

        /// <summary>
        /// Queues a change in the number of turns based on the given direction.
        /// </summary>
        public void QueueTurnChange(int direction) => m_PendingTurnChange += direction;

        /// <summary>
        /// Queues a step cycle for the turn adjustment.
        /// </summary>
        public void QueueTurnStepCycle() => m_PendingTurnStepCycle = true;

        /// <summary>
        /// Queues a change in the clearance based on the given direction.
        /// </summary>
        public void QueueClearanceChange(int direction) => m_PendingClearanceChange += direction;

        /// <summary>
        /// Queues a step cycle for the clearance adjustment.
        /// </summary>
        public void QueueClearanceStepCycle() => m_PendingClearanceStepCycle = true;
        #endregion

        #region Metrics & Data Retrieval
        /// <summary>
        /// Retrieves the current diameter step index.
        /// </summary>
        public int GetCurrentDiameterStepIndex() => m_CurrentDiameterStepIndex;

        /// <summary>
        /// Retrieves the current turn step index.
        /// </summary>
        public int GetCurrentTurnStepIndex() => m_CurrentTurnStepIndex;

        /// <summary>
        /// Retrieves the current clearance step index.
        /// </summary>
        public int GetCurrentClearanceStepIndex() => m_CurrentClearanceStepIndex;

        /// <summary>
        /// Retrieves the current session diameter, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentDiameter() { if (m_CurrentSessionDiameter < 0) m_CurrentSessionDiameter = Mod.settings != null ? Mod.settings.DefaultHelixDiameter : 96; return m_CurrentSessionDiameter; }

        /// <summary>
        /// Retrieves the current session turns, applying default settings if uninitialized.
        /// </summary>
        public float GetCurrentTurns() { if (m_CurrentSessionTurns < 0) m_CurrentSessionTurns = Mod.settings != null ? Mod.settings.DefaultTurns : 3f; return m_CurrentSessionTurns; }

        /// <summary>
        /// Retrieves the current session clearance, applying default settings if uninitialized.
        /// </summary>
        public float GetCurrentClearance() { if (m_CurrentSessionClearance < 0) m_CurrentSessionClearance = Mod.settings != null ? Mod.settings.DefaultClearance : 8f; return m_CurrentSessionClearance; }

        /// <summary>
        /// Calculates the minimum allowed diameter based on the road prefab width and clearance.
        /// </summary>
        private int GetMinimumAllowedDiameter(NetPrefab roadPrefab)
        {
            if (roadPrefab == null) return 48;

            float roadWidth = EstimateRoadWidth(roadPrefab);
            float collisionMin = roadWidth * 3f;

            float clearance = GetCurrentClearance();
            float slopeMin = (clearance / (math.PI * 0.25f)) + roadWidth;

            return (int)math.ceil(math.max(collisionMin, slopeMin));
        }
        #endregion

        #region Tool State & Lifecycle
        /// <summary>
        /// Called when the tool is activated to handle state cleanup or initialization.
        /// </summary>
        protected override void OnToolActivated() { MertToolState.HelixCleanupRequested = true; }

        /// <summary>
        /// Called when the tool is deactivated to perform necessary cleanup flags.
        /// </summary>
        protected override void OnToolDeactivated() { MertToolState.HelixCleanupRequested = false; }
        #endregion

        #region Core Tool Processing
        /// <summary>
        /// Processes user inputs and applies queued changes to the tool state.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            if (m_PendingDiameterStepCycle) { m_CurrentDiameterStepIndex = CycleIndex(m_CurrentDiameterStepIndex, m_DiameterSteps); m_PendingDiameterStepCycle = false; }
            if (m_PendingTurnStepCycle) { m_CurrentTurnStepIndex = CycleIndex(m_CurrentTurnStepIndex, m_TurnSteps); m_PendingTurnStepCycle = false; }
            if (m_PendingClearanceStepCycle) { m_CurrentClearanceStepIndex = CycleIndex(m_CurrentClearanceStepIndex, m_ClearanceSteps); m_PendingClearanceStepCycle = false; }

            if (m_PendingDiameterChange != 0) { ChangeDiameter(m_PendingDiameterChange); m_PendingDiameterChange = 0; }
            if (m_PendingTurnChange != 0) { ChangeTurns(m_PendingTurnChange); m_PendingTurnChange = 0; }
            if (m_PendingClearanceChange != 0) { ChangeClearance(m_PendingClearanceChange); m_PendingClearanceChange = 0; }

            if (Mod.settings != null && Mod.settings.UseCtrlWheelForHelixTurnAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0) SetCurrentTurns(GetCurrentTurns() + (scrollDir * 0.125f));
            }
        }

        /// <summary>
        /// Changes the diameter by aligning it to the next step value.
        /// </summary>
        public void ChangeDiameter(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentDiameter(), stepSize, direction);

            SetCurrentDiameter(nextValue);
        }

        /// <summary>
        /// Changes the number of turns based on the current step size and direction.
        /// </summary>
        public void ChangeTurns(int direction)
        {
            float stepSize = GetCurrentStepValue(m_CurrentTurnStepIndex, m_TurnSteps);
            SetCurrentTurns(GetNextStepAlignedValue(GetCurrentTurns(), stepSize, direction));
        }

        /// <summary>
        /// Changes the clearance based on the current step size and direction.
        /// </summary>
        public void ChangeClearance(int direction)
        {
            float stepSize = GetCurrentStepValue(m_CurrentClearanceStepIndex, m_ClearanceSteps);
            m_CurrentSessionClearance = math.clamp(GetNextStepAlignedValue(GetCurrentClearance(), stepSize, direction), 7.25f, 15f);
        }

        /// <summary>
        /// Safely sets the current diameter within legal bounds and queues a preview rebuild.
        /// </summary>
        private void SetCurrentDiameter(int diameter)
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            int dynamicMinBound = GetMinimumAllowedDiameter(roadPrefab);

            int clamped = math.clamp(diameter, dynamicMinBound, 940);
            if (m_CurrentSessionDiameter == clamped)
                return;

            m_CurrentSessionDiameter = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Clamps and applies the specified number of turns to the current session state.
        /// </summary>
        private void SetCurrentTurns(float turns) => m_CurrentSessionTurns = math.clamp(turns, 0.5f, 12f);
        #endregion

        #region Geometry Generation
        /// <summary>
        /// Attempts to generate the sub-networks and cells for the helix geometry.
        /// </summary>
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            int minAllowed = GetMinimumAllowedDiameter(roadPrefab);
            if (m_CurrentSessionDiameter < minAllowed)
            {
                m_CurrentSessionDiameter = minAllowed;
            }

            subNets = null; widthCells = 0; depthCells = 0; costElevation = 0f;

            float roadWidth = EstimateRoadWidth(roadPrefab);
            float buildRadius = (m_CurrentSessionDiameter - roadWidth) * 0.5f;

            if (buildRadius < roadWidth) return false;

            int segments = (int)math.ceil(m_CurrentSessionTurns * 8f);
            float baseElevation = GetCurrentNetToolElevation();

            subNets = BuildHelixSubNets(roadPrefab, buildRadius, segments, baseElevation, m_CurrentSessionClearance, m_CurrentSessionTurns);
            widthCells = depthCells = (int)math.ceil(m_CurrentSessionDiameter / 8f);
            costElevation = m_CurrentSessionClearance * m_CurrentSessionTurns;

            return true;
        }

        /// <summary>
        /// Builds the bezier curves representing the helix's sub-networks based on mathematical parameters.
        /// </summary>
        private ObjectSubNetInfo[] BuildHelixSubNets(NetPrefab roadPrefab, float radius, int segments, float startElevation, float clearance, float totalTurns)
        {
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[segments];
            float stepRadian = (totalTurns * math.PI * 2f) / segments;
            float stepHeight = (clearance * totalTurns) / segments;
            float tangentLength = radius * math.tan(stepRadian / 4f) * (4f / 3f);
            float slope = clearance / (math.PI * 2f * radius);

            float3[] points = new float3[segments + 1];
            float3[] forwardTangents = new float3[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float a = i * stepRadian;
                points[i] = new float3(math.cos(a) * radius, startElevation + (i * stepHeight), math.sin(a) * radius);
                forwardTangents[i] = math.normalizesafe(new float3(-math.sin(a), slope, math.cos(a)));
            }

            for (int i = 0; i < segments; i++)
            {
                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Colossal.Mathematics.Bezier4x3(points[i], points[i] + forwardTangents[i] * tangentLength, points[i + 1] - forwardTangents[i + 1] * tangentLength, points[i + 1]),
                    m_NodeIndex = new int2(i, i + 1),
                    m_ParentMesh = new int2(-1, -1)
                };
            }
            return result;
        }
        #endregion
    }
}