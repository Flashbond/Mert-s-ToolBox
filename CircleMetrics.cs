using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox
{
    public readonly struct CircleMetrics
    {
        public readonly int OuterDiameterMeters;
        public readonly int CenterDiameterMeters;
        public readonly int InnerDiameterMeters;

        public readonly float OuterDiameterUnits;
        public readonly float InnerDiameterUnits;

        private CircleMetrics(
            int outerDiameterMeters,
            int centerDiameterMeters,
            int innerDiameterMeters,
            float outerDiameterUnits,
            float innerDiameterUnits)
        {
            OuterDiameterMeters = outerDiameterMeters;
            CenterDiameterMeters = centerDiameterMeters;
            InnerDiameterMeters = innerDiameterMeters;

            OuterDiameterUnits = outerDiameterUnits;
            InnerDiameterUnits = innerDiameterUnits;
        }

        public static CircleMetrics FromOuterDiameter(int outerDiameterMeters, float roadWidth)
        {
            int widthMeters = (int)math.round(roadWidth);

            int outer = math.max(0, outerDiameterMeters);
            int center = math.max(0, outer - widthMeters);
            int inner = math.max(0, outer - (widthMeters * 2));

            float outerU = outer / 8f;
            float innerU = inner / 8f;

            return new CircleMetrics(
                outer,
                center,
                inner,
                outerU,
                innerU
            );
        }
       
    }
}