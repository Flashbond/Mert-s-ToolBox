using System.Collections.Generic;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MertsToolBox.Systems.SubSystems
{
    /// <summary>
    /// Keeps the flatten preview's roads on the flattened ground.
    /// The object tool computes the heights of the stamp's roads from the game's CPU height copy, which never contains
    /// our preview overlay: over water it measures against the sea floor, splits the roads at the shore and raises the
    /// water parts as bridges (tall pillars), although the overlay shows solid, flat ground there.
    /// This system corrects the tool's OUTPUT - its net course definitions - before the game generates the preview
    /// from them (register before GenerateObjectsSystem in Modification1, next to MertPlacementGhostSuppressSystem):
    /// every new course of our roads gets the flat road height the commit will use and the tool elevation. The game then
    /// generates flat, ground-level preview roads through its normal pass; the preview entities are never touched.
    /// Course ends attached to existing entities (m_Entity set) keep their height.
    /// </summary>
    public partial class MertFlattenCourseSystem : SystemBase
    {
        private const float k_Tolerance = 0.01f;

        private EntityQuery m_CourseQuery;

        /// <summary>Builds the query of the tool's net course definitions.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            m_CourseQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<CreationDefinition>(), ComponentType.ReadWrite<NetCourse>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });
        }

        /// <summary>Flattens our courses while a flatten preview is shown; does nothing otherwise.</summary>
        protected override void OnUpdate()
        {
            if (!MertsToolBox.Management.MertToolState.FlattenToolActive || m_CourseQuery.IsEmptyIgnoreFilter)
                return;

            MertToolBoxTerrainFlattenSystem flatten = MertToolBoxTerrainFlattenSystem.Instance;
            if (flatten == null || !flatten.TryGetPreviewRoadHeight(out float roadY, out float elevation, out HashSet<Entity> roads))
                return;

            using NativeArray<Entity> courses = m_CourseQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity e in courses)
            {
                CreationDefinition definition = EntityManager.GetComponentData<CreationDefinition>(e);
                if (definition.m_Original != Entity.Null || !roads.Contains(definition.m_Prefab))
                    continue;

                NetCourse course = EntityManager.GetComponentData<NetCourse>(e);
                bool changed = false;

                float ya = course.m_StartPosition.m_Entity == Entity.Null ? roadY : course.m_Curve.a.y;
                float yd = course.m_EndPosition.m_Entity == Entity.Null ? roadY : course.m_Curve.d.y;

                changed |= SetY(ref course.m_Curve.a.y, ya);
                changed |= SetY(ref course.m_Curve.b.y, math.lerp(ya, yd, 1f / 3f));
                changed |= SetY(ref course.m_Curve.c.y, math.lerp(ya, yd, 2f / 3f));
                changed |= SetY(ref course.m_Curve.d.y, yd);

                if (course.m_StartPosition.m_Entity == Entity.Null)
                    changed |= FlattenEnd(ref course.m_StartPosition, roadY, elevation);
                if (course.m_EndPosition.m_Entity == Entity.Null)
                    changed |= FlattenEnd(ref course.m_EndPosition, roadY, elevation);

                if (changed)
                    EntityManager.SetComponentData(e, course);
            }
        }

        /// <summary>Puts a free course end on the flat road height with the tool elevation.</summary>
        private static bool FlattenEnd(ref CoursePos pos, float roadY, float elevation)
        {
            bool changed = SetY(ref pos.m_Position.y, roadY);
            float2 el = new(elevation);
            if (math.any(math.abs(pos.m_Elevation - el) > k_Tolerance))
            {
                pos.m_Elevation = el;
                changed = true;
            }
            return changed;
        }

        /// <summary>Sets a height when it differs; returns true if it changed.</summary>
        private static bool SetY(ref float current, float target)
        {
            if (math.abs(current - target) <= k_Tolerance)
                return false;
            current = target;
            return true;
        }
    }
}
