using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   An impulse event. </summary>
    public struct ImpulseEvent
    {
        internal ImpulseEventData EventData;

        /// <summary>   Gets the body index a. </summary>
        ///
        /// <value> The body index a. </value>
        public int BodyIndexA => EventData.BodyIndices.BodyIndexA;

        /// <summary>   Gets the body index b. </summary>
        ///
        /// <value> The body index b. </value>
        public int BodyIndexB => EventData.BodyIndices.BodyIndexB;

        /// <summary>   Gets the impulse. </summary>
        ///
        /// <value> The impulse. </value>
        public float3 Impulse => EventData.Impulse;

        /// <summary>   Gets the <see cref="ConstraintType"/>. </summary>
        ///
        /// <value> The constraint type. </value>
        public ConstraintType Type => EventData.Type;

        /// <summary>   Gets the joint entity. </summary>
        ///
        /// <value> The joint entity. </value>
        public Entity JointEntity => EventData.JointEntity;
    }
    /// <summary>
    /// A stream of impulse events. This is a value type, which means it can be used in Burst jobs
    /// (unlike IEnumerable&lt;ImpulseEvent&gt;).
    /// </summary>
    public struct ImpulseEvents
    {
        //@TODO: Unity should have a Allow null safety restriction
        [NativeDisableContainerSafetyRestriction]
        private readonly NativeStream m_EventDataStream;

        internal ImpulseEvents(NativeStream eventDataStream)
        {
            m_EventDataStream = eventDataStream;
        }

        /// <summary>   Gets the enumerator. </summary>
        ///
        /// <returns>   The enumerator. </returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(m_EventDataStream);
        }

        /// <summary>   An enumerator. </summary>
        public struct Enumerator
        {
            private NativeStream.Reader m_Reader;
            private int m_CurrentWorkItem;
            private readonly int m_NumWorkItems;

            /// <summary>   Gets or sets the current. </summary>
            ///
            /// <value> The current. </value>
            public ImpulseEvent Current { get; private set; }

            internal Enumerator(NativeStream stream)
            {
                m_Reader = stream.IsCreated ? stream.AsReader() : new NativeStream.Reader();
                m_CurrentWorkItem = 0;
                m_NumWorkItems = stream.IsCreated ? stream.ForEachCount : 0;
                Current = default;

                AdvanceReader();
            }

            /// <summary>   Determines if we can move next. </summary>
            ///
            /// <returns>   True if we can, false otherwise. </returns>
            public bool MoveNext()
            {
                if (m_Reader.RemainingItemCount > 0)
                {
                    var eventData = m_Reader.Read<ImpulseEventData>();

                    Current = eventData.CreateImpulseEvent();

                    AdvanceReader();
                    return true;
                }
                return false;
            }

            private void AdvanceReader()
            {
                while (m_Reader.RemainingItemCount == 0 && m_CurrentWorkItem < m_NumWorkItems)
                {
                    m_Reader.BeginForEachIndex(m_CurrentWorkItem);
                    m_CurrentWorkItem++;
                }
            }
        }
    }

    struct ImpulseEventData
    {
        public float3 Impulse;
        public BodyIndexPair BodyIndices;
        public ConstraintType Type;
        public Entity JointEntity;

        internal ImpulseEvent CreateImpulseEvent()
        {
            return new ImpulseEvent
            {
                EventData = this
            };
        }
    }

    struct ImpulseEventSolverData
    {
        public float3 AccumulatedImpulse;
        public float3 MaxImpulse;
        public Entity JointEntity;
    }
}
