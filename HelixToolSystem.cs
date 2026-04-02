using Game.Prefabs;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class HelixToolSystem : MertBaseToolSystem
    {
        #region 1. STATE & SETTINGS

        private int m_CurrentSessionDiameter = -1;
        private readonly int[] m_DiameterSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentDiameterStepIndex = 3;

        private float m_CurrentSessionTurns = -1f;
        private readonly float[] m_TurnSteps = new float[] { 0.25f, 0.50f, 1f, 2f };
        private int m_CurrentTurnStepIndex = 3;

        private float m_CurrentSessionClearance = -1f;
        private readonly float[] m_ClearanceSteps = new float[] { 0.25f, 0.50f, 1f, 2f };
        private int m_CurrentClearanceStepIndex = 3;
        #endregion

        #region 2. PENDING ACTIONS (MAILBOX)

        private int m_PendingDiameterChange = 0;
        private bool m_PendingDiameterStepCycle = false;

        private int m_PendingTurnChange = 0;
        private bool m_PendingTurnStepCycle = false;

        private int m_PendingClearanceChange = 0;
        private bool m_PendingClearanceStepCycle = false;

        private string m_PendingSnapToggle = null;

        #endregion

        #region 3. PROPERTIES & CORE OVERRIDES

        /// <summary>
        /// Gets the internal name of the tool.
        /// </summary>
        protected override string GetToolName() => "Helix";
        /// <summary>
        /// Can overlap.
        /// </summary>
        protected override bool AllowOverlapPlacement => true;

        /// <summary>
        /// Determines if this tool requires continuous snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => true;

        #endregion

        #region 4. UI EVENT QUEUES (MAILBOX)

        /// <summary>
        /// Queues a toggle action for a specific snap type.
        /// </summary>
        public void QueueSnapToggle(string snapType) => m_PendingSnapToggle = snapType;

        /// <summary>
        /// Queues a change in the helix's diameter based on the given direction.
        /// </summary>
        public void QueueDiameterChange(int direction) => m_PendingDiameterChange += direction;

        /// <summary>
        /// Queues a cycle to the next diameter step size.
        /// </summary>
        public void QueueDiameterStepCycle() => m_PendingDiameterStepCycle = true;

        /// <summary>
        /// Queues a change in the number of vertical turns.
        /// </summary>
        public void QueueTurnChange(int direction) => m_PendingTurnChange += direction;

        /// <summary>
        /// Queues a cycle to the next turn step size.
        /// </summary>
        public void QueueTurnStepCycle() => m_PendingTurnStepCycle = true;

        /// <summary>
        /// Queues a change in the vertical clearance between turns.
        /// </summary>
        public void QueueClearanceChange(int direction) => m_PendingClearanceChange += direction;

        /// <summary>
        /// Queues a cycle to the next clearance step size.
        /// </summary>
        public void QueueClearanceStepCycle() => m_PendingClearanceStepCycle = true;

        #endregion

        #region 5. GETTERS (UI BINDING)

        /// <summary>
        /// Gets the index of the current diameter step size.
        /// </summary>
        public int GetCurrentDiameterStepIndex() => m_CurrentDiameterStepIndex;

        /// <summary>
        /// Gets the actual value of the current diameter step size.
        /// </summary>
        public int GetDiameterStepSize() => GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);

        /// <summary>
        /// Retrieves the current diameter, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentDiameter()
        {
            if (m_CurrentSessionDiameter < 0)
                m_CurrentSessionDiameter = Mod.settings != null ? Mod.settings.DefaultHelixDiameter : 80;
            return m_CurrentSessionDiameter;
        }

        /// <summary>
        /// Gets the index of the current turn step size.
        /// </summary>
        public int GetCurrentTurnStepIndex() => m_CurrentTurnStepIndex;

        /// <summary>
        /// Gets the actual value of the current turn step size.
        /// </summary>
        public float GetTurnStepSize() => GetCurrentStepValue(m_CurrentTurnStepIndex, m_TurnSteps);

        /// <summary>
        /// Retrieves the current turns, initializing it to a default value if not already set.
        /// </summary>
        public float GetCurrentTurns()
        {
            if (m_CurrentSessionTurns < 0)
                m_CurrentSessionTurns = Mod.settings != null ? Mod.settings.DefaultTurns : 3f;
            return m_CurrentSessionTurns;
        }

        /// <summary>
        /// Gets the index of the current clearance step size.
        /// </summary>
        public int GetCurrentClearanceStepIndex() => m_CurrentClearanceStepIndex;

        /// <summary>
        /// Gets the actual value of the current clearance step size.
        /// </summary>
        public float GetClearanceStepSize() => GetCurrentStepValue(m_CurrentClearanceStepIndex, m_ClearanceSteps);

        /// <summary>
        /// Retrieves the current clearance, initializing it to a default value if not already set.
        /// </summary>
        public float GetCurrentClearance()
        {
            if (m_CurrentSessionClearance < 0)
                m_CurrentSessionClearance = Mod.settings != null ? Mod.settings.DefaultClearance : 8f;
            return m_CurrentSessionClearance;
        }

        #endregion

        #region 6. LIFECYCLE CONTROLS

        /// <summary>
        /// Invoked when the tool is activated, triggering necessary state cleanups.
        /// </summary>
        protected override void OnToolActivated()
        {
            MertToolState.HelixCleanupRequested = true;
        }

        /// <summary>
        /// Invoked when the tool is deactivated, clearing pending cleanup requests.
        /// </summary>
        protected override void OnToolDeactivated()
        {
            MertToolState.HelixCleanupRequested = false;
        }

        /// <summary>
        /// Continuously enforces cleanup state while the tool remains active.
        /// </summary>
        protected override void OnToolTick()
        {
            if (ToolEnabled)
            {
                MertToolState.HelixCleanupRequested = true;
            }
        }

        #endregion

        #region 7. INPUT PROCESSING

        /// <summary>
        /// Processes pending UI actions and hardware inputs safely on the main thread.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            if (m_PendingDiameterStepCycle)
            {
                m_CurrentDiameterStepIndex = CycleIndex(m_CurrentDiameterStepIndex, m_DiameterSteps); 
                m_PendingDiameterStepCycle = false;
            }

            if (m_PendingTurnStepCycle)
            {
                m_CurrentTurnStepIndex = CycleIndex(m_CurrentTurnStepIndex, m_TurnSteps);
                m_PendingTurnStepCycle = false;
            }
            if (m_PendingClearanceStepCycle)
            {
                m_CurrentClearanceStepIndex = CycleIndex(m_CurrentClearanceStepIndex, m_ClearanceSteps);
                m_PendingClearanceStepCycle = false;
            }
            if (m_PendingDiameterChange != 0) { ChangeDiameter(m_PendingDiameterChange); m_PendingDiameterChange = 0; }
            if (m_PendingTurnChange != 0) { ChangeTurns(m_PendingTurnChange); m_PendingTurnChange = 0; }
            if (m_PendingClearanceChange != 0) { ChangeClearance(m_PendingClearanceChange); m_PendingClearanceChange = 0; }
            if (m_PendingSnapToggle != null) { ToggleSnap(m_PendingSnapToggle); m_PendingSnapToggle = null; }

            if (Mod.settings != null &&
                Mod.settings.UseCtrlWheelForHelixTurnAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0)
                {
                    SetCurrentTurns(GetCurrentTurns() + (scrollDir * 0.125f));
                }
            }
        }

        #endregion

        #region 8. VALUE MUTATION & CLAMPING

        /// <summary>
        /// Changes the current diameter by a multiple of the active step size, resolved via the base helper.
        /// </summary>
        public void ChangeDiameter(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentDiameter(), stepSize, direction);
            SetCurrentDiameter(nextValue);
        }

        /// <summary>
        /// Sets the current diameter, clamping it within bounds to prevent geometry collision and ceiling breach.
        /// </summary>
        private void SetCurrentDiameter(int diameter)
        {
            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            float roadWidth = roadPrefab != null ? EstimateRoadWidth(roadPrefab) : 8f;

            float collisionMin = (roadWidth * 2f) + 8f;

            float maxAllowedSlope = 0.25f;
            float slopeMin = (m_CurrentSessionClearance / (math.PI * maxAllowedSlope)) + roadWidth;

            float safeMin = math.max(collisionMin, slopeMin);
            int dynamicMinBound = (int)math.ceil(safeMin / 8.0) * 8;

            int clamped = math.clamp(diameter, dynamicMinBound, 940);
            if (m_CurrentSessionDiameter == clamped) return;

            m_CurrentSessionDiameter = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Changes the current number of turns based on the aligned step value.
        /// </summary>
        public void ChangeTurns(int direction)
        {
            float stepSize = GetCurrentStepValue(m_CurrentTurnStepIndex, m_TurnSteps);
            float nextValue = GetNextStepAlignedValue(GetCurrentTurns(), stepSize, direction);
            SetCurrentTurns(nextValue);
        }

        /// <summary>
        /// Sets the current number of turns, preventing it from exceeding the global altitude ceiling.
        /// </summary>
        private void SetCurrentTurns(float turns)
        {
            // Gerçekçi tavan: Maksimum 12 tam tur.
            float clamped = math.clamp(turns, 0.5f, 12f);

            if (math.abs(m_CurrentSessionTurns - clamped) < 0.01f) return;

            m_CurrentSessionTurns = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Changes the current clearance based on the aligned step value.
        /// </summary>
        public void ChangeClearance(int direction)
        {
            float stepSize = GetCurrentStepValue(m_CurrentClearanceStepIndex, m_ClearanceSteps);
            float nextValue = GetNextStepAlignedValue(GetCurrentClearance(), stepSize, direction);
            SetCurrentClearance(nextValue);
        }

        /// <summary>
        /// Sets the current clearance, clamping it based on the number of turns and the global ceiling limit.
        /// </summary>
        private void SetCurrentClearance(float clearance)
        {
            // Gerçekçi sınır: Minimum 6.5m (Tırların geçebilmesi için), Maksimum 15m.
            float clamped = math.clamp(clearance, 7.25f, 15f);

            if (math.abs(m_CurrentSessionClearance - clamped) < 0.01f) return;

            m_CurrentSessionClearance = clamped;
            QueuePreviewRebuild();
        }

        #endregion

        #region 9. SHAPE GENERATION (STAMP)

        /// <summary>
        /// Attempts to mutate the target stamp by calculating geometry, sub-nets, and 3D elevations for a helical structure.
        /// </summary>
        protected override bool TryMutateTargetStamp()
        {
            if (!EnsureRuntimeStamp()) return false;

            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null) return false;

            float roadWidth = EstimateRoadWidth(roadPrefab);

            float buildRadius = (m_CurrentSessionDiameter - roadWidth) * 0.5f;
            if (buildRadius * 2f < roadWidth * 2f + 8f) return false;

            int segments = (int)math.ceil(m_CurrentSessionTurns * 8f);
            float baseElevation = GetCurrentNetToolElevation();

            ObjectSubNetInfo[] subNets = BuildHelixSubNets(roadPrefab, buildRadius, segments, baseElevation, m_CurrentSessionClearance, m_CurrentSessionTurns);

            if (!m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets objectSubNets) || objectSubNets == null)
                objectSubNets = m_RuntimeStamp.AddComponent<ObjectSubNets>();

            objectSubNets.m_SubNets = subNets;

            int cells = (int)math.ceil(m_CurrentSessionDiameter / 8f);
            m_RuntimeStamp.m_Width = math.max(4, cells);
            m_RuntimeStamp.m_Depth = math.max(4, cells);

            float highestElevation = m_CurrentSessionClearance * m_CurrentSessionTurns;

            if (!TryCalculateAndApplyCosts(m_RuntimeStamp, subNets, roadPrefab, highestElevation))
            {
                QueueDeferredCostResolve(m_RuntimeStamp, subNets, roadPrefab, highestElevation);
            }

            m_RuntimeStamp.asset?.MarkDirty();

            m_LastUsedRoadPrefab = roadPrefab;
            return true;
        }

        /// <summary>
        /// Generates the 3D Bezier curves, ascending sub-nets, and tangent vectors for the helix shape.
        /// </summary>
        private ObjectSubNetInfo[] BuildHelixSubNets(NetPrefab roadPrefab, float radius, int segments, float startElevation, float clearance, float totalTurns)
        {
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[segments];

            float totalRadians = totalTurns * math.PI * 2f;
            float stepRadian = totalRadians / segments;
            float stepHeight = (clearance * totalTurns) / segments;

            float tension = 4f / 3f;
            float tangentLength = radius * math.tan(stepRadian / 4f) * tension;

            float3[] points = new float3[segments + 1];
            float3[] forwardTangents = new float3[segments + 1];

            float slope = clearance / (math.PI * 2f * radius);

            for (int i = 0; i <= segments; i++)
            {
                float a = i * stepRadian;
                float currentH = startElevation + (i * stepHeight);

                points[i] = new float3(math.cos(a) * radius, currentH, math.sin(a) * radius);

                float3 rawTangent = new(-math.sin(a), slope, math.cos(a));
                forwardTangents[i] = math.normalizesafe(rawTangent);
            }

            for (int i = 0; i < segments; i++)
            {
                float3 p0 = points[i];
                float3 p3 = points[i + 1];

                float3 t0 = forwardTangents[i] * tangentLength;
                float3 t1 = forwardTangents[i + 1] * tangentLength;

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Colossal.Mathematics.Bezier4x3(p0, p0 + t0, p3 - t1, p3),
                    m_NodeIndex = new int2(i, i + 1),
                    m_ParentMesh = new int2(-1, -1),
                    m_Upgrades = null
                };
            }

            return result;
        }

        #endregion
    }
}