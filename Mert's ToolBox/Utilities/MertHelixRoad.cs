using Colossal.Serialization.Entities;
using Unity.Entities;

namespace MertsToolBox.Utilities
{
    /// <summary>
    /// Tag placed on road edges created by the helix tool. Pillars of tagged roads, or pillars standing on
    /// tagged roads, may end on the road below them even when another tool is editing the network.
    /// Saved with the city through IEmptySerializable (no payload).
    /// </summary>
    public struct MertHelixRoad : IComponentData, IQueryTypeParameter, IEmptySerializable
    {
    }
}
