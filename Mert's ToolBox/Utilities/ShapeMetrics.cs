using Unity.Mathematics;

namespace MertsToolBox.Utilities
{
    public readonly struct ShapeMetrics
    {
        public readonly int OuterDimensionMeters;
                public readonly float CenterDiameterMeters;
        public readonly float InnerDiameterMeters;
        public readonly float OuterDimensionUnits;
        public readonly float InnerDimensionUnits;
        public readonly float BuildRadius;

        private ShapeMetrics(
            int outerDimensionMeters,
            float centerDiameterMeters,
            float innerDiameterMeters,
            float outerDimensionUnits,
            float innerDimensionUnits,
            float buildRadius)
        {
            OuterDimensionMeters = outerDimensionMeters;
            CenterDiameterMeters = centerDiameterMeters;
            InnerDiameterMeters = innerDiameterMeters;
            OuterDimensionUnits = outerDimensionUnits;
            InnerDimensionUnits = innerDimensionUnits;
            BuildRadius = buildRadius;
        }

        public static ShapeMetrics FromOuterDimension(int targetOuterDimension, float roadWidth)
        {
            int outer = math.max(0, targetOuterDimension);
            float center = math.max(0f, outer - roadWidth);
            float inner = math.max(0f, outer - (roadWidth * 2f));
            float outerU = outer / 8f;
            float innerU = inner / 8f;
            float buildRadius = center * 0.5f;

            return new ShapeMetrics(
                outer,
                center,
                inner,
                outerU,
                innerU,
                buildRadius
            );
        }
    }
}