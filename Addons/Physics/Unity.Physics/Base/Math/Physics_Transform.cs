using System;
using System.Diagnostics;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   A mathematics utility class. </summary>
    public static partial class Math
    {
        /// <summary>   A transform in matrix format </summary>
        [DebuggerStepThrough]
        public struct MTransform : IEquatable<MTransform>
        {
            /// <summary>   The rotation. </summary>
            public float3x3 Rotation;
            /// <summary>   The translation. </summary>
            public float3 Translation;

            /// <summary>   Gets the identity. </summary>
            ///
            /// <value> The identity. </value>
            public static MTransform Identity => new MTransform { Rotation = float3x3.identity, Translation = float3.zero };

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="transform">    The transform. </param>
            public MTransform(RigidTransform transform)
            {
                Rotation = new float3x3(transform.rot);
                Translation = transform.pos;
            }

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="rotation">     The rotation. </param>
            /// <param name="translation">  The translation. </param>
            public MTransform(quaternion rotation, float3 translation)
            {
                Rotation = new float3x3(rotation);
                Translation = translation;
            }

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="rotation">     The rotation. </param>
            /// <param name="translation">  The translation. </param>
            public MTransform(float3x3 rotation, float3 translation)
            {
                Rotation = rotation;
                Translation = translation;
            }

            /// <summary>   Gets the inverse rotation. </summary>
            ///
            /// <value> The inverse rotation. </value>
            public float3x3 InverseRotation => math.transpose(Rotation);

            /// <summary>   Tests if this MTransform is considered equal to another. </summary>
            ///
            /// <param name="other">    The m transform to compare to this object. </param>
            ///
            /// <returns>   True if the objects are considered equal, false if they are not. </returns>
            public bool Equals(MTransform other)
            {
                return this.Rotation.Equals(other.Rotation) && this.Translation.Equals(other.Translation);
            }
        }

        /// <summary>    A transform in matrix format that includes scale. </summary>
        [DebuggerStepThrough]
        public struct ScaledMTransform
        {
            /// <summary>   The transform. </summary>
            public MTransform Transform;
            internal float m_Scale;

            /// <summary>   Gets or sets the scale. </summary>
            ///
            /// <value> The scale. </value>
            public float Scale { get => m_Scale; set { UnityEngine.Assertions.Assert.IsTrue(value != 0.0f); m_Scale = value; } }

            /// <summary>   Gets the inverse rotation. </summary>
            ///
            /// <value> The inverse rotation. </value>
            public float3x3 InverseRotation => math.transpose(Transform.Rotation);

            /// <summary>   Gets or sets the rotation. </summary>
            ///
            /// <value> The rotation. </value>
            public float3x3 Rotation { get => Transform.Rotation; set => Transform.Rotation = value; }

            /// <summary>   Gets or sets the translation. </summary>
            ///
            /// <value> The translation. </value>
            public float3 Translation { get => Transform.Translation; set => Transform.Translation = value; }

            /// <summary>   Gets the identity. </summary>
            ///
            /// <value> The identity. </value>
            public static ScaledMTransform Identity => new ScaledMTransform { Transform = MTransform.Identity, Scale = 1.0f };

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="transform">    The transform. </param>
            /// <param name="uniformScale"> The uniform scale. </param>
            public ScaledMTransform(RigidTransform transform, float uniformScale)
            {
                Transform = new MTransform(transform);
                UnityEngine.Assertions.Assert.IsTrue(uniformScale != 0.0f);

                m_Scale = uniformScale;
            }

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="transform">    The transform. </param>
            /// <param name="uniformScale"> The uniform scale. </param>
            public ScaledMTransform(MTransform transform, float uniformScale)
            {
                Transform = transform;
                UnityEngine.Assertions.Assert.IsTrue(uniformScale != 0.0f);

                m_Scale = uniformScale;
            }

            /// <summary>
            /// Returns aFromC = aFromB * bFromC, where bFromC has no scale Advanced use only.
            /// </summary>
            ///
            /// <param name="aFromB">   aFromB. </param>
            /// <param name="bFromC">   bFromC. </param>
            ///
            /// <returns>   A ScaledMTransform, aFromC. </returns>
            public static ScaledMTransform Mul(ScaledMTransform aFromB, MTransform bFromC)
            {
                return new ScaledMTransform
                {
                    Rotation = math.mul(aFromB.Rotation, bFromC.Rotation),
                    Translation = math.mul(aFromB.Rotation, aFromB.Scale * bFromC.Translation) + aFromB.Translation,
                    Scale = aFromB.Scale
                };
            }
        }

        /// <summary>   Returns cFromA = cFromB * bFromA. </summary>
        ///
        /// <param name="cFromB">   cFromB. </param>
        /// <param name="bFromA">   bFromA. </param>
        ///
        /// <returns>   A MTransform, cFromA. </returns>
        public static MTransform Mul(MTransform cFromB, MTransform bFromA)
        {
            return new MTransform
            {
                Rotation = math.mul(cFromB.Rotation, bFromA.Rotation),
                Translation = math.mul(cFromB.Rotation, bFromA.Translation) + cFromB.Translation
            };
        }

        /// <summary>   Inverses the given transform. </summary>
        ///
        /// <param name="transform">    A MTransform to process. </param>
        ///
        /// <returns>   An inverse of the provided transform. </returns>
        public static MTransform Inverse(MTransform transform)
        {
            float3x3 inverseRotation = math.transpose(transform.Rotation);
            return new MTransform
            {
                Rotation = inverseRotation,
                Translation = math.mul(inverseRotation, -transform.Translation)
            };
        }

        /// <summary>   Multiplies the point by the transform. </summary>
        ///
        /// <param name="a">    A MTransform to multiply with. </param>
        /// <param name="x">    A point to process. </param>
        ///
        /// <returns>   A transformed point. </returns>
        public static float3 Mul(MTransform a, float3 x)
        {
            return math.mul(a.Rotation, x) + a.Translation;
        }

        /// <summary>   Returns cFromA = cFromB * bFromA. </summary>
        ///
        /// <param name="cFromB">   cFromB. </param>
        /// <param name="bFromA">   bFromA. </param>
        ///
        /// <returns>   A ScaledMTransform, cFromA. </returns>
        public static ScaledMTransform Mul(ScaledMTransform cFromB, ScaledMTransform bFromA)
        {
            return new ScaledMTransform
            {
                Transform = new MTransform
                {
                    Rotation = math.mul(cFromB.Rotation, bFromA.Rotation),
                    Translation = math.mul(cFromB.Rotation, cFromB.Scale * bFromA.Translation) + cFromB.Translation
                },
                Scale = cFromB.Scale * bFromA.Scale
            };
        }

        /// <summary>   Inverses the given transform. </summary>
        ///
        /// <param name="transform">    A ScaledMTransform to process. </param>
        ///
        /// <returns>   A ScaledMTransform that is the inverse of the input. </returns>
        public static ScaledMTransform Inverse(ScaledMTransform transform)
        {
            var invRotation = transform.InverseRotation;
            var invScale = 1.0f / transform.Scale;
            return new ScaledMTransform
            {
                Transform = new MTransform
                {
                    Rotation = invRotation,
                    Translation = math.mul(invRotation, -transform.Translation * invScale)
                },
                Scale = invScale
            };
        }

        /// <summary>   Multiplies the point by the transform. </summary>
        ///
        /// <param name="a">    A ScaledMTransform to multiply with. </param>
        /// <param name="x">    A point to process. </param>
        ///
        /// <returns>   A transformed point. </returns>
        public static float3 Mul(ScaledMTransform a, float3 x)
        {
            // Only possible due to scale being uniform
            // In other cases, should apply scale to rotation matrix and then mul
            return math.mul(a.Rotation, a.Scale * x) + a.Translation;
        }
    }
}
