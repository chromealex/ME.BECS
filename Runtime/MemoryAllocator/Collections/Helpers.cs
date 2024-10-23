namespace ME.BECS {

    using System.Runtime.CompilerServices;
    
    public interface IIsCreated {

        bool IsCreated { get; }

    }

    public interface IUnmanagedList : IIsCreated {

        object[] ToManagedArray();
        Ent Ent { get; }
        uint GetConfigId();

    }

    public interface IMemArray { }

    public interface IMemList { }

    public enum InsertionBehavior {

        None = 0,
        OverwriteExisting,
        ThrowOnExisting,

    }

    public static class Helpers {

        public static uint NextPot(uint n) {

            --n;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            ++n;
            return n;

        }

    }
    
    internal static class HashHelpers {

        internal static readonly uint[] capacitySizes = {
            3,
            15,
            63,
            255,
            1_023,
            4_095,
            16_383,
            65_535,
            131_071,
            262_143,
            524_287,
            1_048_575,
            2_097_151,
            4_194_303,
            8_388_607,
            16_777_215,
            33_554_431,
            67_108_863,
            134_217_727,
            268_435_455,
            536_870_912,
            1_073_741_823
        };
            
        internal static readonly uint[] smallCapacitySizes = {
            3,
            7,
            15,
            31,
            63,
            127,
            255,
            511,
            1_023,
            2_047,
            4_095,
            16_383,
            65_535,
            131_071,
            262_143,
            524_287,
            1_048_575,
            2_097_151,
            4_194_303,
            8_388_607,
            16_777_215,
            33_554_431,
            67_108_863,
            134_217_727,
            268_435_455,
            536_870_912,
            1_073_741_823
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExpandCapacity(uint oldSize) {
            var min = oldSize - 1 << 1;
            return min > 2_146_435_069U && 2_146_435_069 > oldSize ? 2_146_435_069 : GetCapacity(min);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExpandCapacitySmall(uint oldSize) {
            var min = oldSize - 1 << 1;
            return min > 2_146_435_069U && 2_146_435_069 > oldSize ? 2_146_435_069 : GetCapacitySmall(min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetCapacity(uint min) {
            for (int index = 0, length = capacitySizes.Length; index < length; ++index) {
                var prime = capacitySizes[index];
                if (prime >= min) {
                    return prime;
                }
            }

            throw new System.Exception("Capacity is too big");
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetCapacitySmall(uint min) {
            for (int index = 0, length = smallCapacitySizes.Length; index < length; ++index) {
                var prime = smallCapacitySizes[index];
                if (prime >= min) {
                    return prime;
                }
            }

            throw new System.Exception("Capacity is too big");
        }
        
        #if FEATURE_RANDOMIZED_STRING_HASHING
        public const int HashCollisionThreshold = 100;
        public static bool s_UseRandomizedStringHashing = String.UseRandomizedHashing();
        #endif

        internal const System.Int32 HashPrime = 101;
        // Table of prime numbers to use as hash table sizes. 
        // A typical resize algorithm would pick the smallest prime number in this array
        // that is larger than twice the previous capacity. 
        // Suppose our Hashtable currently has capacity x and enough elements are added 
        // such that a resize needs to occur. Resizing first computes 2x then finds the 
        // first prime in the table greater than 2x, i.e. if primes are ordered 
        // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n. 
        // Doubling is important for preserving the asymptotic complexity of the 
        // hashtable operations such as add.  Having a prime guarantees that double 
        // hashing does not lead to infinite loops.  IE, your hash function will be 
        // h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
        public static readonly uint[] primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPrime(uint candidate) {
            if ((candidate & 1) != 0) {
                int limit = (int)System.Math.Sqrt(candidate);
                for (int divisor = 3; divisor <= limit; divisor += 2) {
                    if ((candidate % divisor) == 0) return false;
                }

                return true;
            }

            return (candidate == 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetPrime(uint min) {
            
            for (int i = 0; i < HashHelpers.primes.Length; i++) {
                var prime = HashHelpers.primes[i];
                if (prime >= min) return prime;
            }

            //outside of our predefined table. 
            //compute the hard way. 
            for (uint i = (min | 1); i < int.MaxValue; i += 2) {
                if (HashHelpers.IsPrime(i) && ((i - 1) % HashHelpers.HashPrime != 0)) return i;
            }

            return min;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetMinPrime() {
            return HashHelpers.primes[0];
        }

        // Returns size of hashtable to grow to.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExpandPrime(uint oldSize) {
            uint newSize = 2u * oldSize;

            // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (TKey) cast
            if ((uint)newSize > HashHelpers.MaxPrimeArrayLength && HashHelpers.MaxPrimeArrayLength > oldSize) {
                //System.Diagnostics.Contracts.Contract.Assert(HashHelpers.MaxPrimeArrayLength == HashHelpers.GetPrime(HashHelpers.MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
                return HashHelpers.MaxPrimeArrayLength;
            }

            return HashHelpers.GetPrime(newSize);
        }


        // This is the maximum prime smaller than Array.MaxArrayLength
        public const uint MaxPrimeArrayLength = 0x7FEFFFFD;

        #if FEATURE_RANDOMIZED_STRING_HASHING
        public static bool IsWellKnownEqualityComparer(object comparer) {
            return (comparer == null || comparer == System.Collections.Generic.EqualityComparer<string>.Default || comparer is IWellKnownStringEqualityComparer); 
        }

        public static IEqualityComparer GetRandomizedEqualityComparer(object comparer) {
            Contract.Assert(comparer == null || comparer == System.Collections.Generic.EqualityComparer<string>.Default || comparer is IWellKnownStringEqualityComparer); 

            if(comparer == null) {
                return new System.Collections.Generic.RandomizedObjectEqualityComparer();
            } 

            if(comparer == System.Collections.Generic.EqualityComparer<string>.Default) {
                return new System.Collections.Generic.RandomizedStringEqualityComparer();
            }

            IWellKnownStringEqualityComparer cmp = comparer as IWellKnownStringEqualityComparer;

            if(cmp != null) {
                return cmp.GetRandomizedEqualityComparer();
            }

            Contract.Assert(false, "Missing case in GetRandomizedEqualityComparer!");

            return null;
        }
        #endif
    }

}