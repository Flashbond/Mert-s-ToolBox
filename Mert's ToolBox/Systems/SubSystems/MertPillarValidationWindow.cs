using System.Collections.Generic;
using Game.Common;
using Game.Tools;
using Unity.Entities;
using ObjectElevation = Game.Objects.Elevation;
using ObjectElevationFlags = Game.Objects.ElevationFlags;

namespace MertsToolBox.Systems
{
    /// <summary>
    /// Shared state for pillars that stand on a lower road. Right before tool validation their elevation is
    /// made slightly negative so they only carry the Underground collision mask; right after validation the
    /// original value is restored. Nothing outside the validation window ever sees the temporary value.
    /// </summary>
    internal static class MertPillarValidationWindow
    {
        internal const float k_ValidationElevation = -0.01f;

        internal struct Pending
        {
            public Entity Pillar;
            public bool HadElevation;
            public ObjectElevation Original;
        }

        internal static readonly Dictionary<Entity, Entity> Tracked = new();
        internal static readonly List<Pending> PendingRestore = new();
        internal static readonly HashSet<Entity> FrameOnly = new();

        /// <summary>Registers a temp pillar together with the road entity it now stands on.</summary>
        internal static void Track(Entity pillar, Entity road)
        {
            Tracked[pillar] = road;
        }

        /// <summary>Registers a built (non-temp) pillar for this frame's validation only.</summary>
        internal static void TrackForFrame(Entity pillar)
        {
            FrameOnly.Add(pillar);
        }

        /// <summary>Forgets every tracked pillar (tool closed).</summary>
        internal static void Clear()
        {
            Tracked.Clear();
            FrameOnly.Clear();
        }
    }

    public partial class MertPillarValidationPrepareSystem : SystemBase
    {
        private readonly List<Entity> m_Stale = new();

        /// <summary>Gives every tracked, still-alive temp pillar a slightly negative elevation for this frame's validation.</summary>
        protected override void OnUpdate()
        {
            if (MertPillarValidationWindow.Tracked.Count == 0 && MertPillarValidationWindow.FrameOnly.Count == 0)
                return;

            m_Stale.Clear();
            foreach (KeyValuePair<Entity, Entity> kv in MertPillarValidationWindow.Tracked)
            {
                Entity pillar = kv.Key;
                Entity road = kv.Value;
                bool pillarAlive = EntityManager.Exists(pillar) && EntityManager.HasComponent<Temp>(pillar)
                                   && !EntityManager.HasComponent<Deleted>(pillar);
                bool roadAlive = road == Entity.Null || (EntityManager.Exists(road) && !EntityManager.HasComponent<Deleted>(road));
                if (!pillarAlive || !roadAlive)
                {
                    m_Stale.Add(pillar);
                    continue;
                }

                Prepare(pillar);
            }

            foreach (Entity key in m_Stale)
                MertPillarValidationWindow.Tracked.Remove(key);

            foreach (Entity pillar in MertPillarValidationWindow.FrameOnly)
            {
                if (MertPillarValidationWindow.Tracked.ContainsKey(pillar))
                    continue;
                if (!EntityManager.Exists(pillar) || EntityManager.HasComponent<Deleted>(pillar))
                    continue;
                Prepare(pillar);
            }
            MertPillarValidationWindow.FrameOnly.Clear();
        }

        /// <summary>Stores the pillar's current elevation for restore and sets the temporary negative value.</summary>
        private void Prepare(Entity pillar)
        {
            var pending = new MertPillarValidationWindow.Pending { Pillar = pillar };
            var temporary = new ObjectElevation
            {
                m_Elevation = MertPillarValidationWindow.k_ValidationElevation,
                m_Flags = (ObjectElevationFlags)0,
            };

            if (EntityManager.HasComponent<ObjectElevation>(pillar))
            {
                pending.HadElevation = true;
                pending.Original = EntityManager.GetComponentData<ObjectElevation>(pillar);
                EntityManager.SetComponentData(pillar, temporary);
            }
            else
            {
                EntityManager.AddComponentData(pillar, temporary);
            }

            MertPillarValidationWindow.PendingRestore.Add(pending);
        }
    }

    public partial class MertPillarValidationRestoreSystem : SystemBase
    {
        /// <summary>Puts back the elevation every prepared pillar had before validation.</summary>
        protected override void OnUpdate()
        {
            List<MertPillarValidationWindow.Pending> pending = MertPillarValidationWindow.PendingRestore;
            if (pending.Count == 0)
                return;

            foreach (MertPillarValidationWindow.Pending p in pending)
            {
                if (!EntityManager.Exists(p.Pillar))
                    continue;

                if (p.HadElevation)
                    EntityManager.SetComponentData(p.Pillar, p.Original);
                else if (EntityManager.HasComponent<ObjectElevation>(p.Pillar))
                    EntityManager.RemoveComponent<ObjectElevation>(p.Pillar);
            }

            pending.Clear();
        }
    }
}
