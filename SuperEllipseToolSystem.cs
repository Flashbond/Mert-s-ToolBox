using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Mathematics;

namespace MertsToolBox
{
    public partial class SuperEllipseToolSystem : MertBaseToolSystem
    {
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
        protected override string GetToolName() => "SuperEllipse";
        protected override bool RequiresSnapEnforcement => true;
        public void QueueWidthChange(int direction) => m_PendingWidthChange += direction;
        public void QueueWidthStepCycle() => m_PendingWidthStepCycle = true;
        public void QueueLengthChange(int direction) => m_PendingLengthChange += direction;
        public void QueueLengthStepCycle() => m_PendingLengthStepCycle = true;
        public void QueueNChange(float sliderDir) => m_PendingNSliderChange += sliderDir;
        public void SetNFromUi(float absVal) => SetCurrentNSlider(absVal);
        public int GetCurrentWidthStepIndex() => m_CurrentWidthStepIndex;
        public int GetCurrentLengthStepIndex() => m_CurrentLengthStepIndex;
        public int GetCurrentWidth() { if (m_CurrentSessionWidth < 0) m_CurrentSessionWidth = Mod.settings != null ? Mod.settings.DefaultEllipseWidth : 96; return m_CurrentSessionWidth; }
        public int GetCurrentLength() { if (m_CurrentSessionLength < 0) m_CurrentSessionLength = Mod.settings != null ? Mod.settings.DefaultEllipseLength : 192; return m_CurrentSessionLength; }
        public float GetCurrentNSliderValue() => 1.0f + (((1.0f - (0.895f / m_N)) - 0.105f) / 0.895f) * 14.0f;

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
        private int GetMinimumAllowedSize(NetPrefab roadPrefab)
        {
            if (roadPrefab == null) return 48;
            float roadWidth = EstimateRoadWidth(roadPrefab);
            return (int)math.ceil(roadWidth * 3f); // Kaptanın Altın Oranı
        }

        private void SetCurrentWidth(int width)
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            int dynamicMinBound = GetMinimumAllowedSize(roadPrefab);

            int clamped = math.clamp(width, dynamicMinBound, 940);
            if (m_CurrentSessionWidth == clamped) return;

            m_CurrentSessionWidth = clamped;
            QueuePreviewRebuild();
        }

        private void SetCurrentLength(int length)
        {
            NetPrefab roadPrefab = TryGetCurrentSelectedRoadPrefab();
            int dynamicMinBound = GetMinimumAllowedSize(roadPrefab);

            int clamped = math.clamp(length, dynamicMinBound, 940);
            if (m_CurrentSessionLength == clamped) return;

            m_CurrentSessionLength = clamped;
            QueuePreviewRebuild();
        }
        public void ChangeWidth(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentWidthStepIndex, m_WidthSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentWidth(), stepSize, direction);
            SetCurrentWidth(nextValue);
        }

        public void ChangeLength(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentLengthStepIndex, m_LengthSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentLength(), stepSize, direction);
            SetCurrentLength(nextValue);
        }

        private void SetCurrentNSlider(float targetSlider)
        {
            float nextSlider = math.clamp(math.abs(targetSlider - 8.0f) < 0.05f ? 8.0f : targetSlider, 1.0f, 15.0f);
            if (math.abs(GetCurrentNSliderValue() - nextSlider) < 0.01f) return;
            m_N = 0.895f / (1.0f - math.min(0.105f + ((nextSlider - 1.0f) / 14.0f) * 0.895f, 0.999f));
        }

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
    }
}