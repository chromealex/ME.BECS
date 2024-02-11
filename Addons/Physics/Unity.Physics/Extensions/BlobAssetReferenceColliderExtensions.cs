using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Unity.Physics.Extensions
{
    /// <summary>   A blob asset reference collider extension. Enables casting to various collider types. </summary>
    public static class BlobAssetReferenceColliderExtension
    {
        /// <summary>   Get cast reference to the Collider inside a BlobAssetReference container. </summary>
        ///
        /// <typeparam name="To">   Type of to. </typeparam>
        /// <param name="col"> The BlobAssetReference&lt;Collider&gt; instance that we're attempting to
        /// extract data from. </param>
        ///
        /// <returns>   A reference to the Collider instance, cast to the specified type. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref To As<To>(this BlobAssetReference<Collider> col)
            where To : unmanaged, ICollider
        {
            unsafe
            {
                SafetyChecks.CheckColliderTypeAndThrow<To>(col.Value.Type);
                return ref *(To*)col.GetUnsafePtr();
            }
        }

        /// <summary>   Get cast pointer to the Collider inside a BlobAssetReference container. </summary>
        ///
        /// <typeparam name="To">   Type of to. </typeparam>
        /// <param name="col"> The BlobAssetReference&lt;Collider&gt; instance that we're attempting to
        /// extract data from. </param>
        ///
        /// <returns>   A pointer to the Collider instance, cast to the specified type. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static To* AsPtr<To>(this BlobAssetReference<Collider> col)
            where To : unmanaged, ICollider
        {
            SafetyChecks.CheckColliderTypeAndThrow<To>(col.Value.Type);
            return (To*)col.GetUnsafePtr();
        }

        /// <summary>
        /// A BlobAssetReference&lt;Collider&gt; extension method that converts a BlobAssetReference&lt;Collider&gt; to a pointer.
        /// </summary>
        ///
        /// <param name="col">  The BlobAssetReference&lt;Collider&gt; to act on. </param>
        ///
        /// <returns>   Null if it fails, else a pointer to a Collider. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static Collider* AsPtr(this BlobAssetReference<Collider> col)
        {
            return (Collider*)col.GetUnsafePtr();
        }

        /// <summary>
        /// Get a PhysicsComponent instance containing this BlobAssetReference&lt;Collider&gt;
        /// </summary>
        ///
        /// <param name="col"> The BlobAssetReference&lt;Collider&gt; instance that we're attempting to
        /// extract data from. </param>
        ///
        /// <returns>   A PhysicsComponent instance. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PhysicsCollider AsComponent(this BlobAssetReference<Collider> col)
        {
            return new PhysicsCollider() { Value = col };
        }

        /// <summary>
        /// Set the Collider* property of a ColliderCastInput struct, avoiding the need for an unsafe
        /// block in developer code.
        /// </summary>
        ///
        /// <param name="input"> [in,out] The ColliderCastInput instance that needs the Collider* property
        /// set. </param>
        /// <param name="col">   The BlobAssetReference&lt;Collider&gt; instance that we're attempting to
        /// extract data from. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCollider(ref this ColliderCastInput input, BlobAssetReference<Collider> col)
        {
            unsafe
            {
                input.Collider = col.AsPtr();
            }
        }

        /// <summary>
        /// Set the Collider* property of a ColliderDistanceInput struct, avoiding the need for an unsafe
        /// block in developer code.
        /// </summary>
        ///
        /// <param name="input"> [in,out] The ColliderDistanceInput instance that needs the Collider*
        /// property set. </param>
        /// <param name="col">   The BlobAssetReference&lt;Collider&gt; instance that we're attempting to
        /// extract data from. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCollider(ref this ColliderDistanceInput input, BlobAssetReference<Collider> col)
        {
            unsafe
            {
                input.Collider = col.AsPtr();
            }
        }
    }
}
