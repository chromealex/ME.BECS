using System;
using UnityEngine.Scripting;

namespace Unity.Entities
{
    /// <summary>
    /// This interface marks structs as 'unmanaged components' and classes as 'managed components'.
    /// </summary>
    /// <remarks>
    /// For more information, see the documentation on [components](xref:ecs-components).
    /// </remarks>
    [RequireImplementors]
    public interface IComponentData : IQueryTypeParameter
    {
    }

    /// <summary>
    /// An interface for creating structs that can be stored in a <see cref="DynamicBuffer{T}"/>.
    /// </summary>
    /// <remarks>
    /// See [Dynamic Buffers](xref:components-buffer-introducing) for additional information.
    /// </remarks>
    [RequireImplementors]
    public interface IBufferElementData
    {
    }

    /// <summary>
    /// Specifies the maximum number of elements to store inside a chunk.
    /// </summary>
    /// <remarks>
    /// Use this attribute on the declaration of your IBufferElementData subtype:
    ///
    /// <code>
    /// [InternalBufferCapacity(10)]
    /// public struct FloatBufferElement : IBufferElementData
    /// {
    ///     public float Value;
    /// }
    /// </code>
    ///
    /// All <see cref="DynamicBuffer{T}"/> with this type of element store the specified number of elements inside the
    /// chunk along with other component types in the same archetype. When the number of elements in the buffer exceeds
    /// this limit, the entire buffer is moved outside the chunk.
    ///
    /// [DefaultBufferCapacityNumerator](xref:Unity.Entities.TypeManager.DefaultBufferCapacityNumerator) defines
    /// the default number of elements.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct)]
    public class InternalBufferCapacityAttribute : Attribute
    {
        /// <summary>
        /// The number of elements stored inside the chunk.
        /// </summary>
        public readonly int Capacity;

        /// <summary>
        /// The number of elements stored inside the chunk.
        /// </summary>
        /// <param name="capacity"></param>
        public InternalBufferCapacityAttribute(int capacity)
        {
            Capacity = capacity;
        }
    }

    /// <summary>
    /// Specifies the maximum number of components of a type that can be stored in the same chunk.
    /// </summary>
    /// <remarks>Place this attribute on the declaration of a component, such as <see cref="IComponentData"/>, to
    /// limit the number of entities with that component which can be stored in a single chunk. Note that the actual
    /// limit on the number of entities in a chunk can be smaller, based on the actual size of all the components in the
    /// same <see cref="EntityArchetype"/> as the component defining this limit.
    ///
    /// If an archetype contains more than one component type specifying a chunk capacity limit, then the lowest limit
    /// is used.</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class MaximumChunkCapacityAttribute : Attribute
    {
        /// <summary>
        /// The maximum number of entities having this component type in an <see cref="ArchetypeChunk"/>.
        /// </summary>
        public readonly int Capacity;

        /// <summary>
        /// The maximum number of entities having this component type in an <see cref="ArchetypeChunk"/>.
        /// </summary>
        /// <param name="capacity"></param>
        public MaximumChunkCapacityAttribute(int capacity)
        {
            Capacity = capacity;
        }
    }

    /// <summary>
    /// States that a component type is serializable.
    /// </summary>
    /// <remarks>
    /// By default, ECS does not support storing pointer types in chunks. Apply this attribute to a component declaration
    /// to allow the use of pointers as fields in the component.
    ///
    /// Note that ECS does not perform any pre- or post-serialization processing to maintain pointer validity. When
    /// using this attribute, your code assumes responsibility for handling pointer serialization and deserialization.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class ChunkSerializableAttribute : Attribute
    {
    }

    // [TODO: Document shared components with Jobs...]
    /// <summary>
    /// An interface for a component type whose value is shared across all entities with the same value.
    /// </summary>
    /// <remarks>
    /// For more information, see the documentation on [Shared components](xref:components-shared).
    /// </remarks>
    [RequireImplementors]
    public interface ISharedComponentData : IQueryTypeParameter
    {
    }

    /// <summary>
    /// An interface for a component that must be removed individually after its entity is destroyed.
    /// </summary>
    /// <remarks>
    /// See [Cleanup Components](xref:components-cleanup) for additional information.
    /// </remarks>
    [RequireImplementors]
    public interface ICleanupComponentData : IComponentData
    {
    }

    /// <summary>
    /// Obsolete. Use <see cref="ICleanupComponentData"/> instead.
    /// </summary>
    /// <remarks>
    /// **Obsolete.** Use <see cref="ICleanupComponentData"/> instead. See [Cleanup Components](xref:components-cleanup) for additional information.
    ///
    /// An interface for a component that must be removed individually after its entity is destroyed.
    /// </remarks>
    [Obsolete("ISystemStateComponentData has been renamed to ICleanupComponentData. This old type has been kept for transition purposes, but it will not function correctly, so you should replace it with ICleanupComponentData immediately. (UnityUpgradable) -> ICleanupComponentData", true)]
    public interface ISystemStateComponentData : IComponentData
    {
    }

    /// <summary>
    /// An interface for a buffer component that must be removed individually after its entity is destroyed.
    /// </summary>
    /// <seealso cref="ICleanupComponentData"/>
    /// <seealso cref="IBufferElementData"/>
    [RequireImplementors]
    public interface ICleanupBufferElementData : IBufferElementData
    {
    }

    /// <summary>
    /// Obsolete. Use <see cref="ICleanupBufferElementData"/> instead.
    /// </summary>
    /// <remarks>**Obsolete.** Use <see cref="ICleanupBufferElementData"/> instead.
    ///
    /// An interface for a buffer component that must be removed individually after its entity is destroyed.</remarks>
    /// <seealso cref="ICleanupComponentData"/>
    /// <seealso cref="IBufferElementData"/>
    [Obsolete("ISystemStateBufferElementData has been renamed to ICleanupBufferElementData. This old type has been kept for transition purposes, but it will not function correctly, so you should replace it with ICleanupBufferElementData immediately. (UnityUpgradable) -> ICleanupBufferElementData", true)]
    public interface ISystemStateBufferElementData : IBufferElementData
    {
    }

    /// <summary>
    /// An interface for a shared component that must be removed individually after its entity is destroyed.
    /// </summary>
    /// <seealso cref="ICleanupComponentData"/>
    /// <seealso cref="ISharedComponentData"/>
    [RequireImplementors]
    public interface ICleanupSharedComponentData : ISharedComponentData
    {
    }

    /// <summary>
    /// Obsolete. Use <see cref="ICleanupSharedComponentData"/> instead.
    /// </summary>
    /// <remarks>**Obsolete.** Use <see cref="ICleanupSharedComponentData"/> instead.
    ///
    /// An interface for a shared component that must be removed individually after its entity is destroyed.</remarks>
    /// <seealso cref="ICleanupComponentData"/>
    /// <seealso cref="IBufferElementData"/>
    [Obsolete("ISystemStateSharedComponentData has been renamed to ICleanupSharedComponentData. This old type has been kept for transition purposes, but it will not function correctly, so you should replace it with ICleanupSharedComponentData immediately. (UnityUpgradable) -> ICleanupSharedComponentData", true)]
    public interface ISystemStateSharedComponentData : IBufferElementData
    {
    }

    /// <summary>
    /// An interface for a component type which allows the component to be enabled and disabled at runtime without a
    /// structural change.
    /// </summary>
    /// <remarks>
    /// This interface is only valid when used in combination with <see cref="IBufferElementData"/> or <see cref="IComponentData"/>.
    /// While the extra overhead involved in processing enableable components is generally quite low, this interface should only
    /// be used on components that will actually be enabled/disabled by the application at relatively high frequency.
    /// </remarks>
    /// <seealso cref="EntityManager.SetComponentEnabled{T}"/>
    /// <seealso cref="EntityManager.IsComponentEnabled{T}"/>
    /// <seealso cref="ComponentLookup{T}.SetComponentEnabled"/>
    /// <seealso cref="ComponentLookup{T}.IsComponentEnabled"/>
    /// <seealso cref="BufferLookup{T}.SetBufferEnabled"/>
    /// <seealso cref="BufferLookup{T}.IsBufferEnabled"/>
    /// <seealso cref="ArchetypeChunk.SetComponentEnabled{T}(ref ComponentTypeHandle{T},int,bool)"/>
    /// <seealso cref="ArchetypeChunk.IsComponentEnabled{T}(ref ComponentTypeHandle{T},int)"/>
    [RequireImplementors]
    public interface IEnableableComponent
    {
    }

    /// <summary>
    /// Disables the entity.
    /// </summary>
    /// <remarks> By default, an <see cref="EntityQuery"/> ignores all entities that have this component. You
    /// can override this default behavior by setting the `EntityQueryOptions.IncludeDisabledEntities` flag of the
    /// <see cref="EntityQueryDesc"/> object used to create the query.</remarks>
    /// <seealso cref="EntityManager.IsEnabled(Entity)"/>
    /// <seealso cref="EntityManager.SetEnabled(Entity,bool)"/>
    public struct Disabled : IComponentData
    {
    }

    /// <summary>
    /// Marks the entity as a prefab, which implicitly disables the entity.
    /// </summary>
    /// <remarks> By default, an <see cref="EntityQuery"/> ignores all entities that have a Prefab component. You
    /// can override this default behavior by setting the EntityQueryOptions.IncludePrefab flag of the
    /// <see cref="EntityQueryDesc"/> object used to create the query.</remarks>
    public struct Prefab : IComponentData
    {
    }

    /// <summary>
    /// Marks the entity as an asset, which is used for the Export phase of GameObject conversion.
    /// </summary>
    public struct Asset : IComponentData
    {
    }

    /// <summary>
    /// The LinkedEntityGroup buffer makes the entity be the root of a set of connected entities.
    /// </summary>
    /// <remarks>
    /// Referenced Prefabs automatically add a LinkedEntityGroup with the complete child hierarchy.
    /// EntityManager.Instantiate uses LinkedEntityGroup to instantiate the whole set of entities automatically.
    /// EntityManager.SetEnabled uses LinkedEntityGroup to enable the whole set of entities.
    /// </remarks>
    [InternalBufferCapacity(1)]
    public struct LinkedEntityGroup : IBufferElementData
    {
        /// <summary>
        /// A child entity.
        /// </summary>
        public Entity Value;

        /// <summary>
        /// Provides implicit conversion of an <see cref="Entity"/> to a LinkedEntityGroup element.
        /// </summary>
        /// <param name="e">The entity to convert</param>
        /// <returns>A new buffer element.</returns>
        public static implicit operator LinkedEntityGroup(Entity e)
        {
            return new LinkedEntityGroup {Value = e};
        }
    }

    /// <summary>
    /// The presence of this component instructs <see cref="M:EntityManager.Instantiate"/> to not to create the
    /// <see cref="LinkedEntityGroup"/> buffer component for every prefab instance, and the prefab child entities
    /// are still created. This component is not kept on the instantiated entities afterwards. This component is
    /// completely ignored and treated as a regular tag component when LinkedEntityGroup is not present on the
    /// source prefab entity.
    /// </summary>
    /// <remarks>
    /// The use of this component is likely for performance consideration, because creating buffer components is
    /// an expensive process involving heap memory allocations linear to the number of instances being instantiated.
    /// Omitting the LinkedEntityGroup could make it difficult to associate the children entities with their prefab
    /// roots; in such case components with Entity reference like <see cref="Unity.Transforms.Parent"/> could be
    /// useful.
    /// </remarks>
    public struct OmitLinkedEntityGroupFromPrefabInstance : IComponentData
    { }

    /// <summary>
    /// A Unity-defined shared component assigned to all entities in the same subscene.
    /// </summary>
    [Serializable][ChunkSerializable]
    public struct SceneTag : ISharedComponentData, IEquatable<SceneTag>
    {
        /// <summary>
        /// The root entity of the subscene.
        /// </summary>
        public Entity  SceneEntity;

        /// <summary>
        /// A unique hash code for comparison.
        /// </summary>
        /// <returns>The scene entity has code.</returns>
        public override int GetHashCode()
        {
            return SceneEntity.GetHashCode();
        }

        /// <summary>
        /// Two SceneTags are equal if they have the same root subscene entity.
        /// </summary>
        /// <param name="other">The other SceneTag.</param>
        /// <returns>True if both SceneTags refer to the same Subscene. False, otherwise.</returns>
        public bool Equals(SceneTag other)
        {
            return SceneEntity == other.SceneEntity;
        }

        /// <summary>
        /// A string for logging.
        /// </summary>
        /// <returns>A string identifying the root subscene entity.</returns>
        public override string ToString()
        {
            return $"SubSceneTag: {SceneEntity}";
        }
    }

    /// <summary>
    /// Enable simulation of an entity.
    /// </summary>
    /// <remarks> This component is added by default to all entities. Systems which needs to support simulating a
    /// subset of entities matching a specific query - such as prediction systems in netcode - need to include this
    /// component in their queries to make sure entities which are not supposed to be simulated at the moment
    /// are skipped.</remarks>
    public struct Simulate : IComponentData, IEnableableComponent
    {
    }
}
