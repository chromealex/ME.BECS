using Unity.Entities;

namespace ME.BECS.Addons.Physics.Runtime.Extensions {

    public static class EntityExt {

        public static Entity ToPhysicsEntity(this Ent ent) {

            return new Entity() {
                Index = (int) ent.id,
                Version = (int) ent.Version,
            };

        }

    }

}
