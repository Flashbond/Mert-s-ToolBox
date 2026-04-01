using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class SuperEllipseToolSystem : MertBaseToolSystem
    {
        #region 1. STATE & SETTINGS

        private int m_CurrentSessionWidth = -1;
        private readonly int[] m_WidthSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentWidthStepIndex = 3;

        private int m_CurrentSessionLength = -1;
        private readonly int[] m_LengthSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentLengthStepIndex = 3;

        private float m_N = 2.0f;
        private bool m_IsSubtractEnabled = false;

        #endregion

        #region 2. PENDING ACTIONS (MAILBOX)

        private int m_PendingWidthChange = 0;
        private bool m_PendingWidthStepCycle = false;

        private int m_PendingLengthChange = 0;
        private bool m_PendingLengthStepCycle = false;

        private float m_PendingNSliderChange = 0f;
        private string m_PendingSnapToggle = null;
        private bool m_PendingSubtractToggle = false;

        #endregion

        #region 3. PROPERTIES & CORE OVERRIDES

        /// <summary>
        /// Gets the internal name of the tool.
        /// </summary>
        protected override string GetToolName() => "SuperEllipse";

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
        /// Queues a change in the shape's width based on the given direction.
        /// </summary>
        public void QueueWidthChange(int direction) => m_PendingWidthChange += direction;

        /// <summary>
        /// Queues a cycle to the next width step size.
        /// </summary>
        public void QueueWidthStepCycle() => m_PendingWidthStepCycle = true;

        /// <summary>
        /// Queues a change in the shape's length based on the given direction.
        /// </summary>
        public void QueueLengthChange(int direction) => m_PendingLengthChange += direction;

        /// <summary>
        /// Queues a cycle to the next length step size.
        /// </summary>
        public void QueueLengthStepCycle() => m_PendingLengthStepCycle = true;

        /// <summary>
        /// Queues a change in the N-value (curvature) slider based on user input.
        /// </summary>
        public void QueueNChange(float sliderDirection) => m_PendingNSliderChange += sliderDirection;

        /// <summary>
        /// Sets the N slider directly from the UI using an absolute slider value.
        /// </summary>
        public void SetNFromUi(float absoluteSliderValue) => SetCurrentNSlider(absoluteSliderValue);

        #endregion

        #region 5. GETTERS (UI BINDING)
        /// <summary>
        /// Gets the index of the current width step size.
        /// </summary>
        public int GetCurrentWidthStepIndex() => m_CurrentWidthStepIndex;

        /// <summary>
        /// Gets the actual value of the current width step size.
        /// </summary>
        public int GetWidthStepSize() => GetCurrentStepValue(m_CurrentWidthStepIndex, m_WidthSteps);
        /// <summary>
        /// Retrieves the current width, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentWidth()
        {
            if (m_CurrentSessionWidth < 0)
                m_CurrentSessionWidth = Mod.settings != null ? Mod.settings.DefaultEllipseWidth : 80;
            return m_CurrentSessionWidth;
        }
        /// <summary>
        /// Gets the index of the current length step size.
        /// </summary>
        public int GetCurrentLengthStepIndex() => m_CurrentLengthStepIndex;

        /// <summary>
        /// Gets the actual value of the current length step size.
        /// </summary>
        public int GetLengthStepSize() => GetCurrentStepValue(m_CurrentLengthStepIndex, m_LengthSteps);
        /// <summary>
        /// Retrieves the current length, initializing it to a default value if not already set.
        /// </summary>
        public int GetCurrentLength()
        {
            if (m_CurrentSessionLength < 0)
                m_CurrentSessionLength = Mod.settings != null ? Mod.settings.DefaultEllipseLength : 160;
            return m_CurrentSessionLength;
        }
        /// <summary>
        /// Retrieves the current N value.
        /// </summary>
        public float GetCurrentNSliderValue()
        {
            float currentKappa = 1.0f - (0.895f / m_N);
            return 1.0f + ((currentKappa - 0.105f) / 0.895f) * 14.0f;
        }

        #endregion

        #region 6. INPUT PROCESSING

        /// <summary>
        /// Processes pending UI actions and hardware inputs safely on the main thread.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            if (m_PendingWidthStepCycle) { m_CurrentWidthStepIndex = CycleIndex(m_CurrentWidthStepIndex, m_WidthSteps); m_PendingWidthStepCycle = false; }
            if (m_PendingLengthStepCycle) { m_CurrentLengthStepIndex = CycleIndex(m_CurrentLengthStepIndex, m_LengthSteps); m_PendingLengthStepCycle = false; }

            if (m_PendingWidthChange != 0) { ChangeWidth(m_PendingWidthChange); m_PendingWidthChange = 0; }
            if (m_PendingLengthChange != 0) { ChangeLength(m_PendingLengthChange); m_PendingLengthChange = 0; }

            if (math.abs(m_PendingNSliderChange) > 0.001f)
            {
                SetCurrentNSlider(GetCurrentNSliderValue() + m_PendingNSliderChange);
                m_PendingNSliderChange = 0f;
            }

            if (m_PendingSnapToggle != null) { ToggleSnap(m_PendingSnapToggle); m_PendingSnapToggle = null; }
            if (m_PendingSubtractToggle) { m_IsSubtractEnabled = !m_IsSubtractEnabled; m_PendingSubtractToggle = false; }

            if (Mod.settings != null &&
                 Mod.settings.UseCtrlWheelForShapeAdjustment &&
                 UnityEngine.InputSystem.Keyboard.current != null &&
                 UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0)
                {
                    SetCurrentNSlider(GetCurrentNSliderValue() + (scrollDir * 0.1f));
                }
            }
        }

        #endregion

        #region 7. VALUE MUTATION & CLAMPING

        /// <summary>
        /// Changes the current width by a multiple of the active step size.
        /// </summary>
        public void ChangeWidth(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentWidthStepIndex, m_WidthSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentWidth(), stepSize, direction);
            SetCurrentWidth(nextValue);
        }

        /// <summary>
        /// Sets the current width, clamping it to safe minimum bounds based on road width to prevent geometry collision.
        /// </summary>
        private void SetCurrentWidth(int width)
        {
            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            float roadWidth = roadPrefab != null ? EstimateRoadWidth(roadPrefab) : 8f;

            float safeMin = (roadWidth * 2f) + 8f;
            int dynamicMinBound = (int)math.ceil(safeMin / 8.0) * 8;

            int clamped = math.clamp(width, dynamicMinBound, 940);
            if (m_CurrentSessionWidth == clamped) return;

            m_CurrentSessionWidth = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Changes the current length by a multiple of the active step size.
        /// </summary>
        public void ChangeLength(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentLengthStepIndex, m_LengthSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentLength(), stepSize, direction);
            SetCurrentLength(nextValue);
        }

        /// <summary>
        /// Sets the current length, clamping it to safe minimum bounds to prevent geometry collision.
        /// </summary>
        private void SetCurrentLength(int length)
        {
            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            float roadWidth = roadPrefab != null ? EstimateRoadWidth(roadPrefab) : 8f;

            float safeMin = (roadWidth * 2f) + 8f;
            int dynamicMinBound = (int)math.ceil(safeMin / 8.0) * 8;

            int clamped = math.clamp(length, dynamicMinBound, 940);
            if (m_CurrentSessionLength == clamped) return;

            m_CurrentSessionLength = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Processes a requested UI slider value, converting it to the internal N-value (curvature factor) with safety limits and a magnetic snap for perfect circles.
        /// </summary>
        private void SetCurrentNSlider(float targetSlider)
        {
            float nextSlider = targetSlider;

            if (math.abs(nextSlider - 8.0f) < 0.05f) nextSlider = 8.0f;

            nextSlider = math.clamp(nextSlider, 1.0f, 15.0f);

            float currentSlider = GetCurrentNSliderValue();
            if (math.abs(currentSlider - nextSlider) < 0.01f) return;

            float nextKappa = 0.105f + ((nextSlider - 1.0f) / 14.0f) * 0.895f;
            nextKappa = math.min(nextKappa, 0.999f);

            m_N = 0.895f / (1.0f - nextKappa);

            QueuePreviewRebuild();
        }

        #endregion

        #region 8. SHAPE GENERATION (STAMP)

        /// <summary>
        /// Attempts to mutate the target stamp by calculating geometry, sub-nets, and costs for the super-ellipse shape.
        /// </summary>
        protected override bool TryMutateTargetStamp()
        {
            if (!EnsureRuntimeStamp()) return false;

            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null) return false;

            float roadWidth = EstimateRoadWidth(roadPrefab);

            float buildRx = (m_CurrentSessionWidth - roadWidth) * 0.5f;
            float buildRy = (m_CurrentSessionLength - roadWidth) * 0.5f;

            if (buildRx < roadWidth || buildRy < roadWidth) return false;

            float baseElevation = GetCurrentNetToolElevation();

            ObjectSubNetInfo[] subNets = BuildSuperEllipseSubNets(roadPrefab, buildRx, buildRy, m_N, baseElevation);

            if (!m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets objectSubNets) || objectSubNets == null)
                objectSubNets = m_RuntimeStamp.AddComponent<ObjectSubNets>();

            objectSubNets.m_SubNets = subNets;

            int cellsX = (int)math.ceil(m_CurrentSessionWidth / 8f);
            int cellsY = (int)math.ceil(m_CurrentSessionLength / 8f);
            m_RuntimeStamp.m_Width = math.max(4, cellsX);
            m_RuntimeStamp.m_Depth = math.max(4, cellsY);

            if (!TryCalculateAndApplyCosts(m_RuntimeStamp, subNets, roadPrefab, baseElevation))
            {
                QueueDeferredCostResolve(m_RuntimeStamp, subNets, roadPrefab, baseElevation);
            }

            m_LastUsedRoadPrefab = roadPrefab;
            return true;
        }

        /// <summary>
        /// Generates the Bezier curves and tangent vectors required to form the super-ellipse based on the N-value.
        /// </summary>
        private ObjectSubNetInfo[] BuildSuperEllipseSubNets(NetPrefab roadPrefab, float a, float b, float n, float elevation)
        {
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[4];
            float kappa = 1.0f - (0.895f / n);

            float3[] points = new float3[4];
            points[0] = new float3(a, elevation, 0);
            points[1] = new float3(0, elevation, b);
            points[2] = new float3(-a, elevation, 0);
            points[3] = new float3(0, elevation, -b);

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                float3 p0 = points[i];
                float3 p3 = points[next];

                float3 t0 = float3.zero;
                float3 t1 = float3.zero;

                if (i == 0) { t0 = new float3(0, 0, b * kappa); t1 = new float3(a * kappa, 0, 0); }
                if (i == 1) { t0 = new float3(-a * kappa, 0, 0); t1 = new float3(0, 0, b * kappa); }
                if (i == 2) { t0 = new float3(0, 0, -b * kappa); t1 = new float3(-a * kappa, 0, 0); }
                if (i == 3) { t0 = new float3(a * kappa, 0, 0); t1 = new float3(0, 0, -b * kappa); }

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Bezier4x3(p0, p0 + t0, p3 + t1, p3),
                    m_NodeIndex = new int2(i, (i + 1) % 4),
                    m_ParentMesh = new int2(-1, -1)
                };
            }
            return result;
        }

        #endregion

        #region 9. SUBTRACT & PLACEMENT LOGIC

        /// <summary>
        /// Executes post-placement logic, triggering the terrain subtract manager with appropriate inner and outer bounds if enabled.
        /// </summary>
        protected override void OnShapePlaced()
        {
            if (!m_IsSubtractEnabled) return;
            if (!m_ToolRaycastSystem.GetRaycastResult(out var result)) return;

            float3 hitPos = result.m_Hit.m_HitPosition;

            NetPrefab roadPrefab = ResolveRoadPrefabForToolWork();
            if (roadPrefab == null) return;

            float roadWidth = EstimateRoadWidth(roadPrefab);

            float outerA = (m_CurrentSessionWidth - roadWidth) * 0.5f;
            float outerB = (m_CurrentSessionLength - roadWidth) * 0.5f;

            float innerA = outerA - (roadWidth * 0.75f);
            float innerB = outerB - (roadWidth * 0.75f);

            innerA = math.max(innerA, 1f);
            innerB = math.max(innerB, 1f);

            float nValue = m_N;
            float rotation = 0f;

            SubtractManager.Request(hitPos, rotation, outerA, outerB, innerA, innerB, nValue);
        }

        #endregion
    }
}