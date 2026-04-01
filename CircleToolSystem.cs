using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class CircleToolSystem : MertBaseToolSystem
    {
        #region 1. STATE & SETTINGS

        private int m_CurrentSessionDiameter = -1;
        private readonly int[] m_DiameterSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentDiameterStepIndex = 3;
        private bool m_IsSubtractEnabled = false;
        #endregion

        #region 2. PENDING ACTIONS (MAILBOX)

        private int m_PendingDiameterChange = 0;
        private bool m_PendingDiameterStepCycle = false;
        private string m_PendingSnapToggle = null;
        private bool m_PendingSubtractToggle = false;

        #endregion

        #region 3. PROPERTIES & CORE OVERRIDES

        /// <summary>
        /// Gets the internal name of the tool.
        /// </summary>
        protected override string GetToolName() => "Circle";

        /// <summary>
        /// Determines if this tool requires continuous snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => true;

        #endregion

        #region 4. UI EVENT QUEUES (MAILBOX)

        /// <summary>
        /// Queues a toggle action for the subtract mode.
        /// </summary>
        public void QueueSubtractToggle() => m_PendingSubtractToggle = true;

        /// <summary>
        /// Checks if the subtract mode is currently enabled. Used for UI binding.
        /// </summary>
        public bool IsSubtractEnabled() => m_IsSubtractEnabled;

        /// <summary>
        /// Queues a toggle action for a specific snap type.
        /// </summary>
        public void QueueSnapToggle(string snapType) => m_PendingSnapToggle = snapType;

        /// <summary>
        /// Queues a change in the circle's diameter based on the given direction.
        /// </summary>
        public void QueueDiameterChange(int direction) => m_PendingDiameterChange += direction;

        /// <summary>
        /// Queues a cycle to the next diameter step size.
        /// </summary>
        public void QueueDiameterStepCycle() => m_PendingDiameterStepCycle = true;

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
                m_CurrentSessionDiameter = Mod.settings != null ? Mod.settings.DefaultCircleDiameter : 80;
            return m_CurrentSessionDiameter;
        }
        public CircleMetrics GetCurrentCircleMetrics()
        {
            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            float roadWidth = roadPrefab != null ? EstimateRoadWidth(roadPrefab) : 8f;
            return CircleMetrics.FromOuterDiameter(GetCurrentDiameter(), roadWidth);
        }
        #endregion

        #region 6. INPUT PROCESSING

        /// <summary>
        /// Processes pending UI actions and keyboard/mouse inputs for the tool.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            if (m_PendingDiameterChange != 0)
            {
                ChangeDiameter(m_PendingDiameterChange);
                m_PendingDiameterChange = 0;
            }

            if (m_PendingSnapToggle != null)
            {
                ToggleSnap(m_PendingSnapToggle);
                m_PendingSnapToggle = null;
            }
            
            if (m_PendingDiameterStepCycle)
            {
                m_CurrentDiameterStepIndex = CycleIndex(m_CurrentDiameterStepIndex, m_DiameterSteps);
                m_PendingDiameterStepCycle = false;
            }

            if (m_PendingSubtractToggle)
            {
                m_IsSubtractEnabled = !m_IsSubtractEnabled;
                m_PendingSubtractToggle = false;
            }

            if (Mod.settings != null &&
                Mod.settings.UseCtrlWheelForCircleDiameterAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0)
                {
                    int currentDiameter = GetCurrentDiameter();
                    SetCurrentDiameter(currentDiameter + scrollDir);
                }
            }
        }

        #endregion

        #region 7. SHAPE GENERATION (STAMP)

        /// <summary>
        /// Attempts to mutate the target stamp by calculating geometry, sub-nets, and costs for a circular road.
        /// </summary>
        protected override bool TryMutateTargetStamp()
        {
            if (!EnsureRuntimeStamp()) return false;

            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null) return false;

            float elevation = 0f;
            float roadWidth = EstimateRoadWidth(roadPrefab);
            int currentDiameter = GetCurrentDiameter();
            float buildRadius = (currentDiameter - roadWidth) * 0.5f;

            if (buildRadius <= 1f) return false;

            int autoSegments = CalculateAutoSegments(buildRadius);
            ObjectSubNetInfo[] circleSubNets = BuildCircleSubNets(roadPrefab, buildRadius, autoSegments, elevation);

            if (!m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets objectSubNets) || objectSubNets == null)
                objectSubNets = m_RuntimeStamp.AddComponent<ObjectSubNets>();

            objectSubNets.m_SubNets = circleSubNets;

            int cells = math.max(4, (int)math.ceil(currentDiameter / 8f));
            m_RuntimeStamp.m_Width = cells;
            m_RuntimeStamp.m_Depth = cells;

            float currentElevation = GetCurrentNetToolElevation();
            if (!TryCalculateAndApplyCosts(m_RuntimeStamp, circleSubNets, roadPrefab, currentElevation))
            {
               QueueDeferredCostResolve(m_RuntimeStamp, circleSubNets, roadPrefab, currentElevation);
            }

            m_RuntimeStamp.asset?.MarkDirty();
            m_LastUsedRoadPrefab = roadPrefab;

            return true;
        }

        /// <summary>
        /// Determines the optimal number of segments for the circle based on its radius.
        /// </summary>
        private int CalculateAutoSegments(float radius)
        {
            if (radius <= 24f) return 4;
            if (radius <= 40f) return 6;
            return 8;
        }

        /// <summary>
        /// Generates the Bezier curves and sub-net structures for the circular shape.
        /// </summary>
        private ObjectSubNetInfo[] BuildCircleSubNets(NetPrefab roadPrefab, float radius, int segments, float elevation)
        {
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[segments];
            float step = (math.PI * 2f) / segments;
            float tension = 4f / 3f;
            float tangentLength = radius * math.tan(step / 4f) * tension;

            float3[] points = new float3[segments];
            float3[] forwardTangents = new float3[segments];

            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                points[i] = new float3(math.cos(a) * radius, elevation, math.sin(a) * radius);
                forwardTangents[i] = new float3(-math.sin(a), 0f, math.cos(a));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                float3 p0 = points[i];
                float3 p3 = points[next];
                float3 t0 = forwardTangents[i] * tangentLength;
                float3 t1 = forwardTangents[next] * tangentLength;

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Bezier4x3(p0, p0 + t0, p3 - t1, p3),
                    m_NodeIndex = new int2(i, (i + 1) % segments),
                    m_ParentMesh = new int2(-1, -1),
                    m_Upgrades = null
                };
            }

            ApplyClosureNudge(result, tangentLength);
            return result;
        }

        /// <summary>
        /// Adjusts the tangents of the first and last segments to ensure a perfect closed loop.
        /// </summary>
        private void ApplyClosureNudge(ObjectSubNetInfo[] subNets, float tangentLength)
        {
            if (subNets == null || subNets.Length < 2) return;

            ref ObjectSubNetInfo firstInfo = ref subNets[0];
            ref ObjectSubNetInfo lastInfo = ref subNets[^1];

            float3 lockedStart = firstInfo.m_BezierCurve.a;
            float3 startDir = math.normalizesafe(firstInfo.m_BezierCurve.b - lockedStart, new float3(1, 0, 0));

            lastInfo.m_BezierCurve.d = lockedStart;
            lastInfo.m_BezierCurve.c = lockedStart - startDir * tangentLength;
            firstInfo.m_BezierCurve.b = lockedStart + startDir * tangentLength;
        }

        #endregion

        #region 8. DIAMETER MANAGEMENT
         /// <summary>
        /// Changes the current diameter by a multiple of the active step size.
        /// </summary>
        public void ChangeDiameter(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentDiameter(), stepSize, direction);
            SetCurrentDiameter(nextValue);
        }

        /// <summary>
        /// Sets the current diameter, clamping it strictly based on the physical road width (2x road width + 8m).
        /// </summary>
        private void SetCurrentDiameter(int diameter)
        {
            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            float roadWidth = roadPrefab != null ? EstimateRoadWidth(roadPrefab) : 8f;

            float safeMin = (roadWidth * 2f) + 8f;
            int dynamicMinBound = (int)math.ceil(safeMin / 8.0) * 8;
            
            int clamped = math.clamp(diameter, dynamicMinBound, 940);
            if (m_CurrentSessionDiameter == clamped) return;

            m_CurrentSessionDiameter = clamped;
            QueuePreviewRebuild();
        }

        #endregion

        #region 9. SUBTRACT & PLACEMENT LOGIC

        /// <summary>
        /// Executes post-placement logic, including triggering the terrain subtract manager if enabled.
        /// </summary>
        protected override void OnShapePlaced()
        {
            if (!m_IsSubtractEnabled) return;

            if (!m_ToolRaycastSystem.GetRaycastResult(out var result))
                return;

            float3 hitPos = result.m_Hit.m_HitPosition;

            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null)
                return;

            float roadWidth = EstimateRoadWidth(roadPrefab);

            float outerRadius = (GetCurrentDiameter() - roadWidth) * 0.5f;
            if (outerRadius <= 1f)
                return;

            float innerRadius = outerRadius - (roadWidth * 0.75f);
            innerRadius = math.max(innerRadius, 1f);

            float outerA = outerRadius;
            float outerB = outerRadius;
            float innerA = innerRadius;
            float innerB = innerRadius;
            float rotation = 0f;
            float nValue = 2f;

            SubtractManager.Request(hitPos, rotation, outerA, outerB, innerA, innerB, nValue);
        }

        #endregion
    }
}