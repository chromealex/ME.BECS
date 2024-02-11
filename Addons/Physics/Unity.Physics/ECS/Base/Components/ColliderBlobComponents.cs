using Unity.Entities;

namespace Unity.Physics
{
    internal struct ColliderBlobCleanupData : ICleanupComponentData
    {
        public BlobAssetReference<Collider> Value;
    }

    internal struct EnsureUniqueColliderBlobTag : IComponentData {}
}
