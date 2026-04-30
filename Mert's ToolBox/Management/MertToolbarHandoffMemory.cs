using Colossal.Entities;
using Game.Prefabs;
using MertsToolBox.Systems;
using System;
using Unity.Entities;

namespace MertsToolBox.Management
{
    internal static class MertToolbarHandoffMemory
    {
        #region Tool & UI State Validation
        /// <summary>
        /// Checks if any of the custom tools in the toolbox are currently enabled.
        /// </summary>
        public static bool IsAnyCustomToolOpen()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return false;

            var circle = world.GetExistingSystemManaged<CircleToolSystem>();
            var helix = world.GetExistingSystemManaged<HelixToolSystem>();
            var superEllipse = world.GetExistingSystemManaged<SuperEllipseToolSystem>();
            var grid = world.GetExistingSystemManaged<GridToolSystem>();

            return (circle != null && circle.ToolEnabled) ||
                   (helix != null && helix.ToolEnabled) ||
                   (superEllipse != null && superEllipse.ToolEnabled) ||
                   (grid != null && grid.ToolEnabled);
        }

        /// <summary>
        /// Determines if the provided prefab is our custom shared prebaked stamp.
        /// </summary>
        public static bool IsCurrentStamp(PrefabBase prefab)
        {
            if (prefab == null || string.IsNullOrEmpty(prefab.name))
                return false;

            string name = prefab.name;

            return name.StartsWith("MertsToolBox_SharedPrebakedStamp", StringComparison.Ordinal) ||
                   name.StartsWith("MertsToolBox_RoadStamp_", StringComparison.Ordinal) ||
                   name.StartsWith("MertsToolBox_WarmupStamp_", StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks if the provided category entity corresponds to a roads category.
        /// </summary>
        public static bool IsRoadsCategory(Entity categoryEntity)
        {
            if (!TryResolvePrefab(categoryEntity, out var prefab))
                return false;

            string lower = (prefab?.name ?? string.Empty).ToLowerInvariant();
            return lower.Contains("road");
        }

        /// <summary>
        /// Determines if the provided asset entity is a network prefab representing a road.
        /// </summary>
        public static bool IsRoadNetPrefab(Entity assetEntity, out NetPrefab netPrefab)
        {
            netPrefab = null;

            if (!TryResolvePrefab(assetEntity, out var prefab))
                return false;

            if (prefab is NetPrefab net)
            {
                netPrefab = net;
                return true;
            }

            return false;
        }
        #endregion

        #region Entity & Prefab Resolution
        /// <summary>
        /// Attempts to retrieve the underlying prefab base for a given ECS entity.
        /// </summary>
        public static bool TryResolvePrefab(Entity entity, out PrefabBase prefab)
        {
            prefab = null;

            if (entity == Entity.Null)
                return false;

            var world = World.DefaultGameObjectInjectionWorld;
            var prefabSystem = world?.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null)
                return false;

            return prefabSystem.TryGetPrefab<PrefabBase>(entity, out prefab);
        }

        /// <summary>
        /// Attempts to retrieve the corresponding ECS entity for a given network prefab.
        /// </summary>
        public static bool TryResolveEntity(NetPrefab prefab, out Entity entity)
        {
            entity = Entity.Null;

            if (prefab == null)
                return false;

            var world = World.DefaultGameObjectInjectionWorld;
            var prefabSystem = world?.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null)
                return false;

            entity = prefabSystem.GetEntity(prefab);
            return entity != Entity.Null;
        }

        /// <summary>
        /// Attempts to resolve the parent category entity for a given asset entity.
        /// </summary>
        public static bool TryResolveCategoryFromAsset(Entity assetEntity, out Entity category)
        {
            category = Entity.Null;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return false;

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(assetEntity))
                return false;

            if (entityManager.TryGetComponent(assetEntity, out UIObjectData uiObject) &&
                uiObject.m_Group != Entity.Null)
            {
                category = uiObject.m_Group;
                return true;
            }

            return false;
        }
        #endregion
    }
}