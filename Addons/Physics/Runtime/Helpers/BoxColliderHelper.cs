using Unity.Physics;

namespace ME.BECS.Addons.Physics.Runtime.Helpers {

    public static unsafe class BoxColliderHelper {

        public static MemAllocatorPtrAuto<Collider> Create(in Ent ent, BoxGeometry geometry,
            CollisionFilter filter,
            Material material) {

            var blobAssetRef = BoxCollider.Create(geometry, filter, material);
            var memAllocatorPtr = new MemAllocatorPtrAuto<Collider>(in ent, blobAssetRef.GetUnsafePtr(), (uint)sizeof(Collider), alignment: 16);
            blobAssetRef.Dispose();

            return memAllocatorPtr;

        }
        
        public static MemAllocatorPtrAuto<Collider> Create(in Ent ent, BoxGeometry geometry) => BoxColliderHelper.Create(in ent, geometry, CollisionFilter.Default, Material.Default);

        public static MemAllocatorPtrAuto<Collider> Create(in Ent ent, BoxGeometry geometry, CollisionFilter filter) => BoxColliderHelper.Create(in ent, geometry, filter, Material.Default);

    }

}
