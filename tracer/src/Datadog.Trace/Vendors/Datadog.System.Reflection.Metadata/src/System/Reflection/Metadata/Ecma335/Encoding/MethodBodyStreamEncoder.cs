﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodBodyStreamEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>Encodes method body stream.</summary>
  public readonly struct MethodBodyStreamEncoder
  {
    public BlobBuilder Builder { get; }

    public MethodBodyStreamEncoder(BlobBuilder builder)
    {
      if (builder == null)
        Throw.BuilderArgumentNull();
      this.Builder = builder.Count % 4 == 0 ? builder : throw new ArgumentException(SR.BuilderMustAligned, nameof (builder));
    }

    /// <summary>
    /// Encodes a method body and adds it to the method body stream.
    /// </summary>
    /// <param name="codeSize">Number of bytes to be reserved for instructions.</param>
    /// <param name="maxStack">Max stack.</param>
    /// <param name="exceptionRegionCount">Number of exception regions.</param>
    /// <param name="hasSmallExceptionRegions">True if the exception regions should be encoded in 'small' format.</param>
    /// <param name="localVariablesSignature">Local variables signature handle.</param>
    /// <param name="attributes">Attributes.</param>
    /// <returns>The offset of the encoded body within the method body stream.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="codeSize" />, <paramref name="exceptionRegionCount" />, or <paramref name="maxStack" /> is out of allowed range.
    /// </exception>
    public MethodBodyStreamEncoder.MethodBody AddMethodBody(
      int codeSize,
      int maxStack,
      int exceptionRegionCount,
      bool hasSmallExceptionRegions,
      StandaloneSignatureHandle localVariablesSignature,
      MethodBodyAttributes attributes)
    {
      return this.AddMethodBody(codeSize, maxStack, exceptionRegionCount, hasSmallExceptionRegions, localVariablesSignature, attributes, false);
    }

    /// <summary>
    /// Encodes a method body and adds it to the method body stream.
    /// </summary>
    /// <param name="codeSize">Number of bytes to be reserved for instructions.</param>
    /// <param name="maxStack">Max stack.</param>
    /// <param name="exceptionRegionCount">Number of exception regions.</param>
    /// <param name="hasSmallExceptionRegions">True if the exception regions should be encoded in 'small' format.</param>
    /// <param name="localVariablesSignature">Local variables signature handle.</param>
    /// <param name="attributes">Attributes.</param>
    /// <param name="hasDynamicStackAllocation">True if the method allocates from dynamic local memory pool (<c>localloc</c> instruction).</param>
    /// <returns>The offset of the encoded body within the method body stream.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="codeSize" />, <paramref name="exceptionRegionCount" />, or <paramref name="maxStack" /> is out of allowed range.
    /// </exception>
    public MethodBodyStreamEncoder.MethodBody AddMethodBody(
      int codeSize,
      int maxStack = 8,
      int exceptionRegionCount = 0,
      bool hasSmallExceptionRegions = true,
      StandaloneSignatureHandle localVariablesSignature = default (StandaloneSignatureHandle),
      MethodBodyAttributes attributes = MethodBodyAttributes.InitLocals,
      bool hasDynamicStackAllocation = false)
    {
      if (codeSize < 0)
        Throw.ArgumentOutOfRange(nameof (codeSize));
      if ((uint) maxStack > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (maxStack));
      if (!ExceptionRegionEncoder.IsExceptionRegionCountInBounds(exceptionRegionCount))
        Throw.ArgumentOutOfRange(nameof (exceptionRegionCount));
      return new MethodBodyStreamEncoder.MethodBody(this.SerializeHeader(codeSize, (ushort) maxStack, exceptionRegionCount, attributes, localVariablesSignature, hasDynamicStackAllocation), this.Builder.ReserveBytes(codeSize), exceptionRegionCount > 0 ? ExceptionRegionEncoder.SerializeTableHeader(this.Builder, exceptionRegionCount, hasSmallExceptionRegions) : new ExceptionRegionEncoder());
    }

    /// <summary>
    /// Encodes a method body and adds it to the method body stream.
    /// </summary>
    /// <param name="instructionEncoder">Instruction encoder.</param>
    /// <param name="maxStack">Max stack.</param>
    /// <param name="localVariablesSignature">Local variables signature handle.</param>
    /// <param name="attributes">Attributes.</param>
    /// <returns>The offset of the encoded body within the method body stream.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="instructionEncoder" /> has default value.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="maxStack" /> is out of range [0, <see cref="F:System.UInt16.MaxValue" />].</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// A label targeted by a branch in the instruction stream has not been marked,
    /// or the distance between a branch instruction and the target label doesn't fit the size of the instruction operand.
    /// </exception>
    public int AddMethodBody(
      InstructionEncoder instructionEncoder,
      int maxStack,
      StandaloneSignatureHandle localVariablesSignature,
      MethodBodyAttributes attributes)
    {
      return this.AddMethodBody(instructionEncoder, maxStack, localVariablesSignature, attributes, false);
    }

    /// <summary>
    /// Encodes a method body and adds it to the method body stream.
    /// </summary>
    /// <param name="instructionEncoder">Instruction encoder.</param>
    /// <param name="maxStack">Max stack.</param>
    /// <param name="localVariablesSignature">Local variables signature handle.</param>
    /// <param name="attributes">Attributes.</param>
    /// <param name="hasDynamicStackAllocation">True if the method allocates from dynamic local memory pool (the IL contains <c>localloc</c> instruction).
    /// </param>
    /// <returns>The offset of the encoded body within the method body stream.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="instructionEncoder" /> has default value.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="maxStack" /> is out of range [0, <see cref="F:System.UInt16.MaxValue" />].</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// A label targeted by a branch in the instruction stream has not been marked,
    /// or the distance between a branch instruction and the target label doesn't fit the size of the instruction operand.
    /// </exception>
    public int AddMethodBody(
      InstructionEncoder instructionEncoder,
      int maxStack = 8,
      StandaloneSignatureHandle localVariablesSignature = default (StandaloneSignatureHandle),
      MethodBodyAttributes attributes = MethodBodyAttributes.InitLocals,
      bool hasDynamicStackAllocation = false)
    {
      if ((uint) maxStack > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (maxStack));
      BlobBuilder codeBuilder = instructionEncoder.CodeBuilder;
      ControlFlowBuilder controlFlowBuilder = instructionEncoder.ControlFlowBuilder;
      if (codeBuilder == null)
        Throw.ArgumentNull(nameof (instructionEncoder));
      int exceptionHandlerCount = controlFlowBuilder != null ? controlFlowBuilder.ExceptionHandlerCount : 0;
      if (!ExceptionRegionEncoder.IsExceptionRegionCountInBounds(exceptionHandlerCount))
        Throw.ArgumentOutOfRange(nameof (instructionEncoder), SR.TooManyExceptionRegions);
      int num = this.SerializeHeader(codeBuilder.Count, (ushort) maxStack, exceptionHandlerCount, attributes, localVariablesSignature, hasDynamicStackAllocation);
      if (controlFlowBuilder != null && controlFlowBuilder.BranchCount > 0)
        controlFlowBuilder.CopyCodeAndFixupBranches(codeBuilder, this.Builder);
      else
        codeBuilder.WriteContentTo(this.Builder);
      controlFlowBuilder?.SerializeExceptionTable(this.Builder);
      return num;
    }

    private int SerializeHeader(
      int codeSize,
      ushort maxStack,
      int exceptionRegionCount,
      MethodBodyAttributes attributes,
      StandaloneSignatureHandle localVariablesSignature,
      bool hasDynamicStackAllocation)
    {
      bool flag = (attributes & MethodBodyAttributes.InitLocals) != 0;
      int count;
      if (codeSize < 64 && maxStack <= (ushort) 8 && localVariablesSignature.IsNil && (!hasDynamicStackAllocation || !flag) && exceptionRegionCount == 0)
      {
        count = this.Builder.Count;
        this.Builder.WriteByte((byte) (codeSize << 2 | 2));
      }
      else
      {
        this.Builder.Align(4);
        count = this.Builder.Count;
        ushort num = 12291;
        if (exceptionRegionCount > 0)
          num |= (ushort) 8;
        if (flag)
          num |= (ushort) 16;
        this.Builder.WriteUInt16((ushort) (attributes | (MethodBodyAttributes) num));
        this.Builder.WriteUInt16(maxStack);
        this.Builder.WriteInt32(codeSize);
        this.Builder.WriteInt32(localVariablesSignature.IsNil ? 0 : MetadataTokens.GetToken((EntityHandle) localVariablesSignature));
      }
      return count;
    }

    public readonly struct MethodBody
    {
      /// <summary>
      /// Offset of the encoded method body in method body stream.
      /// </summary>
      public int Offset { get; }

      /// <summary>Blob reserved for instructions.</summary>
      public Blob Instructions { get; }

      /// <summary>Use to encode exception regions to the method body.</summary>
      public ExceptionRegionEncoder ExceptionRegions { get; }

      internal MethodBody(
        int bodyOffset,
        Blob instructions,
        ExceptionRegionEncoder exceptionRegions)
      {
        this.Offset = bodyOffset;
        this.Instructions = instructions;
        this.ExceptionRegions = exceptionRegions;
      }
    }
  }
}
