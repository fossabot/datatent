﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Datatent.Core.Algo.Sort
{
    /// <summary>
    /// TODO: source
    /// </summary>
   public static unsafe partial class RadixLsdUnsafe
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyAscending(uint* offsets, uint* keys, uint* workspace,
        int start, int end, int shift, uint groupMask)
        {
            uint* ptr = keys + start;
            int count = end - start;

            while (count-- != 0)
            {
                var j = offsets[(int)((*ptr >> shift) & groupMask)]++;
                workspace[(int)j] = *ptr++;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyDescending(uint* offsets, uint* keys, uint* workspace,
            int start, int end, int shift, uint groupMask)
        {
            uint* ptr = keys + start;
            int count = end - start;
            while (count-- != 0)
            {
                var j = offsets[(int)((~*ptr >> shift) & groupMask)]++;
                workspace[(int)j] = *ptr++;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BucketCountAscending(uint* buckets, uint* keys, int start, int end, int shift, uint groupMask, int bucketCount)
        {
            int count = end - start;
            var localBuckets = stackalloc uint[bucketCount]; // write to stack to avoid write collisions; improves perf

            var ptr = keys + start;
            while (count-- != 0)
            {
                localBuckets[(int)((*ptr++ >> shift) & groupMask)]++;
            }
            while (bucketCount-- != 0)
                *buckets++ = *localBuckets++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BucketCountDescending(uint* buckets, uint* keys, int start, int end, int shift, uint groupMask, int bucketCount)
        {
            int count = end - start;
            var localBuckets = stackalloc uint[bucketCount]; // write to stack to avoid write collisions; improves perf
            // Unsafe.InitBlock(buckets, 0, (uint)bucketCount << 2);
            uint* ptr = keys + start;
            while (count-- != 0)
            {
                localBuckets[(int)((~*ptr++ >> shift) & groupMask)]++;
                // buckets[(int)((~*ptr++ >> shift) & groupMask)]++;
            }
            while (bucketCount-- != 0)
                *buckets++ = *localBuckets++;
        }
        public static void Sort<T>(this Span<T> keys, Span<T> workspace, int r = default, bool descending = false) where T : struct
        {
            if (keys.Length <= 1) return;
            workspace = workspace.Slice(0, keys.Length);

            if (Unsafe.SizeOf<T>() == 4)
            {
                fixed (uint* k = &MemoryMarshal.GetReference(MemoryMarshal.Cast<T, uint>(keys)))
                fixed (uint* w = &MemoryMarshal.GetReference(MemoryMarshal.Cast<T, uint>(workspace)))
                {
                    Sort32(k, w, keys.Length,
                        r, uint.MaxValue, !descending, NumberSystem<T>.Value);
                }
            }
            else
            {
                throw new NotSupportedException($"Sort type '{typeof(T).Name}' is {Unsafe.SizeOf<T>()} bytes, which is not supported");
            }
        }
        public static void Sort(uint* keys, uint* workspace, int length, int r = default, bool descending = false, uint mask = uint.MaxValue)
            => Sort32(keys, workspace, length, r, mask, !descending, NumberSystem.Unsigned);

        public static void Sort(this Span<uint> keys, Span<uint> workspace, int r = default, bool descending = false, uint mask = uint.MaxValue)
        {
            if (keys.Length <= 1) return;
            workspace = workspace.Slice(0, keys.Length);

            fixed (uint* k = &MemoryMarshal.GetReference(keys))
            fixed (uint* w = &MemoryMarshal.GetReference(workspace))
            {
                Sort32(k, w, keys.Length, r, mask, !descending, NumberSystem.Unsigned);
            }
        }


        static void Swap(ref uint* x, ref uint* y, ref bool reversed)
        {
            var tmp = x;
            x = y;
            y = tmp;
            reversed = !reversed;
        }


        static int GroupCount<T>(int r)
        {
            int bits = Unsafe.SizeOf<T>() << 3;
            return ((bits - 1) / r) + 1;
        }

        public static int DefaultR { get; set; }

        private static void Sort32(uint* keys, uint* workspace, int len, int r, uint keyMask, bool ascending, NumberSystem numberSystem)
        {
            if (len <= 1 || keyMask == 0) return;
            r = SortUtils.ChooseBitCount<uint>(r, DefaultR);
            
            int countLength = 1 << r;
            int groups = GroupCount<uint>(r);
            uint* countsOffsets = stackalloc uint[countLength];
            uint mask = (uint)(countLength - 1);

            bool reversed = false;
            if (SortCore32(keys, workspace, r, keyMask, countLength, len, countsOffsets, groups, mask, ascending, numberSystem != NumberSystem.Unsigned))
            {
                Swap(ref keys, ref workspace, ref reversed);
            }

            if (reversed)
            {
                Unsafe.CopyBlock(workspace, keys, (uint)len << 2);
            }
        }

        private static bool SortCore32(uint* keys, uint* workspace, int r, uint keyMask, int countLength, int len, uint* countsOffsets, int groups, uint mask, bool ascending, bool isSigned)
        {
            int invertC = isSigned ? groups - 1 : -1;
            bool reversed = false;
            for (int c = 0, shift = 0; c < groups; c++, shift += r)
            {
                uint groupMask = (keyMask >> shift) & mask;
                keyMask &= ~(mask << shift); // remove those bits from the keyMask to allow fast exit
                if (groupMask == 0)
                {
                    if (keyMask == 0) break;
                    else continue;
                }

                if (ascending)
                    BucketCountAscending(countsOffsets, keys, 0, len, shift, groupMask, countLength);
                else
                    BucketCountDescending(countsOffsets, keys, 0, len, shift, groupMask, countLength);

                if (!ComputeOffsets(countsOffsets, countLength, len, c == invertC ? GetInvertStartIndex(32, r) : 0)) continue; // all in one group

                if (ascending)
                    ApplyAscending(countsOffsets, keys, workspace, 0, len, shift, groupMask);
                else
                    ApplyDescending(countsOffsets, keys, workspace, 0, len, shift, groupMask);

                Swap(ref keys, ref workspace, ref reversed);
            }
            return reversed;
        }

        private static int GetInvertStartIndex(int width, int r)
        {
            // e.g. if width 32 and r 2, then: all bits useful, 4 groups, invert at 2
            // if width 32 and r 3, then: 2 bits useful in final chunk, 8 groups, invert at 2
            var mod = width % r;
            return mod == 0 ? 1 << (r - 1) : 1 << (mod - 1);
        }

        private static void InvertSignedOffsets(uint* offsets, int shift)
        {
            throw new NotImplementedException();
        }

        static bool ComputeOffsets(uint* countsOffsets, int bucketCount, int length, int bucketOffset)
        {
            uint offset = 0;
            if(bucketOffset != 0)
            {
                int count = bucketCount - bucketOffset;
                bucketCount -= count;
                var ptr = countsOffsets + bucketOffset;
                while(count-- != 0)
                {
                    var grpCount = *ptr;
                    if (grpCount == length) return false;

                    *ptr++ = offset;
                    offset += grpCount;
                }
            }

            while (bucketCount-- != 0)
            {
                var grpCount = *countsOffsets;
                if (grpCount == length) return false;

                *countsOffsets++ = offset;
                offset += grpCount;                
            }
            return true;
        }
    }
}
