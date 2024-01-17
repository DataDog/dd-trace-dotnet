﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ControlFlowBuilder
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
    public sealed class ControlFlowBuilder
  {

    #nullable disable
    private readonly ImmutableArray<ControlFlowBuilder.BranchInfo>.Builder _branches;
    private readonly ImmutableArray<int>.Builder _labels;
    private ImmutableArray<ControlFlowBuilder.ExceptionHandlerInfo>.Builder _lazyExceptionHandlers;

    public ControlFlowBuilder()
    {
      this._branches = ImmutableArray.CreateBuilder<ControlFlowBuilder.BranchInfo>();
      this._labels = ImmutableArray.CreateBuilder<int>();
    }

    /// <summary>
    /// Clears the object's internal state, allowing the same instance to be reused.
    /// </summary>
    public void Clear()
    {
      this._branches.Clear();
      this._labels.Clear();
      this._lazyExceptionHandlers?.Clear();
    }

    internal LabelHandle AddLabel()
    {
      this._labels.Add(-1);
      return new LabelHandle(this._labels.Count);
    }

    internal void AddBranch(int ilOffset, LabelHandle label, ILOpCode opCode)
    {
      this.ValidateLabel(label, nameof (label));
      this._branches.Add(new ControlFlowBuilder.BranchInfo(ilOffset, label, opCode));
    }

    internal void MarkLabel(int ilOffset, LabelHandle label)
    {
      this.ValidateLabel(label, nameof (label));
      this._labels[label.Id - 1] = ilOffset;
    }

    private int GetLabelOffsetChecked(LabelHandle label)
    {
      int label1 = this._labels[label.Id - 1];
      if (label1 < 0)
        Throw.InvalidOperation_LabelNotMarked(label.Id);
      return label1;
    }

    private void ValidateLabel(LabelHandle label, string parameterName)
    {
      if (label.IsNil)
        Throw.ArgumentNull(parameterName);
      if (label.Id <= this._labels.Count)
        return;
      Throw.LabelDoesntBelongToBuilder(parameterName);
    }

    /// <summary>Adds finally region.</summary>
    /// <param name="tryStart">Label marking the first instruction of the try block.</param>
    /// <param name="tryEnd">Label marking the instruction immediately following the try block.</param>
    /// <param name="handlerStart">Label marking the first instruction of the handler.</param>
    /// <param name="handlerEnd">Label marking the instruction immediately following the handler.</param>
    /// <exception cref="T:System.ArgumentException">A label was not defined by an instruction encoder this builder is associated with.</exception>
    /// <exception cref="T:System.ArgumentNullException">A label has default value.</exception>
    public void AddFinallyRegion(
      LabelHandle tryStart,
      LabelHandle tryEnd,
      LabelHandle handlerStart,
      LabelHandle handlerEnd)
    {
      this.AddExceptionRegion(ExceptionRegionKind.Finally, tryStart, tryEnd, handlerStart, handlerEnd);
    }

    /// <summary>Adds fault region.</summary>
    /// <param name="tryStart">Label marking the first instruction of the try block.</param>
    /// <param name="tryEnd">Label marking the instruction immediately following the try block.</param>
    /// <param name="handlerStart">Label marking the first instruction of the handler.</param>
    /// <param name="handlerEnd">Label marking the instruction immediately following the handler.</param>
    /// <exception cref="T:System.ArgumentException">A label was not defined by an instruction encoder this builder is associated with.</exception>
    /// <exception cref="T:System.ArgumentNullException">A label has default value.</exception>
    public void AddFaultRegion(
      LabelHandle tryStart,
      LabelHandle tryEnd,
      LabelHandle handlerStart,
      LabelHandle handlerEnd)
    {
      this.AddExceptionRegion(ExceptionRegionKind.Fault, tryStart, tryEnd, handlerStart, handlerEnd);
    }

    /// <summary>Adds catch region.</summary>
    /// <param name="tryStart">Label marking the first instruction of the try block.</param>
    /// <param name="tryEnd">Label marking the instruction immediately following the try block.</param>
    /// <param name="handlerStart">Label marking the first instruction of the handler.</param>
    /// <param name="handlerEnd">Label marking the instruction immediately following the handler.</param>
    /// <param name="catchType">The type of exception to be caught: <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />.</param>
    /// <exception cref="T:System.ArgumentException">A label was not defined by an instruction encoder this builder is associated with.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="catchType" /> is not a valid type handle.</exception>
    /// <exception cref="T:System.ArgumentNullException">A label has default value.</exception>
    public void AddCatchRegion(
      LabelHandle tryStart,
      LabelHandle tryEnd,
      LabelHandle handlerStart,
      LabelHandle handlerEnd,
      EntityHandle catchType)
    {
      if (!ExceptionRegionEncoder.IsValidCatchTypeHandle(catchType))
        Throw.InvalidArgument_Handle(nameof (catchType));
      LabelHandle tryStart1 = tryStart;
      LabelHandle tryEnd1 = tryEnd;
      LabelHandle handlerStart1 = handlerStart;
      LabelHandle handlerEnd1 = handlerEnd;
      EntityHandle entityHandle = catchType;
      LabelHandle filterStart = new LabelHandle();
      EntityHandle catchType1 = entityHandle;
      this.AddExceptionRegion(ExceptionRegionKind.Catch, tryStart1, tryEnd1, handlerStart1, handlerEnd1, filterStart, catchType1);
    }

    /// <summary>Adds catch region.</summary>
    /// <param name="tryStart">Label marking the first instruction of the try block.</param>
    /// <param name="tryEnd">Label marking the instruction immediately following the try block.</param>
    /// <param name="handlerStart">Label marking the first instruction of the handler.</param>
    /// <param name="handlerEnd">Label marking the instruction immediately following the handler.</param>
    /// <param name="filterStart">Label marking the first instruction of the filter block.</param>
    /// <exception cref="T:System.ArgumentException">A label was not defined by an instruction encoder this builder is associated with.</exception>
    /// <exception cref="T:System.ArgumentNullException">A label has default value.</exception>
    public void AddFilterRegion(
      LabelHandle tryStart,
      LabelHandle tryEnd,
      LabelHandle handlerStart,
      LabelHandle handlerEnd,
      LabelHandle filterStart)
    {
      this.ValidateLabel(filterStart, nameof (filterStart));
      this.AddExceptionRegion(ExceptionRegionKind.Filter, tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
    }

    private void AddExceptionRegion(
      ExceptionRegionKind kind,
      LabelHandle tryStart,
      LabelHandle tryEnd,
      LabelHandle handlerStart,
      LabelHandle handlerEnd,
      LabelHandle filterStart = default (LabelHandle),
      EntityHandle catchType = default (EntityHandle))
    {
      this.ValidateLabel(tryStart, nameof (tryStart));
      this.ValidateLabel(tryEnd, nameof (tryEnd));
      this.ValidateLabel(handlerStart, nameof (handlerStart));
      this.ValidateLabel(handlerEnd, nameof (handlerEnd));
      if (this._lazyExceptionHandlers == null)
        this._lazyExceptionHandlers = ImmutableArray.CreateBuilder<ControlFlowBuilder.ExceptionHandlerInfo>();
      this._lazyExceptionHandlers.Add(new ControlFlowBuilder.ExceptionHandlerInfo(kind, tryStart, tryEnd, handlerStart, handlerEnd, filterStart, catchType));
    }


    #nullable enable
    internal IEnumerable<ControlFlowBuilder.BranchInfo> Branches => (IEnumerable<ControlFlowBuilder.BranchInfo>) this._branches;

    internal IEnumerable<int> Labels => (IEnumerable<int>) this._labels;

    internal int BranchCount => this._branches.Count;

    internal int ExceptionHandlerCount
    {
      get
      {
        ImmutableArray<ControlFlowBuilder.ExceptionHandlerInfo>.Builder exceptionHandlers = this._lazyExceptionHandlers;
        return exceptionHandlers == null ? 0 : exceptionHandlers.Count;
      }
    }

    /// <exception cref="T:System.InvalidOperationException" />
    internal void CopyCodeAndFixupBranches(BlobBuilder srcBuilder, BlobBuilder dstBuilder)
    {
      ControlFlowBuilder.BranchInfo branchInfo = this._branches[0];
      int index1 = 0;
      int branchILOffset = 0;
      int start = 0;
      foreach (Blob blob in srcBuilder.GetBlobs())
      {
        int branchOperandSize;
        while (true)
        {
          int byteCount = Math.Min(branchInfo.ILOffset - branchILOffset, blob.Length - start);
          dstBuilder.WriteBytes(blob.Buffer, start, byteCount);
          branchILOffset += byteCount;
          int index2 = start + byteCount;
          if (index2 != blob.Length)
          {
            branchOperandSize = branchInfo.OpCode.GetBranchOperandSize();
            bool isShortBranch = branchOperandSize == 1;
            dstBuilder.WriteByte(blob.Buffer[index2]);
            int branchDistance = branchInfo.GetBranchDistance(this._labels, branchInfo.OpCode, branchILOffset, isShortBranch);
            if (isShortBranch)
              dstBuilder.WriteSByte((sbyte) branchDistance);
            else
              dstBuilder.WriteInt32(branchDistance);
            branchILOffset += 1 + branchOperandSize;
            ++index1;
            branchInfo = index1 != this._branches.Count ? this._branches[index1] : new ControlFlowBuilder.BranchInfo(int.MaxValue, new LabelHandle(), ILOpCode.Nop);
            if (index2 != blob.Length - 1)
              start = index2 + (1 + branchOperandSize);
            else
              goto label_9;
          }
          else
            break;
        }
        start = 0;
        continue;
label_9:
        start = branchOperandSize;
      }
    }

    internal void SerializeExceptionTable(BlobBuilder builder)
    {
      if (this._lazyExceptionHandlers == null || this._lazyExceptionHandlers.Count == 0)
        return;
      ExceptionRegionEncoder exceptionRegionEncoder = ExceptionRegionEncoder.SerializeTableHeader(builder, this._lazyExceptionHandlers.Count, this.HasSmallExceptionRegions());
      foreach (ControlFlowBuilder.ExceptionHandlerInfo exceptionHandler in this._lazyExceptionHandlers)
      {
        int labelOffsetChecked1 = this.GetLabelOffsetChecked(exceptionHandler.TryStart);
        int labelOffsetChecked2 = this.GetLabelOffsetChecked(exceptionHandler.TryEnd);
        int labelOffsetChecked3 = this.GetLabelOffsetChecked(exceptionHandler.HandlerStart);
        int labelOffsetChecked4 = this.GetLabelOffsetChecked(exceptionHandler.HandlerEnd);
        if (labelOffsetChecked1 > labelOffsetChecked2)
          Throw.InvalidOperation(SR.Format(SR.InvalidExceptionRegionBounds, (object) labelOffsetChecked1, (object) labelOffsetChecked2));
        if (labelOffsetChecked3 > labelOffsetChecked4)
          Throw.InvalidOperation(SR.Format(SR.InvalidExceptionRegionBounds, (object) labelOffsetChecked3, (object) labelOffsetChecked4));
        int num;
        switch (exceptionHandler.Kind)
        {
          case ExceptionRegionKind.Catch:
            num = MetadataTokens.GetToken(exceptionHandler.CatchType);
            break;
          case ExceptionRegionKind.Filter:
            num = this.GetLabelOffsetChecked(exceptionHandler.FilterStart);
            break;
          default:
            num = 0;
            break;
        }
        int catchTokenOrOffset = num;
        exceptionRegionEncoder.AddUnchecked(exceptionHandler.Kind, labelOffsetChecked1, labelOffsetChecked2 - labelOffsetChecked1, labelOffsetChecked3, labelOffsetChecked4 - labelOffsetChecked3, catchTokenOrOffset);
      }
    }

    private bool HasSmallExceptionRegions()
    {
      if (!ExceptionRegionEncoder.IsSmallRegionCount(this._lazyExceptionHandlers.Count))
        return false;
      foreach (ControlFlowBuilder.ExceptionHandlerInfo exceptionHandler in this._lazyExceptionHandlers)
      {
        if (!ExceptionRegionEncoder.IsSmallExceptionRegionFromBounds(this.GetLabelOffsetChecked(exceptionHandler.TryStart), this.GetLabelOffsetChecked(exceptionHandler.TryEnd)) || !ExceptionRegionEncoder.IsSmallExceptionRegionFromBounds(this.GetLabelOffsetChecked(exceptionHandler.HandlerStart), this.GetLabelOffsetChecked(exceptionHandler.HandlerEnd)))
          return false;
      }
      return true;
    }

    internal readonly struct BranchInfo
    {
      internal readonly int ILOffset;
      internal readonly LabelHandle Label;
      private readonly byte _opCode;

      internal ILOpCode OpCode => (ILOpCode) this._opCode;

      internal BranchInfo(int ilOffset, LabelHandle label, ILOpCode opCode)
      {
        this.ILOffset = ilOffset;
        this.Label = label;
        this._opCode = (byte) opCode;
      }

      internal int GetBranchDistance(
        ImmutableArray<int>.Builder labels,
        ILOpCode branchOpCode,
        int branchILOffset,
        bool isShortBranch)
      {
        int label = labels[this.Label.Id - 1];
        if (label < 0)
          Throw.InvalidOperation_LabelNotMarked(this.Label.Id);
        int num = 1 + (isShortBranch ? 1 : 4);
        int p3 = label - (this.ILOffset + num);
        return !isShortBranch || (int) (sbyte) p3 == p3 ? p3 : throw new InvalidOperationException(SR.Format(SR.DistanceBetweenInstructionAndLabelTooBig, (object) branchOpCode, (object) branchILOffset, (object) p3));
      }
    }

    internal readonly struct ExceptionHandlerInfo
    {
      public readonly ExceptionRegionKind Kind;
      public readonly LabelHandle TryStart;
      public readonly LabelHandle TryEnd;
      public readonly LabelHandle HandlerStart;
      public readonly LabelHandle HandlerEnd;
      public readonly LabelHandle FilterStart;
      public readonly EntityHandle CatchType;

      public ExceptionHandlerInfo(
        ExceptionRegionKind kind,
        LabelHandle tryStart,
        LabelHandle tryEnd,
        LabelHandle handlerStart,
        LabelHandle handlerEnd,
        LabelHandle filterStart,
        EntityHandle catchType)
      {
        this.Kind = kind;
        this.TryStart = tryStart;
        this.TryEnd = tryEnd;
        this.HandlerStart = handlerStart;
        this.HandlerEnd = handlerEnd;
        this.FilterStart = filterStart;
        this.CatchType = catchType;
      }
    }
  }
}
