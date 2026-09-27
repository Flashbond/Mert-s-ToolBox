using Colossal.Mathematics;
using Game.Prefabs;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.Utilities.Preset;
using System.Collections.Generic;
using Unity.Mathematics;

namespace MertsToolBox.Systems
{
    public partial class SoftBlockToolSystem : MertBaseToolSystem
    {
        #region Fields & Properties
        private int m_CurrentSessionWidth = -1;
        public readonly int[] m_WidthSteps = new int[] { 8, 6, 4, 2 };
        private int m_CurrentWidthStepIndex = 0;

        private int m_CurrentSessionLength = -1;
        public readonly int[] m_LengthSteps = new int[] { 8, 6, 4, 2 };
        private int m_CurrentLengthStepIndex = 0;

        private float m_CurrentSessionBorderRadius = -1f;

        private bool m_UseStraightCorners = false;

        private int m_PendingWidthChange = 0;
        private int m_TargetWidthStep = -1;
        private int m_PendingLengthChange = 0;
        private int m_TargetLengthStep = -1;
        private float m_PendingBorderRadiushange = 0f;
        private bool m_PendingToggleStraightCorners = false;
        private bool m_IsClockwise = true;
        private bool m_PendingToggleDirection = false;


        private enum BorderRadiusSnapState
        {
            Armed,
            Locked,
            Disarmed
        }

        private BorderRadiusSnapState m_BorderRadiusSnapState = BorderRadiusSnapState.Armed;

        private const float BorderRadiusSnapTarget = 8f;
        private const float BorderRadiusSnapThreshold = 0.4f;
        private int m_PillWheelLatchTicks = 0;

        /// <summary>
        /// Standard cubic Bézier circle approximation.
        /// </summary>
        private static readonly float Kappa = 4f * (math.sqrt(2f) - 1f) / 3f;
        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        public override string ToolId => "SoftBlock";
        public override string ToolName => "Soft Block";

        /// <summary>
        /// Indicates whether this tool requires snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => true;
        #endregion

        #region Preset System
        public override MertToolPreset CreatePresetSnapshot()
        {
            NetPrefab prefab = TryGetCurrentSelectedRoadPrefab();
            string prefabName = prefab?.name ?? "UnknownRoad";

            int width = GetCurrentWidth();
            int length = GetCurrentLength();
            float radius = GetCurrentBorderRadius();


            return new MertToolPreset
            {
                ToolId = ToolId,
                ToolName = ToolName,
                PrefabName = prefabName,

                DisplayName = SanitizeFileName(
                    $"{ToolId}_{prefabName}_Dimensions{width}x{length}m_Radius{radius:0.0}_{(m_IsClockwise ? "CW" : "CCW")}" +
                    $"{(m_UseStraightCorners ? "_StraightCorners" : "")}"+
                    $"{(MertToolState.SuppressCrosswalks ? "_NoCrosswalks" : "")}"
                ),

                Values = new Dictionary<string, float>
                {
                    ["Width"] = width,
                    ["Length"] = length,
                    ["BorderRadius"] = radius,
                    ["Clockwise"] = m_IsClockwise ? 1f : 0f,
                    ["StraightCorners"] = m_UseStraightCorners ? 1f : 0f,
                    ["NoCrosswalks"] = MertToolState.SuppressCrosswalks ? 1f : 0f
                }
            };
        }

        public override void ApplyPresetSnapshot(MertToolPreset preset)
        {
            if (preset == null || preset.Values == null)
                return;

            if (preset.Values.TryGetValue("Width", out float width))
                SetCurrentWidth((int)width);

            if (preset.Values.TryGetValue("Length", out float length))
                SetCurrentLength((int)length);

            if (preset.Values.TryGetValue("BorderRadius", out float radius))
                SetCurrentBorderRadius(radius, useSnap: false);

            if (preset.Values.TryGetValue("Clockwise", out float clockwise))
                m_IsClockwise = clockwise >= 0.5f;

            if (preset.Values.TryGetValue("StraightCorners", out float corners))
                m_UseStraightCorners = corners >= 0.5f;

            if (preset.Values.TryGetValue("NoCrosswalks", out float noCrosswalks))
                MertToolState.SuppressCrosswalks = noCrosswalks >= 0.5f;

            QueuePreviewRebuild();
        }
        #endregion

        #region Input Queuing & State
        protected override void RetrieveParametersFromSettings(int toolIndex, int paramIndex)
        {
            if (toolIndex != 3)
                return;

            switch (paramIndex)
            {
                case 1:
                    m_CurrentSessionWidth = Mod.settings.DefaultSoftBlockWidth;
                    break;

                case 2:
                    m_CurrentSessionLength = Mod.settings.DefaultSoftBlockLength;
                    break;

                case 3:
                    m_CurrentSessionBorderRadius = Mod.settings.DefaultSoftBlockBorderRadius;
                    break;
            }

            QueuePreviewRebuild();
        }
        public void BeginBorderRadiusDrag()
        {
            BeginSliderUndoTransaction();

            m_BorderRadiusSnapState =
                math.abs(m_CurrentSessionBorderRadius - BorderRadiusSnapTarget) <= BorderRadiusSnapThreshold
                    ? BorderRadiusSnapState.Disarmed
                    : BorderRadiusSnapState.Armed;
        }

        public void EndBorderRadiusDrag()
        {
            EndSliderUndoTransaction();

            m_BorderRadiusSnapState = BorderRadiusSnapState.Armed;
        }

        /// <summary>
        /// Queues a change in the width based on the given direction.
        /// </summary>
        public void QueueWidthChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingWidthChange += direction;
        }


        /// <summary>
        /// Queues a step cycle for the width adjustment.
        /// </summary>
        public void QueueSetWidthStep(int value)
        {
            m_TargetWidthStep = value;
        }

        /// <summary>
        /// Queues a change in the length based on the given direction.
        /// </summary>
        public void QueueLengthChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingLengthChange += direction;
        }

        /// <summary>
        /// Queues a step cycle for the length adjustment.
        /// </summary>
        public void QueueSetLengthStep(int value)
        {
            m_TargetLengthStep = value;
        }

        /// <summary>
        /// Sets the current R value directly from the UI slider.
        /// </summary>
        public void SetBorderRadiusFromUi(float value)
        {
            SetCurrentBorderRadius(value, useSnap: true);
        }

        public override void QueueToggleMainDirection()
        {
            if (!IsCurrentPrefabValidForOneWayPattern()) return;
            RegisterUndoForButton();
            m_PendingToggleDirection = true;
        }

        public void QueueToggleStraightCorners()
        {
            if (!IsStraightCornersSupported())
                return;

            RegisterUndoForButton();
            m_PendingToggleStraightCorners = true;
        }

        public override bool GetMainDirectionState() => m_IsClockwise;
        public bool GetUseStraightCorners() => m_UseStraightCorners;

        public bool IsStraightCornersSupported()
        {
            NetPrefab currentPrefab = TryGetCurrentSelectedRoadPrefab();

            if (currentPrefab is TrackPrefab)
            {
                string lower = currentPrefab.name?.ToLowerInvariant() ?? string.Empty;

                if (lower.Contains("train") || lower.Contains("subway"))
                    return false;
            }

            return true;
        }
        #endregion

        #region Metrics & Data Retrieval
        /// <summary>
        /// Retrieves the step size for width adjustments.
        /// </summary>
        public int GetWidthStepSize() => GetCurrentStepValue(m_CurrentWidthStepIndex, m_WidthSteps);

        /// <summary>
        /// Retrieves the step size for length adjustments.
        /// </summary>
        public int GetLengthStepSize() => GetCurrentStepValue(m_CurrentLengthStepIndex, m_LengthSteps);

        /// <summary>
        /// Retrieves the current session width, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentWidth() { if (m_CurrentSessionWidth < 0) m_CurrentSessionWidth = Mod.settings != null ? Mod.settings.DefaultSoftBlockWidth : 96; return m_CurrentSessionWidth; }

        /// <summary>
        /// Retrieves the current session length, applying default settings if uninitialized.
        /// </summary>
        public int GetCurrentLength() { if (m_CurrentSessionLength < 0) m_CurrentSessionLength = Mod.settings != null ? Mod.settings.DefaultSoftBlockLength : 192; return m_CurrentSessionLength; }

        /// <summary>
        /// Retrieves the current session radius, applying default settings if uninitialized.
        /// </summary>
        public float GetCurrentBorderRadius() { if (m_CurrentSessionBorderRadius < 0) m_CurrentSessionBorderRadius = Mod.settings != null ? Mod.settings.DefaultSoftBlockBorderRadius : 5.0f; return m_CurrentSessionBorderRadius; }
        #endregion

        #region Core Tool Processing
        /// <summary>
        /// Processes user inputs and applies queued changes to the tool state.
        /// </summary>
        protected override void ProcessToolInput()
        {
            EnforceOneWayOnlyOptions();

            if (m_PendingToggleDirection)
            {
                m_IsClockwise = !m_IsClockwise;
                m_PendingToggleDirection = false;
                QueuePreviewRebuild();
            }

            if (m_TargetWidthStep != -1)
            {
                m_CurrentWidthStepIndex = GetIndexFromValue(
                    m_TargetWidthStep,
                    m_WidthSteps,
                    m_CurrentWidthStepIndex
                );
                m_TargetWidthStep = -1;
            }

            if (m_PendingWidthChange != 0) { ChangeWidth(m_PendingWidthChange); m_PendingWidthChange = 0; }
            if (m_TargetLengthStep != -1)
            {
                m_CurrentLengthStepIndex = GetIndexFromValue(
                    m_TargetLengthStep,
                    m_LengthSteps,
                    m_CurrentLengthStepIndex
                );
                m_TargetLengthStep = -1;
            }

            if (m_PendingLengthChange != 0) { ChangeLength(m_PendingLengthChange); m_PendingLengthChange = 0; }

            if (m_PendingToggleStraightCorners)
            {
                if (IsStraightCornersSupported())
                {
                    m_UseStraightCorners = !m_UseStraightCorners;
                    QueuePreviewRebuild();
                }

                m_PendingToggleStraightCorners = false;
            }

            if (math.abs(m_PendingBorderRadiushange) > 0.001f)
            {
                SetCurrentBorderRadius(GetCurrentBorderRadius() + m_PendingBorderRadiushange);
                m_PendingBorderRadiushange = 0f;
            }
            if (Mod.settings != null && Mod.settings.UseCtrlWheelForSoftBlockBorderRadius &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0)
                {
                    RegisterUndoForWheel();
                    SetCurrentBorderRadiusFromWheel(scrollDir * 0.1f);
                }
            }
        }
        private void SetCurrentBorderRadiusFromWheel(float delta)
        {
            float current = m_CurrentSessionBorderRadius;
            float raw = math.clamp(current + delta, 1f, 10f);

            float currentDist = math.abs(current - BorderRadiusSnapTarget);
            float rawDist = math.abs(raw - BorderRadiusSnapTarget);

            // ------------------------------------------------------------
            // HARD LATCH:
            // Once snapped to pill (8), consume a few wheel ticks before
            // allowing escape. This makes the pill boundary feel intentional.
            // ------------------------------------------------------------
            if (m_PillWheelLatchTicks > 0)
            {
                bool tryingToLeavePill =
                    (delta > 0f && current >= BorderRadiusSnapTarget) ||
                    (delta < 0f && current <= BorderRadiusSnapTarget);

                if (tryingToLeavePill)
                {
                    m_PillWheelLatchTicks--;
                    return;
                }

                m_PillWheelLatchTicks = 0;
            }

            // ------------------------------------------------------------
            // Enter snap zone from outside -> snap hard to 8.
            // ------------------------------------------------------------
            bool enteredSnapZoneFromOutside =
                currentDist > BorderRadiusSnapThreshold &&
                rawDist <= BorderRadiusSnapThreshold;

            if (enteredSnapZoneFromOutside)
            {
                // Stay attached for a few wheel steps.
                m_PillWheelLatchTicks = 4;

                SetCurrentBorderRadius(BorderRadiusSnapTarget, useSnap: false);
                return;
            }

            SetCurrentBorderRadius(raw, useSnap: false);
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
        /// Calculates the minimum allowed dimension based on the road prefab width.
        /// </summary>
        private int GetMinimumAllowedSize()
        {
            return (int)math.ceil(m_CurrentRoadWidth * GetMinimumSafeRadiusMultiplier());
        }
        private float GetMinimumSafeRadiusMultiplier()
        {
            NetPrefab currentPrefab = TryGetCurrentSelectedRoadPrefab();

            if (currentPrefab is TrackPrefab)
            {
                string lower = currentPrefab.name?.ToLowerInvariant() ?? string.Empty;

                // Tram tracks can tolerate tighter curves.
                if (lower.Contains("tram"))
                    return 4f;

                // Train/subway tracks need wider curves.
                if (lower.Contains("train") ||
                    lower.Contains("subway"))
                    return 8f;

                // Generic track fallback.
                return 6f;
            }

            // Default road behavior.
            return 3f;
        }

        /// <summary>
        /// Safely sets the current width within bounds and queues a preview rebuild.
        /// </summary>
        private void SetCurrentWidth(int width)
        {
            int dynamicMinBound = GetMinimumAllowedSize(); // Parametre silindi

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
            int dynamicMinBound = GetMinimumAllowedSize(); // Parametre silindi

            int clamped = math.clamp(length, dynamicMinBound, 940);
            if (m_CurrentSessionLength == clamped) return;

            m_CurrentSessionLength = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Converts the UI slider value to the mathematical N parameter and updates the state.
        /// </summary>
        private void SetCurrentBorderRadius(float targetSlider, bool useSnap = true)
        {
            float raw = math.clamp(targetSlider, 1f, 10f);
            float nextSlider = raw;

            if (useSnap)
            {
                float dist = math.abs(raw - BorderRadiusSnapTarget);

                if (m_BorderRadiusSnapState == BorderRadiusSnapState.Armed &&
                    dist <= BorderRadiusSnapThreshold)
                {
                    m_BorderRadiusSnapState = BorderRadiusSnapState.Locked;
                    nextSlider = BorderRadiusSnapTarget;
                }
                else if (m_BorderRadiusSnapState == BorderRadiusSnapState.Locked)
                {
                    if (dist <= BorderRadiusSnapThreshold)
                    {
                        nextSlider = BorderRadiusSnapTarget;
                    }
                    else
                    {
                        m_BorderRadiusSnapState = BorderRadiusSnapState.Armed;
                        nextSlider = raw;
                    }
                }
                else if (m_BorderRadiusSnapState == BorderRadiusSnapState.Disarmed)
                {
                    nextSlider = raw;

                    if (dist > BorderRadiusSnapThreshold)
                        m_BorderRadiusSnapState = BorderRadiusSnapState.Armed;
                }
            }

            if (math.abs(m_CurrentSessionBorderRadius - nextSlider) < 0.01f)
                return;

            m_CurrentSessionBorderRadius = nextSlider;
            QueuePreviewRebuild();
        }
        #endregion

        #region Geometry Generation
        /// <summary>
        /// Attempts to generate the sub-networks and cells for the super ellipse geometry.
        /// </summary>
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            int minAllowed = GetMinimumAllowedSize();
            if (m_CurrentSessionWidth < minAllowed) m_CurrentSessionWidth = minAllowed;
            if (m_CurrentSessionLength < minAllowed) m_CurrentSessionLength = minAllowed;
            subNets = null; widthCells = 0; depthCells = 0; costElevation = 0f;
            float buildRx = (m_CurrentSessionWidth - m_CurrentRoadWidth) * 0.5f;
            float buildRy = (m_CurrentSessionLength - m_CurrentRoadWidth) * 0.5f;
            if (buildRx < m_CurrentRoadWidth || buildRy < m_CurrentRoadWidth) return false;
            costElevation = GetCurrentNetToolElevation();

            subNets = BuildAdaptiveShapeSubNets(roadPrefab, buildRx, buildRy, costElevation);

            widthCells = (int)math.ceil(m_CurrentSessionWidth / 8f);
            depthCells = (int)math.ceil(m_CurrentSessionLength / 8f);
            return true;
        }

        /// <summary>
        /// Builds the bezier curves representing the super ellipse's sub-networks based on the Lamé curve equation.
        /// </summary>
        private ObjectSubNetInfo[] BuildAdaptiveShapeSubNets(NetPrefab roadPrefab,float rx,float ry,float elevation)
        {

            const float MinLineLength = 0.5f;

            float shortest = math.min(rx, ry);

            // Minimum legal outer dimension already includes road/track-specific safety.
            // Convert that minimum outer size into a real corner radius.
            float minAllowedSize = GetMinimumAllowedSize();
            float minSafeRadius = (minAllowedSize - m_CurrentRoadWidth) * 0.5f;

            // The pill limit is the largest circular corner radius that still fits the box.
            float pillRadius = shortest;

            minSafeRadius = math.clamp(minSafeRadius, 0.01f, pillRadius);

            float ui = math.clamp(m_CurrentSessionBorderRadius, 1f, 10f);

            float cornerX;
            float cornerY;

            if (ui <= 8f)
            {
                // 1..8 = real quarter-circle radius:
                // minimum safe radius -> pill radius.
                float t = math.saturate((ui - 1f) / 7f);

                float r = math.lerp(minSafeRadius, pillRadius, t);

                cornerX = r;
                cornerY = r;
            }
            else
            {
                // 8..10 = pill -> fountain pen / ellipse.
                // Here the corner stops being a pure circular quarter-piece
                // and morphs into an elliptical cap.
                float blend = math.saturate((ui - 8f) / 2f);

                cornerX = math.lerp(pillRadius, rx, blend);
                cornerY = math.lerp(pillRadius, ry, blend);
            }

            cornerX = math.clamp(cornerX, 0.01f, rx);
            cornerY = math.clamp(cornerY, 0.01f, ry);

            float left = -rx;
            float right = rx;
            float top = ry;
            float bottom = -ry;

            float innerLeft = left + cornerX;
            float innerRight = right - cornerX;
            float innerTop = top - cornerY;
            float innerBottom = bottom + cornerY;

            List<Bezier4x3> curves = new();

            void AddLine(float3 a, float3 d)
            {
                float3 delta = d - a;

                if (math.length(delta) < MinLineLength)
                    return;

                curves.Add(new Bezier4x3(
                    a,
                    a + delta / 3f,
                    a + delta * 2f / 3f,
                    d
                ));
            }

            void AddCorner(float3 a, float3 b, float3 c, float3 d)
            {
                curves.Add(new Bezier4x3(a, b, c, d));
            }

            // TOP
            AddLine(
                new float3(innerRight, elevation, top),
                new float3(innerLeft, elevation, top)
            );
            void AddCornerOrChord(float3 a, float3 b, float3 c, float3 d)
            {
                if (m_UseStraightCorners)
                {
                    AddLine(a, d);
                    return;
                }

                AddCorner(a, b, c, d);
            }
            // TOP LEFT
            AddCornerOrChord(
                new float3(innerLeft, elevation, top),
                new float3(innerLeft - cornerX * Kappa, elevation, top),
                new float3(left, elevation, innerTop + cornerY * Kappa),
                new float3(left, elevation, innerTop)
            );

            // LEFT
            AddLine(
                new float3(left, elevation, innerTop),
                new float3(left, elevation, innerBottom)
            );

            // BOTTOM LEFT
            AddCornerOrChord(
                new float3(left, elevation, innerBottom),
                new float3(left, elevation, innerBottom - cornerY * Kappa),
                new float3(innerLeft - cornerX * Kappa, elevation, bottom),
                new float3(innerLeft, elevation, bottom)
            );

            // BOTTOM
            AddLine(
                new float3(innerLeft, elevation, bottom),
                new float3(innerRight, elevation, bottom)
            );

            // BOTTOM RIGHT
            AddCornerOrChord(
                 new float3(innerRight, elevation, bottom),
                 new float3(innerRight + cornerX * Kappa, elevation, bottom),
                 new float3(right, elevation, innerBottom - cornerY * Kappa),
                 new float3(right, elevation, innerBottom)
             );

            // RIGHT
            AddLine(
                new float3(right, elevation, innerBottom),
                new float3(right, elevation, innerTop)
            );

            // TOP RIGHT
            AddCornerOrChord(
                new float3(right, elevation, innerTop),
                new float3(right, elevation, innerTop + cornerY * Kappa),
                new float3(innerRight + cornerX * Kappa, elevation, top),
                new float3(innerRight, elevation, top)
            );
            RotateCurveStart(curves, 2);
            ObjectSubNetInfo[] result = new ObjectSubNetInfo[curves.Count];

            bool reverseDirection = m_IsClockwise;

            for (int i = 0; i < curves.Count; i++)
            {
                Bezier4x3 c = curves[i];
                int next = (i + 1) % curves.Count;

                result[i] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = reverseDirection ? new Bezier4x3(c.d, c.c, c.b, c.a) : c,
                    m_NodeIndex = reverseDirection ? new int2(next, i) : new int2(i, next),
                    m_ParentMesh = new int2(-1, -1)
                };
            }

            return result;
        }

        private void RotateCurveStart(List<Bezier4x3> curves, int startIndex)
        {
            if (curves == null || curves.Count == 0)
                return;

            startIndex %= curves.Count;
            if (startIndex < 0)
                startIndex += curves.Count;

            if (startIndex == 0)
                return;

            List<Bezier4x3> rotated = new();

            for (int i = 0; i < curves.Count; i++)
            {
                rotated.Add(curves[(startIndex + i) % curves.Count]);
            }

            curves.Clear();
            curves.AddRange(rotated);
        }
        #endregion
    }
}