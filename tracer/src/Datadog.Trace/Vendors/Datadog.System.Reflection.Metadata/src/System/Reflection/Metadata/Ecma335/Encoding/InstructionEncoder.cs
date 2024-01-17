﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.InstructionEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>Encodes instructions.</summary>
  public readonly struct InstructionEncoder
  {
    /// <summary>
    /// Underlying builder where encoded instructions are written to.
    /// </summary>
    public BlobBuilder CodeBuilder { get; }

    /// <summary>
    /// Builder tracking labels, branches and exception handlers.
    /// </summary>
    /// <remarks>
    /// If null the encoder doesn't support construction of control flow.
    /// </remarks>
    public ControlFlowBuilder? ControlFlowBuilder { get; }

    /// <summary>
    /// Creates an encoder backed by code and control-flow builders.
    /// </summary>
    /// <param name="codeBuilder">Builder to write encoded instructions to.</param>
    /// <param name="controlFlowBuilder">
    /// Builder tracking labels, branches and exception handlers.
    /// Must be specified to be able to use some of the control-flow factory methods of <see cref="T:System.Reflection.Metadata.Ecma335.InstructionEncoder" />,
    /// such as <see cref="M:System.Reflection.Metadata.Ecma335.InstructionEncoder.Branch(System.Reflection.Metadata.ILOpCode,System.Reflection.Metadata.Ecma335.LabelHandle)" />, <see cref="M:System.Reflection.Metadata.Ecma335.InstructionEncoder.DefineLabel" />, <see cref="M:System.Reflection.Metadata.Ecma335.InstructionEncoder.MarkLabel(System.Reflection.Metadata.Ecma335.LabelHandle)" /> etc.
    /// </param>
    public InstructionEncoder(BlobBuilder codeBuilder, ControlFlowBuilder? controlFlowBuilder = null)
    {
      if (codeBuilder == null)
        Throw.BuilderArgumentNull();
      this.CodeBuilder = codeBuilder;
      this.ControlFlowBuilder = controlFlowBuilder;
    }

    /// <summary>Offset of the next encoded instruction.</summary>
    public int Offset => this.CodeBuilder.Count;

    /// <summary>Encodes specified op-code.</summary>
    public void OpCode(ILOpCode code)
    {
      if ((ILOpCode) (byte) code == code)
        this.CodeBuilder.WriteByte((byte) code);
      else
        this.CodeBuilder.WriteUInt16BE((ushort) code);
    }

    /// <summary>Encodes a token.</summary>
    public void Token(EntityHandle handle) => this.Token(MetadataTokens.GetToken(handle));

    /// <summary>Encodes a token.</summary>
    public void Token(int token) => this.CodeBuilder.WriteInt32(token);

    /// <summary>
    /// Encodes <code>ldstr</code> instruction and its operand.
    /// </summary>
    public void LoadString(UserStringHandle handle)
    {
      this.OpCode(ILOpCode.Ldstr);
      this.Token(MetadataTokens.GetToken((Handle) handle));
    }

    /// <summary>
    /// Encodes <code>call</code> instruction and its operand.
    /// </summary>
    public void Call(EntityHandle methodHandle)
    {
      if (methodHandle.Kind != HandleKind.MethodDefinition && methodHandle.Kind != HandleKind.MethodSpecification && methodHandle.Kind != HandleKind.MemberReference)
        Throw.InvalidArgument_Handle(nameof (methodHandle));
      this.OpCode(ILOpCode.Call);
      this.Token(methodHandle);
    }

    /// <summary>
    /// Encodes <code>call</code> instruction and its operand.
    /// </summary>
    public void Call(MethodDefinitionHandle methodHandle)
    {
      this.OpCode(ILOpCode.Call);
      this.Token((EntityHandle) methodHandle);
    }

    /// <summary>
    /// Encodes <code>call</code> instruction and its operand.
    /// </summary>
    public void Call(MethodSpecificationHandle methodHandle)
    {
      this.OpCode(ILOpCode.Call);
      this.Token((EntityHandle) methodHandle);
    }

    /// <summary>
    /// Encodes <code>call</code> instruction and its operand.
    /// </summary>
    public void Call(MemberReferenceHandle methodHandle)
    {
      this.OpCode(ILOpCode.Call);
      this.Token((EntityHandle) methodHandle);
    }

    /// <summary>
    /// Encodes <code>calli</code> instruction and its operand.
    /// </summary>
    public void CallIndirect(StandaloneSignatureHandle signature)
    {
      this.OpCode(ILOpCode.Calli);
      this.Token((EntityHandle) signature);
    }

    /// <summary>
    /// Encodes <see cref="T:System.Int32" /> constant load instruction.
    /// </summary>
    public void LoadConstantI4(int value)
    {
      ILOpCode code;
      switch (value)
      {
        case -1:
          code = ILOpCode.Ldc_i4_m1;
          break;
        case 0:
          code = ILOpCode.Ldc_i4_0;
          break;
        case 1:
          code = ILOpCode.Ldc_i4_1;
          break;
        case 2:
          code = ILOpCode.Ldc_i4_2;
          break;
        case 3:
          code = ILOpCode.Ldc_i4_3;
          break;
        case 4:
          code = ILOpCode.Ldc_i4_4;
          break;
        case 5:
          code = ILOpCode.Ldc_i4_5;
          break;
        case 6:
          code = ILOpCode.Ldc_i4_6;
          break;
        case 7:
          code = ILOpCode.Ldc_i4_7;
          break;
        case 8:
          code = ILOpCode.Ldc_i4_8;
          break;
        default:
          if ((int) (sbyte) value == value)
          {
            this.OpCode(ILOpCode.Ldc_i4_s);
            this.CodeBuilder.WriteSByte((sbyte) value);
            return;
          }
          this.OpCode(ILOpCode.Ldc_i4);
          this.CodeBuilder.WriteInt32(value);
          return;
      }
      this.OpCode(code);
    }

    /// <summary>
    /// Encodes <see cref="T:System.Int64" /> constant load instruction.
    /// </summary>
    public void LoadConstantI8(long value)
    {
      this.OpCode(ILOpCode.Ldc_i8);
      this.CodeBuilder.WriteInt64(value);
    }

    /// <summary>
    /// Encodes <see cref="T:System.Single" /> constant load instruction.
    /// </summary>
    public void LoadConstantR4(float value)
    {
      this.OpCode(ILOpCode.Ldc_r4);
      this.CodeBuilder.WriteSingle(value);
    }

    /// <summary>
    /// Encodes <see cref="T:System.Double" /> constant load instruction.
    /// </summary>
    public void LoadConstantR8(double value)
    {
      this.OpCode(ILOpCode.Ldc_r8);
      this.CodeBuilder.WriteDouble(value);
    }

    /// <summary>Encodes local variable load instruction.</summary>
    /// <param name="slotIndex">Index of the local variable slot.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="slotIndex" /> is negative.</exception>
    public void LoadLocal(int slotIndex)
    {
      switch (slotIndex)
      {
        case 0:
          this.OpCode(ILOpCode.Ldloc_0);
          break;
        case 1:
          this.OpCode(ILOpCode.Ldloc_1);
          break;
        case 2:
          this.OpCode(ILOpCode.Ldloc_2);
          break;
        case 3:
          this.OpCode(ILOpCode.Ldloc_3);
          break;
        default:
          if ((uint) slotIndex <= (uint) byte.MaxValue)
          {
            this.OpCode(ILOpCode.Ldloc_s);
            this.CodeBuilder.WriteByte((byte) slotIndex);
            break;
          }
          if (slotIndex > 0)
          {
            this.OpCode(ILOpCode.Ldloc);
            this.CodeBuilder.WriteInt32(slotIndex);
            break;
          }
          Throw.ArgumentOutOfRange(nameof (slotIndex));
          break;
      }
    }

    /// <summary>Encodes local variable store instruction.</summary>
    /// <param name="slotIndex">Index of the local variable slot.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="slotIndex" /> is negative.</exception>
    public void StoreLocal(int slotIndex)
    {
      switch (slotIndex)
      {
        case 0:
          this.OpCode(ILOpCode.Stloc_0);
          break;
        case 1:
          this.OpCode(ILOpCode.Stloc_1);
          break;
        case 2:
          this.OpCode(ILOpCode.Stloc_2);
          break;
        case 3:
          this.OpCode(ILOpCode.Stloc_3);
          break;
        default:
          if ((uint) slotIndex <= (uint) byte.MaxValue)
          {
            this.OpCode(ILOpCode.Stloc_s);
            this.CodeBuilder.WriteByte((byte) slotIndex);
            break;
          }
          if (slotIndex > 0)
          {
            this.OpCode(ILOpCode.Stloc);
            this.CodeBuilder.WriteInt32(slotIndex);
            break;
          }
          Throw.ArgumentOutOfRange(nameof (slotIndex));
          break;
      }
    }

    /// <summary>Encodes local variable address load instruction.</summary>
    /// <param name="slotIndex">Index of the local variable slot.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="slotIndex" /> is negative.</exception>
    public void LoadLocalAddress(int slotIndex)
    {
      if ((uint) slotIndex <= (uint) byte.MaxValue)
      {
        this.OpCode(ILOpCode.Ldloca_s);
        this.CodeBuilder.WriteByte((byte) slotIndex);
      }
      else if (slotIndex > 0)
      {
        this.OpCode(ILOpCode.Ldloca);
        this.CodeBuilder.WriteInt32(slotIndex);
      }
      else
        Throw.ArgumentOutOfRange(nameof (slotIndex));
    }

    /// <summary>Encodes argument load instruction.</summary>
    /// <param name="argumentIndex">Index of the argument.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="argumentIndex" /> is negative.</exception>
    public void LoadArgument(int argumentIndex)
    {
      switch (argumentIndex)
      {
        case 0:
          this.OpCode(ILOpCode.Ldarg_0);
          break;
        case 1:
          this.OpCode(ILOpCode.Ldarg_1);
          break;
        case 2:
          this.OpCode(ILOpCode.Ldarg_2);
          break;
        case 3:
          this.OpCode(ILOpCode.Ldarg_3);
          break;
        default:
          if ((uint) argumentIndex <= (uint) byte.MaxValue)
          {
            this.OpCode(ILOpCode.Ldarg_s);
            this.CodeBuilder.WriteByte((byte) argumentIndex);
            break;
          }
          if (argumentIndex > 0)
          {
            this.OpCode(ILOpCode.Ldarg);
            this.CodeBuilder.WriteInt32(argumentIndex);
            break;
          }
          Throw.ArgumentOutOfRange(nameof (argumentIndex));
          break;
      }
    }

    /// <summary>Encodes argument address load instruction.</summary>
    /// <param name="argumentIndex">Index of the argument.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="argumentIndex" /> is negative.</exception>
    public void LoadArgumentAddress(int argumentIndex)
    {
      if ((uint) argumentIndex <= (uint) byte.MaxValue)
      {
        this.OpCode(ILOpCode.Ldarga_s);
        this.CodeBuilder.WriteByte((byte) argumentIndex);
      }
      else if (argumentIndex > 0)
      {
        this.OpCode(ILOpCode.Ldarga);
        this.CodeBuilder.WriteInt32(argumentIndex);
      }
      else
        Throw.ArgumentOutOfRange(nameof (argumentIndex));
    }

    /// <summary>Encodes argument store instruction.</summary>
    /// <param name="argumentIndex">Index of the argument.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="argumentIndex" /> is negative.</exception>
    public void StoreArgument(int argumentIndex)
    {
      if ((uint) argumentIndex <= (uint) byte.MaxValue)
      {
        this.OpCode(ILOpCode.Starg_s);
        this.CodeBuilder.WriteByte((byte) argumentIndex);
      }
      else if (argumentIndex > 0)
      {
        this.OpCode(ILOpCode.Starg);
        this.CodeBuilder.WriteInt32(argumentIndex);
      }
      else
        Throw.ArgumentOutOfRange(nameof (argumentIndex));
    }

    /// <summary>
    /// Defines a label that can later be used to mark and refer to a location in the instruction stream.
    /// </summary>
    /// <returns>Label handle.</returns>
    /// <exception cref="T:System.InvalidOperationException"><see cref="P:System.Reflection.Metadata.Ecma335.InstructionEncoder.ControlFlowBuilder" /> is null.</exception>
    public LabelHandle DefineLabel() => this.GetBranchBuilder().AddLabel();

    /// <summary>Encodes a branch instruction.</summary>
    /// <param name="code">Branch instruction to encode.</param>
    /// <param name="label">Label of the target location in instruction stream.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="code" /> is not a branch instruction.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="label" /> was not defined by this encoder.</exception>
    /// <exception cref="T:System.InvalidOperationException"><see cref="P:System.Reflection.Metadata.Ecma335.InstructionEncoder.ControlFlowBuilder" /> is null.</exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="label" /> has default value.</exception>
    public void Branch(ILOpCode code, LabelHandle label)
    {
      int branchOperandSize = code.GetBranchOperandSize();
      this.GetBranchBuilder().AddBranch(this.Offset, label, code);
      this.OpCode(code);
      if (branchOperandSize == 1)
        this.CodeBuilder.WriteSByte((sbyte) -1);
      else
        this.CodeBuilder.WriteInt32(-1);
    }

    /// <summary>
    /// Associates specified label with the current IL offset.
    /// </summary>
    /// <param name="label">Label to mark.</param>
    /// <remarks>
    /// A single label may be marked multiple times, the last offset wins.
    /// </remarks>
    /// <exception cref="T:System.InvalidOperationException"><see cref="P:System.Reflection.Metadata.Ecma335.InstructionEncoder.ControlFlowBuilder" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="label" /> was not defined by this encoder.</exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="label" /> has default value.</exception>
    public void MarkLabel(LabelHandle label) => this.GetBranchBuilder().MarkLabel(this.Offset, label);


    #nullable disable
    private ControlFlowBuilder GetBranchBuilder()
    {
      if (this.ControlFlowBuilder == null)
        Throw.ControlFlowBuilderNotAvailable();
      return this.ControlFlowBuilder;
    }
  }
}
