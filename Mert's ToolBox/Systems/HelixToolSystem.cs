using Colossal.Mathematics;
using Game.Prefabs;
using Game.Tools;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.UI;
using MertsToolBox.Utilities.Preset;
using System.Collections.Generic;
using Unity.Mathematics;

namespace MertsToolBox.Systems
{
    public partial class HelixToolSystem : MertBaseToolSystem
    {
        #region Fields & Properties
        private int m_CurrentSessionDiameter = -1;
        public readonly int[] m_DiameterSteps = new int[] { 8, 4, 2, 1 };
        private int m_CurrentDiameterStepIndex = 0;

        private float m_CurrentSessionTurns = -1f;
        public readonly float[] m_TurnSteps = new float[] { 2f, 1f, 0.50f, 0.25f };
        private int m_CurrentTurnStepIndex = 0;

        private float m_CurrentSessionClearance = -1f;
        public readonly float[] m_ClearanceSteps = new float[] { 2f, 1f, 0.50f, 0.25f };
        private int m_CurrentClearanceStepIndex = 0;

        private const float k_GlobalMaxSlope = 0.15f;
        private const float k_EntryTailPillarMargin = 2.0f;
        private const int k_MaxDiameter = 940;
        private const int k_PierMaxDiameter = 15;

        private int m_PendingDiameterChange = 0;
        private int m_TargetDiameterStep = -1;
        private int m_PendingTurnChange = 0;
        private float m_TargetTurnStep = -1f;
        private int m_PendingClearanceChange = 0;
        private float m_TargetClearanceStep = -1f;

        private bool m_IsClockwise = true;
        private bool m_IsAscending = true;
        private bool m_PendingToggleAscension = false;

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        public override string ToolId => "Helix";
        public override string ToolName => "Procedural Helix";

        /// <summary>
        /// Indicates whether this tool requires snap enforcement.
        /// </summary>
        protected override bool RequiresSnapEnforcement => false;
        protected override bool OverridesObjectToolSnapMask => true;
        protected override bool WritesSubNetSnapMetadata => false;
        protected override bool SupportsFlatten => false;

        protected override Snap GetObjectToolSnapMask()
        {
            bool isPier = IsCurrentPierLikePrefab();
            return isPier ? Snap.ExistingGeometry : Snap.ExistingGeometry | Snap.NetArea;
        }
        #endregion

        #region Preset System
        public override MertToolPreset CreatePresetSnapshot()
        {
            NetPrefab prefab = TryGetCurrentSelectedRoadPrefab();
            string prefabName = prefab?.name ?? "UnknownRoad";

            int diameter = GetCurrentDiameter();
            float turns = GetCurrentTurns();
            float clearance = GetCurrentClearance();

            return new MertToolPreset
            {
                ToolId = ToolId,
                ToolName = ToolName,
                PrefabName = prefabName,

                DisplayName = SanitizeFileName(
                    $"{ToolId}_{prefabName}_Diameter{diameter}m_Turn{turns:0.0}_Clearance{clearance:0.0}m_{(m_IsAscending ? "Up" : "Down")}_{(m_IsClockwise ? "CW" : "CCW")}" +
                    $"{(MertToolState.SuppressCrosswalks ? "_NoCrosswalks" : "")}"
                ),

                Values = new Dictionary<string, float>
                {
                    ["Diameter"] = diameter,
                    ["Turns"] = turns,
                    ["Clearance"] = clearance,
                    ["Climbing"] = m_IsAscending ? 1f : 0f,
                    ["Clockwise"] = m_IsClockwise ? 1f : 0f,
                    ["NoCrosswalks"] = MertToolState.SuppressCrosswalks ? 1f : 0f
                }
            };
        }

        public override void ApplyPresetSnapshot(MertToolPreset preset)
        {
            if (preset?.Values == null)
                return;

            if (preset.Values.TryGetValue("Diameter", out float diameter))
                SetCurrentDiameter((int)diameter);

            if (preset.Values.TryGetValue("Turns", out float turns))
                SetCurrentTurns(turns);

            if (preset.Values.TryGetValue("Clearance", out float clearance))
                SetCurrentClearance(clearance);

            if (preset.Values.TryGetValue("Climbing", out float ascending))
                m_IsAscending = ascending > 0.5f;

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
            if (toolIndex != 2)
                return;

            switch (paramIndex)
            {
                case 1:
                    m_CurrentSessionDiameter = Mod.settings.DefaultHelixDiameter;
                    break;

                case 2:
                    m_CurrentSessionTurns = Mod.settings.DefaultTurns;
                    break;

                case 3:
                    m_CurrentSessionClearance = Mod.settings.DefaultClearance;
                    break;
            }

            QueuePreviewRebuild();
        }

        /// <summary>
        /// Queues a change in the diameter based on the given direction.
        /// </summary>
        public void QueueDiameterChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingDiameterChange += direction;
        }

        /// <summary>
        /// Queues a step cycle for the diameter adjustment.
        /// </summary>
        public void QueueSetDiameterStep(int value) => m_TargetDiameterStep = value;

        /// <summary>
        /// Queues a change in the number of turns based on the given direction.
        /// </summary>
        public void QueueTurnChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingTurnChange += direction;
        }

        /// <summary>
        /// Queues a step cycle for the turn adjustment.
        /// </summary>
        public void QueueSetTurnStep(float value) => m_TargetTurnStep = value;

        /// <summary>
        /// Queues a change in the clearance based on the given direction.
        /// </summary>
        public void QueueClearanceChange(int direction)
        {
            RegisterUndoForButton();
            m_PendingClearanceChange += direction;
        }

        /// <summary>
        /// Queues a step cycle for the clearance adjustment.
        /// </summary>
        public void QueueSetClearanceStep(float value) => m_TargetClearanceStep = value;

        /// <summary>
        /// Sets turn direction.
        /// </summary>
        public void QueueToggleDirection()
        {
            RegisterUndoForButton();

            m_IsClockwise = !m_IsClockwise;
            QueuePreviewRebuild();
        }
        public override void QueueToggleMainDirection()
        {
            if (!IsCurrentPrefabValidForOneWayPattern()) return;
            RegisterUndoForButton();
            m_PendingToggleAscension = true;
        }

        #endregion

        #region Metrics & Data Retrieval
        /// <summary>
        /// Retrieves the step size for diameter adjustments.
        /// </summary>
        public int GetDiameterStepSize() => GetCurrentStepValue(m_CurrentDiameterStepIndex, m_DiameterSteps);

        /// <summary>
        /// Retrieves the step size for turn adjustments.
        /// </summary>
        public float GetTurnStepSize() => GetCurrentStepValue(m_CurrentTurnStepIndex, m_TurnSteps);

        /// <summary>
        /// Retrieves the step size for clearance adjustments.
        /// </summary>
        public float GetClearanceStepSize() => GetCurrentStepValue(m_CurrentClearanceStepIndex, m_ClearanceSteps);

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
        public float GetCurrentClearance() { if (m_CurrentSessionClearance < 0) m_CurrentSessionClearance = Mod.settings != null ? Mod.settings.DefaultClearance : 9f; return m_CurrentSessionClearance; }

        public override bool GetMainDirectionState() => m_IsAscending;

        public bool GetIsClockwise() => m_IsClockwise;

        private float GetMaxSlopeLimit()
        {
            return k_GlobalMaxSlope;
        }
        /// <summary>
        /// Calculates the minimum allowed diameter based on the road prefab width and clearance.
        /// </summary>
        private int GetMinimumAllowedDiameter()
        {
            float minDiameter = m_CurrentRoadWidth * 3.0f;
            return (int)math.ceil(minDiameter);
        }
        private int GetMaximumAllowedDiameter()
        {
            int max = IsCurrentPierLikePrefab() ? k_PierMaxDiameter : k_MaxDiameter;
            return math.max(max, GetMinimumAllowedDiameter());
        }
        private float GetMinimumAllowedClearance()
        {
            return IsCurrentPierLikePrefab() ? 3.5f : 10.0f;
        }
        private float GetMaximumAllowedClearance()
        {
            return IsCurrentPierLikePrefab() ? 5.0f : 15.0f;
        }
        #endregion

        #region Tool State & Lifecycle
        /// <summary>
        /// Called when the tool is activated to handle state cleanup or initialization.
        /// </summary>
        protected override void OnToolActivated()
        {
            MertToolState.HelixCleanupRequested = true;
            MertHelixBuildOption.OnHelixOpened();
        }

        /// <summary>
        /// Called when the tool is deactivated to perform necessary cleanup flags.
        /// </summary>
        protected override void OnToolDeactivated()
        {
            MertToolState.HelixCleanupRequested = false;
            MertToolState.ActiveHelixUsesPierLikePrefab = false;
            MertHelixBuildOption.OnHelixClosed();

            if (m_ObjectToolSystem != null)
                m_ObjectToolSystem.selectedSnap = MertToolState.BuildGlobalSnapMask();
        }

        protected override void OnDestroy()
        {
            MertHelixBuildOption.OnHelixClosed();
            base.OnDestroy();
        }
        #endregion

        #region Core Tool Processing
        /// <summary>
        /// Processes user inputs and applies queued changes to the tool state.
        /// </summary>
        protected override void ProcessToolInput()
        {
            MertHelixBuildOption.Update();
            EnforceOneWayOnlyOptions();

            if (m_PendingToggleAscension)
            {
                m_IsAscending = !m_IsAscending;
                m_PendingToggleAscension = false;
                QueuePreviewRebuild();
            }

            if (m_TargetDiameterStep != -1)
            {
                m_CurrentDiameterStepIndex = GetIndexFromValue(
                    m_TargetDiameterStep,
                    m_DiameterSteps,
                    m_CurrentDiameterStepIndex
                );
                m_TargetDiameterStep = -1;
            }

            if (m_PendingDiameterChange != 0) { ChangeDiameter(m_PendingDiameterChange); m_PendingDiameterChange = 0; }
            if (m_TargetTurnStep != -1)
            {
                m_CurrentTurnStepIndex = GetIndexFromValue(
                    m_TargetTurnStep,
                    m_TurnSteps,
                    m_CurrentTurnStepIndex
                );
                m_TargetTurnStep = -1;
            }

            if (m_PendingTurnChange != 0) { ChangeTurns(m_PendingTurnChange); m_PendingTurnChange = 0; }
            if (m_TargetClearanceStep != -1)
            {
                m_CurrentClearanceStepIndex = GetIndexFromValue(
                    m_TargetClearanceStep,
                    m_ClearanceSteps,
                    m_CurrentClearanceStepIndex
                );
                m_TargetClearanceStep = -1;
            }

            if (m_PendingClearanceChange != 0) { ChangeClearance(m_PendingClearanceChange); m_PendingClearanceChange = 0; }

            if (Mod.settings != null && Mod.settings.UseCtrlWheelForHelixTurnAdjustment &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed)
            {
                int scrollDir = GetScrollDirection();
                if (scrollDir != 0)
                {
                    RegisterUndoForWheel();
                    SetCurrentTurns(GetCurrentTurns() + (scrollDir * 0.125f));
                }
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
            SetCurrentClearance(GetNextStepAlignedValue(GetCurrentClearance(), stepSize, direction));
        }

        /// <summary>
        /// Safely sets the current diameter within legal bounds and queues a preview rebuild.
        /// </summary>
        private void SetCurrentDiameter(int diameter)
        {
            int clamped = math.clamp(diameter, GetMinimumAllowedDiameter(), GetMaximumAllowedDiameter());
            if (m_CurrentSessionDiameter == clamped)
                return;

            m_CurrentSessionDiameter = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Clamps and applies the specified number of turns and queues a preview rebuild.
        /// </summary>
        private void SetCurrentTurns(float turns)
        {
            float clamped = math.clamp(turns, 0.5f, 120f);
            if (math.abs(m_CurrentSessionTurns - clamped) < 0.001f) return;

            m_CurrentSessionTurns = clamped;
            QueuePreviewRebuild();
        }

        /// <summary>
        /// Safely sets the current clearance and queues a preview rebuild.
        /// </summary>
        private void SetCurrentClearance(float clearance)
        {
            float clamped = math.clamp(
                clearance,
                GetMinimumAllowedClearance(),
                GetMaximumAllowedClearance()
            );

            if (math.abs(m_CurrentSessionClearance - clamped) < 0.001f)
                return;

            m_CurrentSessionClearance = clamped;
            QueuePreviewRebuild();
        }
        #endregion

        #region Geometry Generation
        private void OptimizeAccordingToMaxSlopeLimit()
        {
            float maxS = GetMaxSlopeLimit();
            float pi = math.PI;

            float minD = GetMinimumAllowedDiameter();
            float maxD = GetMaximumAllowedDiameter();
            float minH = GetMinimumAllowedClearance();
            float maxH = GetMaximumAllowedClearance();

            float d = m_CurrentSessionDiameter;
            float h = m_CurrentSessionClearance;

            float requiredD = h / (pi * maxS);
            if (d < requiredD)
                d = math.ceil(requiredD);

            d = math.clamp(d, minD, maxD);

            float allowedMaxH = d * pi * maxS;
            if (h > allowedMaxH)
                h = math.max(minH, allowedMaxH);

            h = math.clamp(h, minH, maxH);

            m_CurrentSessionDiameter = (int)d;
            m_CurrentSessionClearance = h;
        }

        /// <summary>
        /// Attempts to generate the sub-networks and cells for the helix geometry.
        /// </summary>
        protected override bool TryGenerateGeometry(NetPrefab roadPrefab, out ObjectSubNetInfo[] subNets, out int widthCells, out int depthCells, out float costElevation)
        {
            OptimizeAccordingToMaxSlopeLimit();

            subNets = null;
            widthCells = depthCells = 0;
            costElevation = 0f;

            float buildRadius = (m_CurrentSessionDiameter - m_CurrentRoadWidth) * 0.5f;

            if (buildRadius < m_CurrentRoadWidth)
                return false;

            float turns = math.max(0.5f, m_CurrentSessionTurns);
            int segments = math.max(2, (int)math.ceil(turns * 8f));
            float baseElevation = GetCurrentNetToolElevation();

            bool isPier = IsCurrentPierLikePrefab();
            MertToolState.ActiveHelixUsesPierLikePrefab = isPier;

            float entryTailLength = isPier ? 1.5f : 0.8f;
            float exitTailLength = isPier ? 0.5f : 1.0f;
            if (!isPier)
            {
                float w = m_CurrentRoadWidth;
                float outer = buildRadius + 0.5f * w + k_EntryTailPillarMargin;
                float inner = buildRadius - 0.5f * w;
                float clearLen = math.sqrt(math.max(0f, outer * outer - inner * inner));

                float requiredFactor = clearLen / buildRadius;
                if (requiredFactor > entryTailLength)
                    entryTailLength = requiredFactor;
                   
            }
            widthCells = depthCells = (int)math.ceil(m_CurrentSessionDiameter / 8f);
            costElevation = m_CurrentSessionClearance * turns;

            subNets = BuildHelixSubNets(roadPrefab, buildRadius, segments, baseElevation, m_CurrentSessionClearance, turns, entryTailLength, exitTailLength);
            return true;
        }

        /// <summary>
        /// Builds the bezier curves representing the helix's sub-networks based on mathematical parameters.
        /// </summary>
        private ObjectSubNetInfo[] BuildHelixSubNets(NetPrefab roadPrefab, float radius, int segments, float startElevation, float clearance, float totalTurns, float entryTailLength, float exitTailLength)
        {
            segments = math.max(8, segments);
            totalTurns = math.max(0.5f, totalTurns);

            float entryRampLength = radius * entryTailLength;
            float exitRampLength = radius * exitTailLength;

            float entryCustomSlope = 0.12f;
            float exitCustomSlope = 0.0f;

            ObjectSubNetInfo[] upwardSubNets = new ObjectSubNetInfo[segments + 2];

            float stepRadian = (totalTurns * math.PI * 2f) / segments;
            float stepHeight = (clearance * totalTurns) / segments;

            float helixSlope = clearance / (math.PI * 2f * radius);
            float tangentLength = radius * math.tan(stepRadian / 4f) * (4f / 3f);

            float dirMultiplier = m_IsClockwise ? -1f : 1f;

            float3[] points = new float3[segments + 1];
            float3[] forwardTangents = new float3[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float a = i * stepRadian;
                forwardTangents[i] = math.normalizesafe(new float3(
                    -math.sin(a),
                    helixSlope,
                    math.cos(a) * dirMultiplier
                ));
            }
            float3 startFlatDir = math.normalizesafe(new float3(forwardTangents[0].x, 0f, forwardTangents[0].z));
            float3 entryCustomTangent = math.normalizesafe(new float3(startFlatDir.x, entryCustomSlope, startFlatDir.z));

            float3 endFlatDir = math.normalizesafe(new float3(forwardTangents[segments].x, 0f, forwardTangents[segments].z));
            float3 exitCustomTangent = math.normalizesafe(new float3(endFlatDir.x, exitCustomSlope, endFlatDir.z));

            float tailHeightDrop = entryCustomTangent.y * entryRampLength;

            for (int i = 0; i <= segments; i++)
            {
                float a = i * stepRadian;
                points[i] = new float3(
                    math.cos(a) * radius,
                    startElevation + tailHeightDrop + (i * stepHeight),
                    math.sin(a) * radius * dirMultiplier
                );
            }

            float startOffset = 0.5f;

            float3 bottomStart = new(
                                    points[0].x - (startFlatDir.x * entryRampLength),
                                    startElevation + startOffset,
                                    points[0].z - (startFlatDir.z * entryRampLength)
                                );

            points[0] = new float3(points[0].x, startElevation + startOffset + tailHeightDrop, points[0].z);

            upwardSubNets[0] = new ObjectSubNetInfo
            {
                m_NetPrefab = roadPrefab,
                m_BezierCurve = new Bezier4x3(
                    bottomStart,
                    bottomStart + (startFlatDir * (entryRampLength / 3f)),
                    points[0] - (forwardTangents[0] * (entryRampLength / 3f)),
                    points[0]
                ),
                m_NodeIndex = new int2(0, 1),
                m_ParentMesh = new int2(-1, -1)
            };

            for (int i = 0; i < segments; i++)
            {
                upwardSubNets[i + 1] = new ObjectSubNetInfo
                {
                    m_NetPrefab = roadPrefab,
                    m_BezierCurve = new Bezier4x3(
                        points[i],
                        points[i] + forwardTangents[i] * tangentLength,
                        points[i + 1] - forwardTangents[i + 1] * tangentLength,
                        points[i + 1]
                    ),
                    m_NodeIndex = new int2(i + 1, i + 2),
                    m_ParentMesh = new int2(-1, -1)
                };
            }

            float3 topEnd = points[segments] + (exitCustomTangent * exitRampLength);

            upwardSubNets[segments + 1] = new ObjectSubNetInfo
            {
                m_NetPrefab = roadPrefab,
                m_BezierCurve = new Bezier4x3(
                    points[segments],
                    points[segments] + (exitCustomTangent * (exitRampLength / 3f)),
                    topEnd - (exitCustomTangent * (exitRampLength / 3f)),
                    topEnd
                ),
                m_NodeIndex = new int2(segments + 1, segments + 2),
                m_ParentMesh = new int2(-1, -1)
            };

            if (m_IsAscending)
            {
                return upwardSubNets;
            }

            int totalCount = upwardSubNets.Length;
            ObjectSubNetInfo[] downwardSubNets = new ObjectSubNetInfo[totalCount];

            for (int i = 0; i < totalCount; i++)
            {
                int targetIndex = totalCount - 1 - i;
                var sourceNet = upwardSubNets[i];

                downwardSubNets[targetIndex] = new ObjectSubNetInfo
                {
                    m_NetPrefab = sourceNet.m_NetPrefab,
                    m_BezierCurve = new Bezier4x3(
                        sourceNet.m_BezierCurve.d,
                        sourceNet.m_BezierCurve.c,
                        sourceNet.m_BezierCurve.b,
                        sourceNet.m_BezierCurve.a
                    ),
                    m_NodeIndex = new int2(sourceNet.m_NodeIndex.y, sourceNet.m_NodeIndex.x),
                    m_ParentMesh = sourceNet.m_ParentMesh
                };
            }

            return downwardSubNets;
        }
        #endregion
    }
}