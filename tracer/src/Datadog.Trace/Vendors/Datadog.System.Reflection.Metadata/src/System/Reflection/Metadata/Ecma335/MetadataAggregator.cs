﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataAggregator
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    public sealed class MetadataAggregator
    {

#nullable disable
        private readonly ImmutableArray<ImmutableArray<int>> _heapSizes;
        private readonly ImmutableArray<ImmutableArray<MetadataAggregator.RowCounts>> _rowCounts;


#nullable enable
        public MetadataAggregator(MetadataReader baseReader, IReadOnlyList<MetadataReader> deltaReaders)
          : this(baseReader, (IReadOnlyList<int>)null, (IReadOnlyList<int>)null, deltaReaders)
        {
        }

        public MetadataAggregator(
          IReadOnlyList<int>? baseTableRowCounts,
          IReadOnlyList<int>? baseHeapSizes,
          IReadOnlyList<MetadataReader>? deltaReaders)
          : this((MetadataReader)null, baseTableRowCounts, baseHeapSizes, deltaReaders)
        {
        }


#nullable disable
        private MetadataAggregator(
          MetadataReader baseReader,
          IReadOnlyList<int> baseTableRowCounts,
          IReadOnlyList<int> baseHeapSizes,
          IReadOnlyList<MetadataReader> deltaReaders)
        {
            if (baseTableRowCounts == null)
            {
                if (baseReader == null)
                    Throw.ArgumentNull(nameof(baseReader));
                if (baseReader.GetTableRowCount(TableIndex.EncMap) != 0)
          throw new ArgumentException(SR.BaseReaderMustBeFullMetadataReader, nameof (baseReader));
                MetadataAggregator.CalculateBaseCounts(baseReader, out baseTableRowCounts, out baseHeapSizes);
            }
            else
            {
                if (baseTableRowCounts.Count != MetadataTokens.TableCount)
          throw new ArgumentException(SR.Format(SR.ExpectedListOfSize, (object) MetadataTokens.TableCount), nameof (baseTableRowCounts));
                if (baseHeapSizes == null)
                    Throw.ArgumentNull(nameof(baseHeapSizes));
                if (baseHeapSizes.Count != MetadataTokens.HeapCount)
          throw new ArgumentException(SR.Format(SR.ExpectedListOfSize, (object) MetadataTokens.HeapCount), nameof (baseTableRowCounts));
            }
            if (deltaReaders == null || deltaReaders.Count == 0)
        throw new ArgumentException(SR.ExpectedNonEmptyList, nameof (deltaReaders));
            for (int index = 0; index < deltaReaders.Count; ++index)
            {
                if (deltaReaders[index].GetTableRowCount(TableIndex.EncMap) == 0 || !deltaReaders[index].IsMinimalDelta)
          throw new ArgumentException(SR.ReadersMustBeDeltaReaders, nameof (deltaReaders));
            }
            this._heapSizes = MetadataAggregator.CalculateHeapSizes(baseHeapSizes, deltaReaders);
            this._rowCounts = MetadataAggregator.CalculateRowCounts(baseTableRowCounts, deltaReaders);
        }


#nullable enable
        internal MetadataAggregator(MetadataAggregator.RowCounts[][] rowCounts, int[][] heapSizes)
        {
            this._rowCounts = MetadataAggregator.ToImmutable<MetadataAggregator.RowCounts>(rowCounts);
            this._heapSizes = MetadataAggregator.ToImmutable<int>(heapSizes);
        }


#nullable disable
        private static void CalculateBaseCounts(
          MetadataReader baseReader,
          out IReadOnlyList<int> baseTableRowCounts,
          out IReadOnlyList<int> baseHeapSizes)
        {
            int[] numArray1 = new int[MetadataTokens.TableCount];
            int[] numArray2 = new int[MetadataTokens.HeapCount];
            for (int index = 0; index < numArray1.Length; ++index)
                numArray1[index] = baseReader.GetTableRowCount((TableIndex)index);
            for (int index = 0; index < numArray2.Length; ++index)
                numArray2[index] = baseReader.GetHeapSize((HeapIndex)index);
            baseTableRowCounts = (IReadOnlyList<int>)numArray1;
            baseHeapSizes = (IReadOnlyList<int>)numArray2;
        }

        private static ImmutableArray<ImmutableArray<int>> CalculateHeapSizes(
          IReadOnlyList<int> baseSizes,
          IReadOnlyList<MetadataReader> deltaReaders)
        {
            int length = 1 + deltaReaders.Count;
            int[] items1 = new int[length];
            int[] items2 = new int[length];
            int[] items3 = new int[length];
            int[] items4 = new int[length];
            items1[0] = baseSizes[0];
            items2[0] = baseSizes[1];
            items3[0] = baseSizes[2];
            items4[0] = baseSizes[3] / 16;
            for (int index = 0; index < deltaReaders.Count; ++index)
            {
                items1[index + 1] = items1[index] + deltaReaders[index].GetHeapSize(HeapIndex.UserString);
                items2[index + 1] = items2[index] + deltaReaders[index].GetHeapSize(HeapIndex.String);
                items3[index + 1] = items3[index] + deltaReaders[index].GetHeapSize(HeapIndex.Blob);
                items4[index + 1] = items4[index] + deltaReaders[index].GetHeapSize(HeapIndex.Guid) / 16;
            }
            return ImmutableArray.Create<ImmutableArray<int>>(((IEnumerable<int>)items1).ToImmutableArray<int>(), ((IEnumerable<int>)items2).ToImmutableArray<int>(), ((IEnumerable<int>)items3).ToImmutableArray<int>(), ((IEnumerable<int>)items4).ToImmutableArray<int>());
        }

        private static ImmutableArray<ImmutableArray<MetadataAggregator.RowCounts>> CalculateRowCounts(
          IReadOnlyList<int> baseRowCounts,
          IReadOnlyList<MetadataReader> deltaReaders)
        {
            MetadataAggregator.RowCounts[][] baseRowCounts1 = MetadataAggregator.GetBaseRowCounts(baseRowCounts, 1 + deltaReaders.Count);
            for (int generation = 1; generation <= deltaReaders.Count; ++generation)
                MetadataAggregator.CalculateDeltaRowCountsForGeneration(baseRowCounts1, generation, ref deltaReaders[generation - 1].EncMapTable);
            return MetadataAggregator.ToImmutable<MetadataAggregator.RowCounts>(baseRowCounts1);
        }

        private static ImmutableArray<ImmutableArray<T>> ToImmutable<T>(T[][] array)
        {
            ImmutableArray<T>[] items = new ImmutableArray<T>[array.Length];
            for (int index = 0; index < array.Length; ++index)
                items[index] = ((IEnumerable<T>)array[index]).ToImmutableArray<T>();
            return ((IEnumerable<ImmutableArray<T>>)items).ToImmutableArray<ImmutableArray<T>>();
        }


#nullable enable
        internal static MetadataAggregator.RowCounts[][] GetBaseRowCounts(
          IReadOnlyList<int> baseRowCounts,
          int generations)
        {
            MetadataAggregator.RowCounts[][] baseRowCounts1 = new MetadataAggregator.RowCounts[MetadataTokens.TableCount][];
            for (int index = 0; index < baseRowCounts1.Length; ++index)
            {
                baseRowCounts1[index] = new MetadataAggregator.RowCounts[generations];
                baseRowCounts1[index][0].AggregateInserts = baseRowCounts[index];
            }
            return baseRowCounts1;
        }

        internal static void CalculateDeltaRowCountsForGeneration(
          MetadataAggregator.RowCounts[][] rowCounts,
          int generation,
          ref EnCMapTableReader encMapTable)
        {
            foreach (MetadataAggregator.RowCounts[] rowCount in rowCounts)
                rowCount[generation].AggregateInserts = rowCount[generation - 1].AggregateInserts;
            int numberOfRows = encMapTable.NumberOfRows;
            for (int rowId = 1; rowId <= numberOfRows; ++rowId)
            {
                uint token = encMapTable.GetToken(rowId);
                int num = (int)token & 16777215;
                MetadataAggregator.RowCounts[] rowCount = rowCounts[(int)(token >> 24)];
                if (num > rowCount[generation].AggregateInserts)
                {
                    if (num != rowCount[generation].AggregateInserts + 1)
            throw new BadImageFormatException(SR.EnCMapNotSorted);
                    rowCount[generation].AggregateInserts = num;
                }
                else
                    ++rowCount[generation].Updates;
            }
        }

        /// <summary>
        /// Given a handle of an entity in an aggregate metadata calculates
        /// a handle of the entity within the metadata generation it is defined in.
        /// </summary>
        /// <param name="handle">Handle of an entity in an aggregate metadata.</param>
        /// <param name="generation">The generation the entity is defined in.</param>
        /// <returns>Handle of the entity within the metadata generation <paramref name="generation" />.</returns>
        public Handle GetGenerationHandle(Handle handle, out int generation)
        {
            if (handle.IsVirtual)
                throw new NotSupportedException();
            if (handle.IsHeapHandle)
            {
                int offset = handle.Offset;
                HeapIndex index;
                MetadataTokens.TryGetHeapIndex(handle.Kind, out index);
                ImmutableArray<int> heapSiz = this._heapSizes[(int)index];
                int num1 = handle.Type == 114U ? offset - 1 : offset;
                generation = heapSiz.BinarySearch<int>(num1);
                if (generation >= 0)
                {
                    do
                    {
                        ++generation;
                    }
                    while (generation < heapSiz.Length && heapSiz[generation] == num1);
                }
                else
                    generation = ~generation;
                if (generation >= heapSiz.Length)
          throw new ArgumentException(SR.HandleBelongsToFutureGeneration, nameof (handle));
                int num2 = handle.Type == 114U || generation == 0 ? offset : offset - heapSiz[generation - 1];
                return new Handle((byte)handle.Type, num2);
            }
            int rowId = handle.RowId;
            ImmutableArray<MetadataAggregator.RowCounts> rowCount = this._rowCounts[(int)handle.Type];
            generation = rowCount.BinarySearch<MetadataAggregator.RowCounts>(new MetadataAggregator.RowCounts()
            {
                AggregateInserts = rowId
            });
            if (generation >= 0)
            {
                while (generation > 0 && rowCount[generation - 1].AggregateInserts == rowId)
                    --generation;
            }
            else
            {
                generation = ~generation;
                if (generation >= rowCount.Length)
          throw new ArgumentException(SR.HandleBelongsToFutureGeneration, nameof (handle));
            }
            int num = generation == 0 ? rowId : rowId - rowCount[generation - 1].AggregateInserts + rowCount[generation].Updates;
            return new Handle((byte)handle.Type, num);
        }

        internal struct RowCounts : IComparable<MetadataAggregator.RowCounts>
        {
            public int AggregateInserts;
            public int Updates;

            public int CompareTo(MetadataAggregator.RowCounts other) => this.AggregateInserts - other.AggregateInserts;

            public override string ToString() => string.Format("+0x{0:x} ~0x{1:x}", (object)this.AggregateInserts, (object)this.Updates);
        }
    }
}
