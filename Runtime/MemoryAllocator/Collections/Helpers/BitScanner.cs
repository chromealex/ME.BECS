namespace ME.BECS.Collections {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst.Intrinsics;
    using Unity.Mathematics;

    public static unsafe class BitScanner {

        public const int BITS_IN_ULONG = 64;
        
        [INLINE(256)]
        public static UnsafeList<uint> GetTrueBitsTempFast(in TempBitArray arr, Unity.Collections.Allocator allocator) {
            /*if (X86.Sse2.IsSse2Supported == true) {
                return BitScannerSse2.GetTrueBits(arr.ptr.ptr, (int)arr.Length);
            } else if (X86.Avx2.IsAvx2Supported == true) {
                return BitScannerAvx2.GetTrueBits(arr.ptr.ptr, (int)arr.Length);
            } else if (Unity.Burst.Intrinsics.Arm.Neon.IsNeonSupported == true) {
                return BitScannerNeon.GetTrueBits(arr.ptr.ptr, (int)arr.Length);
            }*/
            return arr.Length < FullFillBits.FILL_BITS_COUNT ? GetTrueBitsTempFastFull(arr.ptr.ptr, (int)arr.Length, allocator) : GetTrueBitsTempFast(arr.ptr.ptr, (int)arr.Length, allocator);
        }
        
        [INLINE(256)]
        public static UnsafeList<uint> GetTrueBitsTempFast(ulong* data, int bitLength, Unity.Collections.Allocator allocator) {
            var trueBits = new UnsafeList<uint>(bitLength, allocator);
            var destPtr = trueBits.Ptr;
            var wordCount = (bitLength + BITS_IN_ULONG - 1) / BITS_IN_ULONG;
            var idx = 0;

            var i = 0;
            var limit4 = wordCount & ~3;
            for (; i < limit4; i += 4) {
                ProcessWordInline(data[i + 0], destPtr, ref idx, (uint)((i + 0) * BITS_IN_ULONG));
                ProcessWordInline(data[i + 1], destPtr, ref idx, (uint)((i + 1) * BITS_IN_ULONG));
                ProcessWordInline(data[i + 2], destPtr, ref idx, (uint)((i + 2) * BITS_IN_ULONG));
                ProcessWordInline(data[i + 3], destPtr, ref idx, (uint)((i + 3) * BITS_IN_ULONG));
            }

            for (i = limit4; i < wordCount; ++i) {
                ProcessWordInline(data[i], destPtr, ref idx, (uint)(i * BITS_IN_ULONG));
            }

            trueBits.Length = idx;

            return trueBits;
        }

        [INLINE(256)]
        public static UnsafeList<uint> GetTrueBitsTempFastFull(ulong* data, int bitLength, Unity.Collections.Allocator allocator) {
            var trueBits = new UnsafeList<uint>(bitLength, allocator);
            var destPtr = trueBits.Ptr;
            var wordCount = (bitLength + BITS_IN_ULONG - 1) / BITS_IN_ULONG;
            var idx = 0;

            var i = 0;
            var limit4 = wordCount & ~3;
            for (; i < limit4; i += 4) {
                ProcessWordInlineFull(data[i + 0], destPtr, ref idx, (uint)((i + 0) * BITS_IN_ULONG));
                ProcessWordInlineFull(data[i + 1], destPtr, ref idx, (uint)((i + 1) * BITS_IN_ULONG));
                ProcessWordInlineFull(data[i + 2], destPtr, ref idx, (uint)((i + 2) * BITS_IN_ULONG));
                ProcessWordInlineFull(data[i + 3], destPtr, ref idx, (uint)((i + 3) * BITS_IN_ULONG));
            }

            for (i = limit4; i < wordCount; ++i) {
                ProcessWordInline(data[i], destPtr, ref idx, (uint)(i * BITS_IN_ULONG));
            }

            trueBits.Length = idx;

            return trueBits;
        }

        [INLINE(256)]
        internal static void ProcessWordInline(ulong val, uint* destPtr, ref int idx, uint offset) {
            if (val == 0UL) return;
            if (val == ulong.MaxValue) {
                if (offset + BITS_IN_ULONG <= FullFillBits.FILL_BITS_COUNT) {
                    var src = FullFillBits.fullFillBits.Data.ptr.ptr;
                    UnsafeUtility.MemCpy(destPtr + idx, src + offset, sizeof(uint) * BITS_IN_ULONG);
                    idx += BITS_IN_ULONG;
                    return;
                }
            }
            while (val != 0UL) {
                var bit = math.tzcnt(val);
                destPtr[idx++] = offset + (uint)bit;
                val &= val - 1UL;
            }
        }

        [INLINE(256)]
        internal static void ProcessWordInlineFull(ulong val, uint* destPtr, ref int idx, uint offset) {
            if (val == 0UL) return;
            if (val == ulong.MaxValue) {
                var src = FullFillBits.fullFillBits.Data.ptr.ptr;
                UnsafeUtility.MemCpy(destPtr + idx, src + offset, sizeof(uint) * BITS_IN_ULONG);
                idx += BITS_IN_ULONG;
                return;
            }
            while (val != 0UL) {
                var bit = math.tzcnt(val);
                destPtr[idx++] = offset + (uint)bit;
                val &= val - 1UL;
            }
        }

    }

    /*
    public static unsafe class BitScannerAvx2 {

        [INLINE(256)]
        public static UnsafeList<uint> GetTrueBits(ulong* data, int bitLength) {
            var result = new UnsafeList<uint>(bitLength, Allocator.Temp);
            var wordCount = (bitLength + 63) / 64;
            var i = 0;
            var limit4 = wordCount & ~3;

            for (; i < limit4; i += 4) {
                var vec = X86.Avx.mm256_loadu_si256((v256*)(data + i));
                var zero = X86.Avx.mm256_set1_epi64x(0);
                var cmpZero = X86.Avx2.mm256_cmpeq_epi64(vec, zero);
                var nonZeroMask = X86.Avx2.mm256_movemask_epi8(cmpZero);
                var lanesNonZeroMask = 0;
                for (var lane = 0; lane < 4; ++lane) {
                    var byteMask = (nonZeroMask >> (lane * 8)) & 0xFF;
                    var laneIsAllEqZero = byteMask == 0xFF;
                    if (laneIsAllEqZero == false) {
                        lanesNonZeroMask |= 1 << lane;
                    }
                }

                var ones = X86.Avx.mm256_set1_epi64x(unchecked((long)~0UL));
                var cmpAllOnes = X86.Avx2.mm256_cmpeq_epi64(vec, ones);
                var allOnesByteMask = X86.Avx2.mm256_movemask_epi8(cmpAllOnes);
                var lanesAllOnesMask = 0;
                for (var lane = 0; lane < 4; ++lane) {
                    var byteMask = (allOnesByteMask >> (lane * 8)) & 0xFF;
                    var laneIsAllOnes = byteMask == 0xFF;
                    if (laneIsAllOnes) {
                        lanesAllOnesMask |= 1 << lane;
                    }
                }

                for (var lane = 0; lane < 4; ++lane) {
                    var laneBit = 1 << lane;
                    if ((lanesNonZeroMask & laneBit) == 0) {
                        continue;
                    }

                    var val = data[i + lane];
                    var offset = (uint)((i + lane) * 64);

                    if ((lanesAllOnesMask & laneBit) != 0) {
                        var src = FullFillBits.fullFillBits.Data;
                        if (offset + 64 <= src.Length) {
                            var srcPtr = src.ptr.ptr + offset;
                            var destPtr = result.Ptr + result.Length;
                            UnsafeUtility.MemCpy(destPtr, srcPtr, sizeof(uint) * 64);
                            result.Length += 64;
                        } else {
                            for (uint j = 0; j < 64; ++j) {
                                result.Add(offset + j);
                            }
                        }
                    } else {
                        while (val != 0UL) {
                            var b = math.tzcnt(val);
                            result.Add(offset + (uint)b);
                            val &= val - 1UL;
                        }
                    }
                }
            }

            for (; i < wordCount; ++i) {
                BitScanner.ProcessWordInline(ref result, data[i], (uint)(i * 64));
            }

            return result;
        }

    }

    public static unsafe class BitScannerSse2 {

        private const int BITS_IN_ULONG = 64;

        [INLINE(256)]
        public static UnsafeList<uint> GetTrueBits(ulong* data, int bitLength) {
            var trueBits = new UnsafeList<uint>(bitLength, Allocator.Temp);
            var wordCount = ((bitLength + 63) / 64);

            var i = 0;
            var limit2 = wordCount & ~1;

            for (; i < limit2; i += 2) {
                v128 vec = X86.Sse2.loadu_si128((v128*)(data + i));
                v128 zero = X86.Sse2.setzero_si128();
                v128 cmpZero = X86.Sse2.cmpeq_epi32(vec, zero);
                v128 allOnes = X86.Sse2.set1_epi64x(unchecked((long)~0UL));
                v128 cmpAllOnes = X86.Sse2.cmpeq_epi32(vec, allOnes);

                int maskZero = X86.Sse2.movemask_epi8(cmpZero);
                int maskOnes = X86.Sse2.movemask_epi8(cmpAllOnes);

                for (var lane = 0; lane < 2; ++lane) {
                    var byteMask = (maskZero >> (lane * 8)) & 0xFF;
                    var laneIsZero = byteMask == 0xFF;
                    if (laneIsZero == true) {
                        continue;
                    }

                    var byteMaskOnes = (maskOnes >> (lane * 8)) & 0xFF;
                    var laneIsAllOnes = byteMaskOnes == 0xFF;

                    var val = data[i + lane];
                    var offset = (uint)((i + lane) * 64);

                    if (laneIsAllOnes == true) {
                        var src = FullFillBits.fullFillBits.Data;
                        if (offset + 64 <= src.Length) {
                            var srcPtr = src.ptr.ptr + offset;
                            var destPtr = trueBits.Ptr + trueBits.Length;
                            UnsafeUtility.MemCpy(destPtr, srcPtr, sizeof(uint) * 64);
                            trueBits.Length += 64;
                        } else {
                            for (uint j = 0; j < 64; ++j) {
                                trueBits.Add(offset + j);
                            }
                        }
                    } else {
                        while (val != 0UL) {
                            var bit = math.tzcnt(val);
                            trueBits.Add(offset + (uint)bit);
                            val &= val - 1UL;
                        }
                    }
                }
            }

            for (; i < wordCount; ++i) {
                BitScanner.ProcessWordInline(ref trueBits, data[i], (uint)(i * 64));
            }

            return trueBits;
        }

    }

    public static unsafe class BitScannerNeon {

        private const int BITS_IN_ULONG = 64;

        [INLINE(256)]
        public static UnsafeList<uint> GetTrueBits(ulong* data, int bitLength) {
            var trueBits = new UnsafeList<uint>(bitLength, Allocator.Temp);
            var wordCount = (bitLength + 63) / 64;

            var i = 0;
            var limit2 = wordCount & ~1;

            for (; i < limit2; i += 2) {
                var vals = Arm.Neon.vld1q_u64(data + i);

                var zeroMask = Arm.Neon.vceqq_u64(vals, new v128(0UL));
                var allMask = Arm.Neon.vceqq_u64(vals, new v128(ulong.MaxValue));

                var val0 = ((ulong*)&vals)[0];
                var val1 = ((ulong*)&vals)[1];

                var isZero0 = ((ulong*)&zeroMask)[0];
                var isZero1 = ((ulong*)&zeroMask)[1];

                var isAll1_0 = ((ulong*)&allMask)[0];
                var isAll1_1 = ((ulong*)&allMask)[1];

                ProcessNeonLane(ref trueBits, val0, isZero0, isAll1_0, (uint)((i + 0) * BITS_IN_ULONG));
                ProcessNeonLane(ref trueBits, val1, isZero1, isAll1_1, (uint)((i + 1) * BITS_IN_ULONG));
            }

            for (; i < wordCount; ++i) {
                BitScanner.ProcessWordInline(ref trueBits, data[i], (uint)(i * 64));
            }

            return trueBits;
        }

        [INLINE(256)]
        private static void ProcessNeonLane(ref UnsafeList<uint> dest, ulong val, ulong isZero, ulong isAllOnes, uint offset) {
            if (isZero == ulong.MaxValue) {
                return;
            }

            if (isAllOnes == ulong.MaxValue) {
                var src = FullFillBits.fullFillBits.Data;
                if (offset + 64 <= src.Length) {
                    var srcPtr = src.ptr.ptr + offset;
                    var destPtr = dest.Ptr + dest.Length;
                    UnsafeUtility.MemCpy(destPtr, srcPtr, sizeof(uint) * 64);
                    dest.Length += 64;
                } else {
                    for (uint j = 0; j < 64; ++j) {
                        dest.Add(offset + j);
                    }
                }
            } else {
                while (val != 0UL) {
                    var bit = math.tzcnt(val);
                    dest.Add(offset + (uint)bit);
                    val &= val - 1UL;
                }
            }
        }

    }*/

}