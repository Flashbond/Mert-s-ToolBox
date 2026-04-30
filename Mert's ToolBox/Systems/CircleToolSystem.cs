using Colossal.Mathematics;
using Game.Prefabs;
using MertsToolBox.Core;
using Unity.Mathematics;

namespace MertsToolBox.Systems
{
    public partial class CircleToolSystem : MertBaseToolSystem
    {
        #region Fields & Properties
        private int m_CurrentSessionDiameter = -1;
        public readonly int[] m_DiameterSteps = new int[] { 8, 6, 4, 2 };
        private int m_CurrentDiameterStepIndex = 0;

        private int m_PendingDiameterChange = 0;
        private int m_TargetDiameterStep = -1;

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        public override string ToolId => "Circle";
        public override string ToolName => "Perfect Circle";

        /// <summary>
        /// Indicates whether this tool requires snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => true;
        protected override bool HandlesOwnElevationInput => true;
        #endregion

        #region Input Queuing & State
        protected override void OnSettingsChanged()
        {
            if (Mod.settings != null)
            {
                m_CurrentSessionDiameter = Mod.settings.DefaultCircleDiameter;
            }

            base.OnSettingsChanged();
        }
        /// <summary>
        /// Queues a change in the diameter based on the given direction.
        /// </summary>
        public void QueueDiameterChange(int direction) => m_PendingDiameterChange += direction;

        /// <summary>
        /// Queues a step cycle for the diameter adjustment.
        /// </summary>
        public void QueueSetDiameterStep(int value) => m_TargetDiameterStep = value;
        #endregion

        #region Metrics & Data Retrieval
        /// <summary>
        /// Retrieves the step size for diameter adjustments.
        /// </summary>
        public int GetDiameterStepSize() => GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);

        /// <summary>
        /// Retrieves the current session diameter, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentDiameter()
        {
            if (m_CurrentSessionDiameter < 0)
                m_CurrentSessionDiameter = Mod.settings != null ? Mod.settings.DefaultCircleDiameter : 96;
            return m_CurrentSessionDiameter;
        }

        /// <summary>
        /// Calculates and returns the current circle metrics.
        /// </summary>
        public CircleMetrics GetCurrentCircleMetrics()
        {
            return CircleMetrics.FromOuterDiameter(GetCurrentDiameter(), m_CurrentWidth);
        }

        /// <summary>
        /// Calculates the minimum allowed diameter based on the road prefab width.
        /// </summary>
        private int GetMinimumAllowedDiameter()
        {
            return (int)math.ceil(m_CurrentWidth * 3f);
        }
        #endregion

        #region Core Tool Processing
        /// <summary>
        /// Processes user inputs and applies queued changes to the tool state.
        /// </summary>
        protected override void ProcessToolInput()
        {
            if (!ToolEnabled) return;

            if (m_TargetDiameterStep != -1)
            {
                m_CurrentDiameterStepIndex = GetIndexFromValue(
                    m_TargetDiameterStep,
                    m_DiameterSteps,
                    m_CurrentDiameterStepIndex
                );
                ModRuntime.Log($"{m_TargetDiameterStep}");
                m_TargetDiameterStep = -1;
            }
            if (m_PendingDiameterChange != 0) { ChangeDiameter(m_PendingDiameterChange); m_PendingDiameterChange = 0; }

            if (Mod.settings != null && Mod.settings.UseCtrlWheelForCircleDiameterAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0) SetCurrentDiameter(GetCurrentDiameter() + scrollDir);
            }
        }

        /// <summary>
        /// Changes the diameter by aligning it to the next step value.
        /// </summary>
        public void ChangeDiameter(int direction)
        {
            ModRuntime.Log($"m_CurrentDiameterStepIndex: {m_CurrentDiameterStepIndex}, m_DiameterSteps: {m_DiameterSteps}");
            int stepSize = GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentDiameter(), stepSize, direction);
            SetCurrentDiameter(nextValue);
        }

        /// <summary>
        /// Safely sets the current diameter and queues a preview rebuild.
        /// </summary>
        private void SetCurrentDiameter(int diameter)
        {
            int dynamicMinBound = GetMinimumAllowedDiameter();
            int clamped = math.clamp(diameter, dynamicMinBound, 940);

            if (m_CurrentSessionDiameter == clamped)
                return;

            m_CurrentSessionDiameter = clamped;
            QueuePreviewRebuild();
        }
        #endregion

        #region Geometry Generation
        /// <summary>
        /// Attempts to generate the sub-networks and cells for the circular geometry.
        /// </summary>
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            int minAllowed = GetMinimumAllowedDiameter();
            if (m_CurrentSessionDiameter < minAllowed)
            {
                m_CurrentSessionDiameter = minAllowed;
            }

            subNets = null; widthCells = 0; depthCells = 0; costElevation = 0f;

            float buildR = (m_CurrentSessionDiameter - m_CurrentWidth) * 0.5f;

            if (buildR < m_CurrentWidth) return false;

            costElevation = GetCurrentNetToolElevation();
            int segments = CalculateAutoSegments(buildR);
            subNets = BuildCircleSubNets(roadPrefab, buildR, segments, costElevation);

            widthCells = (int)math.ceil(m_CurrentSessionDiameter / 8f);
            depthCells = (int)math.ceil(m_CurrentSessionDiameter / 8f);

            return true;
        }

        /// <summary>
        /// Calculates the optimal number of segments for the circle based on its radius.
        /// </summary>
        private int CalculateAutoSegments(float radius)
        {
            if (radius <= 24f) return 4;
            if (radius <= 40f) return 8;
            if (radius <= 80f) return 12;
            return 16;
        }

        /// <summary>
        /// Builds the bezier curves representing the circle's sub-networks.
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
                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Bezier4x3(points[i], points[i] + forwardTangents[i] * tangentLength, points[next] - forwardTangents[next] * tangentLength, points[next]),
                    m_NodeIndex = new int2(i, next),
                    m_ParentMesh = new int2(-1, -1)
                };
            }

            ApplyClosureNudge(result, tangentLength);
            return result;
        }

        /// <summary>
        /// Applies a slight adjustment to properly close the curve loop.
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
    }
}