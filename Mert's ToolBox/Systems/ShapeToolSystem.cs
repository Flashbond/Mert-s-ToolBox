using Colossal.Mathematics;
using Game.Prefabs;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.Utilities.Preset;
using System.Collections.Generic;
using Unity.Mathematics;

namespace MertsToolBox.Systems
{
    public partial class ShapeToolSystem : MertBaseToolSystem
    {

        #region Fields & Properties
        private int m_CurrentSessionDimension = -1;
        public readonly int[] m_DimensionSteps = new int[] { 8, 6, 4, 2 };
        private int m_CurrentDimensionStepIndex = 0;
        private int m_PendingDimensionChange = 0;
        private int m_TargetDimensionStep = -1;

        private bool m_IsClockwise = true; 
        private bool m_PendingToggleDirection = false;

        // --- DATA-DRIVEN SHAPE DEFINITIONS ---
        public static readonly (int Sides, string Name)[] ShapeDefinitions = new (int, string)[]
        {
            (3, "Triangle"),
            (4, "Square"),
            (5, "Pentagon"),
            (6, "Hexagon"),
            (7, "Heptagon"),
            (8, "Octagon"),
            (0, "Circle")
        };

        private int m_CurrentSidesIndex = ShapeDefinitions.Length - 1;
        private int m_PendingSidesChange = 0;

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        public override string ToolId => "Shape";
        public override string ToolName => "Perfect Shape";

        /// <summary>
        /// Indicates whether this tool requires snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => true;
        #endregion

        #region Preset Management
        public override MertToolPreset CreatePresetSnapshot()
        {
            NetPrefab prefab = TryGetCurrentSelectedRoadPrefab();
            string prefabName = prefab?.name ?? "UnknownRoad";

            int dimension = GetCurrentDimension();
            int currentSides = GetCurrentSides();
            string shapeName = GetShapeName(currentSides);

            return new MertToolPreset
            {
                ToolId = ToolId,
                ToolName = ToolName,
                PrefabName = prefabName,

                DisplayName = SanitizeFileName(
                    $"{ToolId}_{prefabName}_{shapeName}_Dimension{dimension}m_{(m_IsClockwise ? "CW" : "CCW")}" +
                    $"{(MertToolState.SuppressCrosswalks ? "_NoCrosswalks" : "")}"
                ),

                Values = new Dictionary<string, float>
                {
                    ["Dimension"] = dimension,
                    ["Sides"] = currentSides,
                    ["Clockwise"] = m_IsClockwise ? 1f : 0f,
                    ["NoCrosswalks"] = MertToolState.SuppressCrosswalks ? 1f : 0f,
                }
            };
        }

        public string GetShapeName(int sides)
        {
            for (int i = 0; i < ShapeDefinitions.Length; i++)
            {
                if (ShapeDefinitions[i].Sides == sides)
                    return ShapeDefinitions[i].Name;
            }
            return "Unknown";
        }

        public override void ApplyPresetSnapshot(MertToolPreset preset)
        {
            if (preset?.Values == null)
                return;

            if (preset.Values.TryGetValue("Dimension", out float dimension))
                SetCurrentDimension((int)dimension);

            if (preset.Values.TryGetValue("Sides", out float sides))
                SetSides((int)sides);

            if (preset.Values.TryGetValue("Clockwise", out float clockwise))
                m_IsClockwise = clockwise > 0.5f;

            if (preset.Values.TryGetValue("NoCrosswalks", out float noCrosswalks))
                MertToolState.SuppressCrosswalks = noCrosswalks >= 0.5f;

        
            QueuePreviewRebuild();
        }
        #endregion

        #region Input Queuing & State
        protected override void RetrieveParametersFromSettings(int toolIndex, int paramIndex)
        {
            if (toolIndex != 1)
                return;

            m_CurrentSessionDimension = Mod.settings.DefaultShapeDimension;

            QueuePreviewRebuild();
        }

        /// <summary>
        /// Queues a change in the diameter based on the given direction.
        /// </summary>
        public void QueueDimensionChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingDimensionChange += direction;
        }

        /// <summary>
        /// Queues a change in the polygon sides based on the given direction.
        /// </summary>
        public void QueueSidesChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingSidesChange += direction;
        }

        public void QueueSetDimensionStep(int value) => m_TargetDimensionStep = value;

        public override void QueueToggleMainDirection()
        {
            if (!IsCurrentPrefabValidForOneWayPattern()) return;
            RegisterUndoForButton();
            m_PendingToggleDirection = true;
        }
        #endregion

        #region Metrics & Data Retrieval
        public int GetDimensionStepSize() => GetCurrentStepValue(m_CurrentDimensionStepIndex, m_DimensionSteps);

        public int GetCurrentDimension()
        {
            if (m_CurrentSessionDimension < 0)
                m_CurrentSessionDimension = Mod.settings != null ? Mod.settings.DefaultShapeDimension : 96;
            return m_CurrentSessionDimension;
        }

        public ShapeMetrics GetCurrentShapeMetrics()
        {
            return ShapeMetrics.FromOuterDimension(GetCurrentDimension(), m_CurrentRoadWidth);
        }

        private int GetMinimumAllowedDimension()
        {
            return (int)math.ceil(m_CurrentRoadWidth * 3f);
        }

        public int GetCurrentSides() => ShapeDefinitions[m_CurrentSidesIndex].Sides;
        public int GetCurrentSidesIndex() => m_CurrentSidesIndex;

        public override bool GetMainDirectionState() => m_IsClockwise;
        #endregion

        #region Core Tool Processing
        protected override void ProcessToolInput()
        {
            EnforceOneWayOnlyOptions();

            if (m_PendingToggleDirection)
            {
                m_IsClockwise = !m_IsClockwise;
                m_PendingToggleDirection = false;
                QueuePreviewRebuild();
            }

            if (m_TargetDimensionStep != -1)
            {
                m_CurrentDimensionStepIndex = GetIndexFromValue(
                    m_TargetDimensionStep,
                    m_DimensionSteps,
                    m_CurrentDimensionStepIndex
                );
                m_TargetDimensionStep = -1;
            }

            if (m_PendingDimensionChange != 0)
            {
                ChangeDimension(m_PendingDimensionChange);
                m_PendingDimensionChange = 0;
            }

            if (m_PendingSidesChange != 0)
            {
                ChangeSides(m_PendingSidesChange);
                m_PendingSidesChange = 0;
            }

            if (Mod.settings != null && Mod.settings.UseCtrlWheelForShapeDimensionAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0)
                {
                    RegisterUndoForWheel();
                    SetCurrentDimension(GetCurrentDimension() + scrollDir);
                }
            }
        }

        public void ChangeDimension(int direction)
        {
            int stepSize = GetCurrentStepValue(m_CurrentDimensionStepIndex, m_DimensionSteps);
            int nextValue = GetNextStepAlignedInt(GetCurrentDimension(), stepSize, direction);
            SetCurrentDimension(nextValue);
        }

        /// <summary>
        /// Wraps the side selection (5 <-> 6 <-> 7 <-> 8 <-> 0)
        /// </summary>
        private void ChangeSides(int direction)
        {
            int newIndex = math.clamp(m_CurrentSidesIndex + direction, 0, ShapeDefinitions.Length - 1);

            if (m_CurrentSidesIndex == newIndex)
                return;

            m_CurrentSidesIndex = newIndex;
            QueuePreviewRebuild();
        }

        private void SetSides(int targetSides)
        {
            for (int i = 0; i < ShapeDefinitions.Length; i++)
            {
                if (ShapeDefinitions[i].Sides == targetSides)
                {
                    if (m_CurrentSidesIndex == i)
                        return;

                    m_CurrentSidesIndex = i;
                    QueuePreviewRebuild();
                    return;
                }
            }
        }

        private void SetCurrentDimension(int diameter)
        {
            int dynamicMinBound = GetMinimumAllowedDimension();
            int clamped = math.clamp(diameter, dynamicMinBound, 940);

            if (m_CurrentSessionDimension == clamped)
                return;

            m_CurrentSessionDimension = clamped;
            QueuePreviewRebuild();
        }
        #endregion

        #region Geometry Generation
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            int minAllowed = GetMinimumAllowedDimension();
            if (m_CurrentSessionDimension < minAllowed)
                m_CurrentSessionDimension = minAllowed;

            ShapeMetrics metrics = GetCurrentShapeMetrics();

            subNets = null;
            widthCells = depthCells = 0;
            costElevation = 0f;

            if (metrics.CenterDiameterMeters <= 0f)
                return false;

            int sides = ShapeDefinitions[m_CurrentSidesIndex].Sides;
            costElevation = GetCurrentNetToolElevation();

            float finalBuildRadius;

            if (sides == 0)
            {
                finalBuildRadius = metrics.BuildRadius;

                int segments = CalculateAutoSegments(finalBuildRadius);
                subNets = BuildShapeSubNets(roadPrefab, finalBuildRadius, segments, costElevation);
            }
            else
            {
                if (sides % 2 == 0)
                {
                    finalBuildRadius = metrics.BuildRadius / math.cos(math.PI / sides);
                }
                else
                {
                    finalBuildRadius = metrics.BuildRadius;
                }

                subNets = BuildPolygonSubNets(roadPrefab, finalBuildRadius, sides, costElevation);
            }

            widthCells = depthCells = (int)math.ceil(metrics.OuterDimensionMeters / 8f);

            return true;
        }

        private int CalculateAutoSegments(float radius)
        {
            if (radius <= 24f) return 4;
            if (radius <= 40f) return 8;
            if (radius <= 80f) return 12;
            return 16;
        }
        /// <summary>
        /// Draws Shape geometry.
        /// </summary>
        private ObjectSubNetInfo[] BuildShapeSubNets(NetPrefab roadPrefab, float radius, int segments, float elevation)
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
                float3 p1 = points[i] + forwardTangents[i] * tangentLength;
                float3 p2 = points[next] - forwardTangents[next] * tangentLength;
                float3 p3 = points[next];

                bool reverseDirection = m_IsClockwise;

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = reverseDirection ? new Bezier4x3(p3, p2, p1, p0) : new Bezier4x3(p0, p1, p2, p3),
                    m_NodeIndex = reverseDirection ? new int2(next, i) : new int2(i, next),
                    m_ParentMesh = new int2(-1, -1)
                };
            }

            return result;
        }

        /// <summary>
        /// Draws Polygon geometry with straight lines.
        /// </summary>
        private ObjectSubNetInfo[] BuildPolygonSubNets(NetPrefab roadPrefab, float radius, int sides, float elevation)
        {
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[sides];
            float step = (math.PI * 2f) / sides;

            float3[] points = new float3[sides];

            for (int i = 0; i < sides; i++)
            {
                float a = i * step - (math.PI / 2f);
                points[i] = new float3(math.cos(a) * radius, elevation, math.sin(a) * radius);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                float3 p0 = points[i];
                float3 p3 = points[next];

                float3 dir = p3 - p0;
                float3 p1 = p0 + (dir / 3f);
                float3 p2 = p3 - (dir / 3f);

                bool reverseDirection = m_IsClockwise;

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = reverseDirection ? new Bezier4x3(p3, p2, p1, p0) : new Bezier4x3(p0, p1, p2, p3),
                    m_NodeIndex = reverseDirection ? new int2(next, i) : new int2(i, next),
                    m_ParentMesh = new int2(-1, -1)
                };
            }

            return result;
        }
        #endregion
    }
}