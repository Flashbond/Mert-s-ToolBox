using Game.Objects;
using Game.Tools;
using MertsToolBox.Management;
using Unity.Entities;

namespace MertsToolBox.Systems.SubSystems
{
    public partial class InjectDummyParentToPillarsSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            RequireForUpdate<Temp>();
            RequireForUpdate<Pillar>();
        }

        protected override void OnUpdate()
        {
            if (!MertToolState.HelixCleanupRequested) return;
            Entity dummyParent = new() { Index = 16384 * 8192 - 1, Version = 1 };

            Entities
                .WithAll<Temp, Pillar>()
                .WithChangeFilter<Transform>()
                .ForEach((Entity entity, ref Attached attached) =>
                {
                    if (attached.m_Parent == Entity.Null)
                    {
                        attached.m_Parent = dummyParent;
                    }
                }).WithoutBurst().Run();
        }
    }
}