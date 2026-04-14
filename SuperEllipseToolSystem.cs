using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class SuperEllipseToolSystem : MertBaseToolSystem
    {
        #region Fields & Properties
        private int m_CurrentSessionWidth = -1;
        private readonly int[] m_WidthSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentWidthStepIndex = 3;

        private int m_CurrentSessionLength = -1;
        private readonly int[] m_LengthSteps = new int[] { 2, 4, 6, 8 };
        private int m_CurrentLengthStepIndex = 3;

        private float m_N = 2.0f;

        private int m_PendingWidthChange = 0;
        private bool m_PendingWidthStepCycle = false;
        private int m_PendingLengthChange = 0;
        private bool m_PendingLengthStepCycle = false;
        private float m_PendingNSliderChange = 0f;

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        protected override string GetToolName() => "SuperEllipse";

        /// <summary>
        /// Indicates whether this tool requires snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => true;
        #endregion

        #region Input Queuing
        /// <summary>
        /// Queues a change in the width based on the given direction.
        /// </summary>
        public void QueueWidthChange(int direction) => m_PendingWidthChange += direction;

        /// <summary>
        /// Queues a step cycle for the width adjustment.
        /// </summary>
        public void QueueWidthStepCycle() => m_PendingWidthStepCycle = true;

        /// <summary>
        /// Queues a change in the length based on the given direction.
        /// </summary>
        public void QueueLengthChange(int direction) => m_PendingLengthChange += direction;

        /// <summary>
        /// Queues a step cycle for the length adjustment.
        /// </summary>
        public void QueueLengthStepCycle() => m_PendingLengthStepCycle = true;

        /// <summary>
        /// Queues a change in the N (curvature) value based on the slider input.
        /// </summary>
        public void QueueNChange(float sliderDir) => m_PendingNSliderChange += sliderDir;

        /// <summary>
        /// Sets the current N value directly from the UI slider.
        /// </summary>
        public void SetNFromUi(float absVal) => SetCurrentNSlider(absVal);
        #endregion

        #region Metrics & Data Retrieval
        /// <summary>
        /// Retrieves the current width step index.
        /// </summary>
        public int GetCurrentWidthStepIndex() => m_CurrentWidthStepIndex;

        /// <summary>
        /// Retrieves the current length step index.
        /// </summary>
        public int GetCurrentLengthStepIndex() => m_CurrentLengthStepIndex;

        /// <summary>
        /// Retrieves the step size for width adjustments.
        /// </summary>
        public float GetWidthStepSize() => GetCurrentStepValue(m_CurrentWidthStepIndex, m_WidthSteps);

        /// <summary>
        /// Retrieves the step size for length adjustments.
        /// </summary>
        public float GetLengthStepSize() => GetCurrentStepValue(m_CurrentLengthStepIndex, m_LengthSteps);

        /// <summary>
        /// Retrieves the current session width, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentWidth() { if (m_CurrentSessionWidth < 0) m_CurrentSessionWidth = Mod.settings != null ? Mod.settings.DefaultEllipseWidth : 96; return m_CurrentSessionWidth; }

        /// <summary>
        /// Retrieves the current session length, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentLength() { if (m_CurrentSessionLength < 0) m_CurrentSessionLength = Mod.settings != null ? Mod.settings.DefaultEllipseLength : 192; return m_CurrentSessionLength; }

        /// <summary>
        /// Calculates and retrieves the current N slider value mapped to a 1-15 scale.
        /// </summary>
        public float GetCurrentNSliderValue() => 1.0f + (((1.0f - (0.895f / m_N)) - 0.105f) / 0.895f) * 14.0f;
        #endregion

        #region Core Tool Processing
        /// <summary>
        /// Processes user inputs and applies queued changes to the tool state.
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
            if (Mod.settings != null && Mod.settings.UseCtrlWheelForShapeAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0) SetCurrentNSlider(GetCurrentNSliderValue() + (scrollDir * 0.1f));
            }
        }

        /// <summary>
        /// Changes the width by aligning it to the next step value.
        /// </summary>
        public void ChangeWidth(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentWidthStepIndex, m_WidthSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentWidth(), stepSize, direction);
            SetCurrentWidth(nextValue);
        }

        /// <summary>
        /// Changes the length by aligning it to the next step value.
        /// </summary>
        public void ChangeLength(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentLengthStepIndex, m_LengthSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentLength(), stepSize, direction);
            SetCurrentLength(nextValue);
        }

        /// <summary>
        /// Safely sets the current width within bounds and queues a preview rebuild.
        /// </summary>
        private void SetCurrentWidth(int width)
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            int dynamicMinBound = GetMinimumAllowedSize(roadPrefab);

            int clamped = math.clamp(width, dynamicMinBound, 940);
            if (m_CurrentSessionWidth == clamped) return;

            m_CurrentSessionWidth = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Safely sets the current length within bounds and queues a preview rebuild.
        /// </summary>
        private void SetCurrentLength(int length)
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            int dynamicMinBound = GetMinimumAllowedSize(roadPrefab);

            int clamped = math.clamp(length, dynamicMinBound, 940);
            if (m_CurrentSessionLength == clamped) return;

            m_CurrentSessionLength = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Converts the UI slider value to the mathematical N parameter and updates the state.
        /// </summary>
        private void SetCurrentNSlider(float targetSlider)
        {
            float nextSlider = math.clamp(math.abs(targetSlider - 8.0f) < 0.05f ? 8.0f : targetSlider, 1.0f, 15.0f);
            if (math.abs(GetCurrentNSliderValue() - nextSlider) < 0.01f) return;
            m_N = 0.895f / (1.0f - math.min(0.105f + ((nextSlider - 1.0f) / 14.0f) * 0.895f, 0.999f));
        }
        #endregion

        #region Geometry Generation
        /// <summary>
        /// Attempts to generate the sub-networks and cells for the super ellipse geometry.
        /// </summary>
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            int minAllowed = GetMinimumAllowedSize(roadPrefab);
            if (m_CurrentSessionWidth < minAllowed) m_CurrentSessionWidth = minAllowed;
            if (m_CurrentSessionLength < minAllowed) m_CurrentSessionLength = minAllowed;

            subNets = null; widthCells = 0; depthCells = 0; costElevation = 0f;

            float roadWidth = EstimateRoadWidth(roadPrefab);
            float buildRx = (m_CurrentSessionWidth - roadWidth) * 0.5f;
            float buildRy = (m_CurrentSessionLength - roadWidth) * 0.5f;

            if (buildRx < roadWidth || buildRy < roadWidth) return false;

            costElevation = GetCurrentNetToolElevation();
            subNets = BuildSuperEllipseSubNets(roadPrefab, buildRx, buildRy, m_N, costElevation);

            widthCells = (int)math.ceil(m_CurrentSessionWidth / 8f);
            depthCells = (int)math.ceil(m_CurrentSessionLength / 8f);

            return true;
        }

        /// <summary>
        /// Calculates the minimum allowed dimension based on the road prefab width.
        /// </summary>
        private int GetMinimumAllowedSize(NetPrefab roadPrefab)
        {
            if (roadPrefab == null) return 48;
            float roadWidth = EstimateRoadWidth(roadPrefab);
            return (int)math.ceil(roadWidth * 3f);
        }

        /// <summary>
        /// Builds the bezier curves representing the super ellipse's sub-networks based on the Lamé curve equation.
        /// </summary>
        private ObjectSubNetInfo[] BuildSuperEllipseSubNets(NetPrefab roadPrefab, float a, float b, float n, float elevation)
        {
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[4];
            float kappa = 1.0f - (0.895f / n);

            float3[] p = { new(a, elevation, 0), new(0, elevation, b), new(-a, elevation, 0), new(0, elevation, -b) };

            for (int i = 0; i < 4; i++)
            {
                float3 t0 = float3.zero, t1 = float3.zero;
                if (i == 0) { t0 = new float3(0, 0, b * kappa); t1 = new float3(a * kappa, 0, 0); }
                if (i == 1) { t0 = new float3(-a * kappa, 0, 0); t1 = new float3(0, 0, b * kappa); }
                if (i == 2) { t0 = new float3(0, 0, -b * kappa); t1 = new float3(-a * kappa, 0, 0); }
                if (i == 3) { t0 = new float3(a * kappa, 0, 0); t1 = new float3(0, 0, -b * kappa); }

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Bezier4x3(p[i], p[i] + t0, p[(i + 1) % 4] + t1, p[(i + 1) % 4]),
                    m_NodeIndex = new int2(i, (i + 1) % 4),
                    m_ParentMesh = new int2(-1, -1)
                };
            }
            ApplyClosureNudge(result);
            return result;
        }

        /// <summary>
        /// Applies a slight adjustment to properly close the curve loop and align tangents.
        /// </summary>
        private void ApplyClosureNudge(ObjectSubNetInfo[] subNets)
        {
            if (subNets == null || subNets.Length < 2) return;

            ref ObjectSubNetInfo firstInfo = ref subNets[0];
            ref ObjectSubNetInfo lastInfo = ref subNets[^1];

            float3 lockedStart = firstInfo.m_BezierCurve.a;
            float dynamicTangentLength = math.distance(lockedStart, firstInfo.m_BezierCurve.b);
            float3 startDir = math.normalizesafe(firstInfo.m_BezierCurve.b - lockedStart, new float3(1, 0, 0));

            lastInfo.m_BezierCurve.d = lockedStart;
            lastInfo.m_BezierCurve.c = lockedStart - startDir * dynamicTangentLength;
            firstInfo.m_BezierCurve.b = lockedStart + startDir * dynamicTangentLength;
        }
        #endregion
    }
}