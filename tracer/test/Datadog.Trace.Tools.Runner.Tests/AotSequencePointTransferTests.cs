// <copyright file="AotSequencePointTransferTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Linq;
using Datadog.Trace.Tools.Runner.Aot.Native;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Pdb;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class AotSequencePointTransferTests
{
    private static readonly PdbDocument Document = new() { Url = "Target.cs" };

    [Fact]
    public void LinesFollowTheOriginalInstructionsAroundThePrologueAndEpilogue()
    {
        // Original: line 10 `ldstr; call`, line 11 `ldc.i4.1; pop`, line 12 `ret`.
        var original = new CilBody();
        original.Instructions.Add(At(OpCodes.Ldstr.ToInstruction("hello"), 10));
        original.Instructions.Add(OpCodes.Pop.ToInstruction());
        original.Instructions.Add(At(OpCodes.Ldc_I4_1.ToInstruction(), 11));
        original.Instructions.Add(OpCodes.Pop.ToInstruction());
        original.Instructions.Add(At(OpCodes.Ret.ToInstruction(), 12));

        // Rewritten: prologue, the original instructions with the ret replaced by a leave, epilogue.
        var rewritten = new CilBody();
        var epilogue = OpCodes.Nop.ToInstruction();
        rewritten.Instructions.Add(OpCodes.Ldnull.ToInstruction());
        rewritten.Instructions.Add(OpCodes.Pop.ToInstruction());
        rewritten.Instructions.Add(OpCodes.Ldstr.ToInstruction("hello"));
        rewritten.Instructions.Add(OpCodes.Pop.ToInstruction());
        rewritten.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
        rewritten.Instructions.Add(OpCodes.Pop.ToInstruction());
        rewritten.Instructions.Add(OpCodes.Leave.ToInstruction(epilogue));
        rewritten.Instructions.Add(epilogue);
        rewritten.Instructions.Add(OpCodes.Ret.ToInstruction());

        SequencePointTransfer.Transfer(original, rewritten).Should().Be(4);

        var lines = rewritten.Instructions.Select(i => i.SequencePoint?.StartLine).ToList();
        lines.Should().Equal(10, null, 10, null, 11, null, 12, null, null);
    }

    [Fact]
    public void BodiesWithoutSequencePointsAreLeftAsTheyAre()
    {
        var original = new CilBody();
        original.Instructions.Add(OpCodes.Ret.ToInstruction());
        var rewritten = new CilBody();
        rewritten.Instructions.Add(OpCodes.Ret.ToInstruction());

        SequencePointTransfer.Transfer(original, rewritten).Should().Be(0);
        rewritten.Instructions[0].SequencePoint.Should().BeNull();
    }

    private static Instruction At(Instruction instruction, int line)
    {
        instruction.SequencePoint = new SequencePoint { Document = Document, StartLine = line, EndLine = line, StartColumn = 1, EndColumn = 2 };
        return instruction;
    }
}
#endif
