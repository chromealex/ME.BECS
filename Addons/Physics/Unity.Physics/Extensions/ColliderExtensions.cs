namespace Unity.Physics.Extensions
{
    /// <summary>   Utility functions acting on colliders. </summary>
    public static class ColliderExtensions
    {
        /// <summary>
        ///  Converts a <see cref="Collider"/> to a <see cref="UnityEngine.Mesh"/>.
        /// </summary>
        /// <param name="collider"> The collider to convert to a mesh.</param>
        /// <returns> The created mesh. </returns>
        public static UnityEngine.Mesh ToMesh(in this Collider collider)
        {
            return MeshUtilities.CreateMeshFromCollider(collider);
        }
    }
}
