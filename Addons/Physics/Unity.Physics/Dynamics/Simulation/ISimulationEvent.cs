using System;
using Unity.Entities;

namespace Unity.Physics
{
    /// <summary>   An event raised when a pair of bodies interact during solving. </summary>
    ///
    /// <typeparam name="T">    Generic type parameter. </typeparam>
    public interface ISimulationEvent<T>: IComparable<T>
    {
        /// <summary>   Gets the entity a. </summary>
        ///
        /// <value> The entity a. </value>
        Entity EntityA { get; }

        /// <summary>   Gets the entity b. </summary>
        ///
        /// <value> The entity b. </value>
        Entity EntityB { get; }

        /// <summary>   Gets the body index a. </summary>
        ///
        /// <value> The body index a. </value>
        int BodyIndexA { get; }

        /// <summary>   Gets the body index b. </summary>
        ///
        /// <value> The body index b. </value>
        int BodyIndexB { get; }

        /// <summary>   Gets the collider key a. </summary>
        ///
        /// <value> The collider key a. </value>
        ColliderKey ColliderKeyA { get; }

        /// <summary>   Gets the collider key b. </summary>
        ///
        /// <value> The collider key b. </value>
        ColliderKey ColliderKeyB { get; }
    }

    /// <summary>   A simulation event utility class. </summary>
    public static class ISimulationEventUtilities
    {
        /// <summary>   Compare events. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="thisEvent">    this event. </param>
        /// <param name="otherEvent">   The other event. </param>
        ///
        /// <returns>
        /// Negative number if this object is less than the other, 0 if they are equal, or positive if this is
        /// greater.
        /// </returns>
        public static int CompareEvents<T>(T thisEvent, T otherEvent) where T : struct, ISimulationEvent<T>
        {
            int i = thisEvent.EntityA.CompareTo(otherEvent.EntityA);
            if (i == 0)
            {
                i = thisEvent.EntityB.CompareTo(otherEvent.EntityB);
                if (i == 0)
                {
                    i = thisEvent.ColliderKeyA.CompareTo(otherEvent.ColliderKeyA);
                    if (i == 0)
                    {
                        i = thisEvent.ColliderKeyB.CompareTo(otherEvent.ColliderKeyB);
                    }
                }
            }

            return i;
        }
    }
}
