using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// Base interface for Aspects, a higher level combination for components.
    /// </summary>
    /// <remarks>
    /// Implement IAspect on a struct with any number of <see cref="RefRW{T}"/> fields.
    /// A <see cref="RefRW{T}"/> field may use these attributes:
    ///     [<see cref="OptionalAttribute"/>]
    ///         Make the component optional. Field <see cref="RefRW{T}.IsValid"/> will be true if the component is present on the current entity.
    ///     [<see cref="Collections.ReadOnlyAttribute"/>]
    ///         Make the component read-only when building an entity query that uses the aspect.
    ///         The field <see cref="RefRW{T}.ValueRW"/> will break the safety checks. Use <see cref="RefRW{T}.ValueRO"/> instead.
    /// </remarks>
    public interface IAspect : IQueryTypeParameter
    {
    }

    /// <summary>
    /// Disable the source generator for an Aspect type, i.e. a struct that implements IAspect
    /// It is up to the user to write the required code to complete all the functionalities needed by an Aspect.
    /// Disabling the source generator should not be used outside of development purposes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class DisableGenerationAttribute : Attribute
    {
    }

    /// <summary>
    /// When used on an aspect's ComponentDataRef field, marks that component type as optional.
    /// The DOTS source generator handles the generation of the query using this attribute.
    /// A ComponentDataRef may also be marked read-only by using the attribute [Unity.Collections.ReadOnly]
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OptionalAttribute : Attribute
    {
    }
}
