// <copyright file="CatchPopFirstInstructionIlTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using FluentAssertions;
using Samples.Probes.TestRuns.ExceptionReplay;
using Xunit;

namespace Datadog.Trace.Debugger.IntegrationTests.ExceptionReplay;

/// <summary>
/// Locks the compiled IL shape of <see cref="CatchPopFirstInstructionTest"/> so e2e
/// coverage cannot silently become a <c>stloc</c> catch if Roslyn changes codegen.
/// </summary>
public class CatchPopFirstInstructionIlTests
{
    private const byte Prefix1 = 0xFE;
    private const byte LdlocWide = 0x0C;

    [Fact]
    public void CatchHandlerBeginsWithOptionalNopsThenPopAndCloneableSetExceptionLoad()
    {
        var stateMachine = typeof(CatchPopFirstInstructionTest).GetNestedType(
            "CatchPopSm",
            BindingFlags.Public | BindingFlags.NonPublic);
        stateMachine.Should().NotBeNull();

        var moveNext = stateMachine!.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public);
        moveNext.Should().NotBeNull();

        var body = moveNext!.GetMethodBody();
        body.Should().NotBeNull();

        var il = body!.GetILAsByteArray();
        il.Should().NotBeNull();

        var catchClause = body.ExceptionHandlingClauses.Should()
                              .ContainSingle(clause => clause.Flags == ExceptionHandlingClauseOptions.Clause)
                              .Subject;

        var offset = catchClause.HandlerOffset;
        var handlerEnd = offset + catchClause.HandlerLength;
        handlerEnd.Should().BeLessThanOrEqualTo(il!.Length);

        while (offset < handlerEnd && il[offset] == (byte)OpCodes.Nop.Value)
        {
            offset++;
        }

        offset.Should().BeLessThan(handlerEnd, "the SetException catch must contain more than nops");
        il[offset].Should().Be(
            (byte)OpCodes.Pop.Value,
            "nameless catch must emit pop as the exception consumer. Handler IL: {0}",
            FormatIl(il, catchClause.HandlerOffset, handlerEnd));

        var previousOffset = -1;
        var seenCall = false;
        offset += GetInstructionSize(il, offset);

        while (offset < handlerEnd)
        {
            if (il[offset] == (byte)OpCodes.Call.Value)
            {
                seenCall = true;
                previousOffset.Should().BeGreaterThanOrEqualTo(
                    catchClause.HandlerOffset,
                    "SetException must be preceded by a cloneable local load. Handler IL: {0}",
                    FormatIl(il, catchClause.HandlerOffset, handlerEnd));
                IsLoadLocalInstruction(il, previousOffset).Should().BeTrue(
                    "SetException must be preceded by a cloneable local load. Handler IL: {0}",
                    FormatIl(il, catchClause.HandlerOffset, handlerEnd));
                break;
            }

            previousOffset = offset;
            offset += GetInstructionSize(il, offset);
        }

        seenCall.Should().BeTrue("the SetException catch must call SetException");
    }

    private static bool IsLoadLocalInstruction(byte[] il, int offset)
    {
        var opcode = il[offset];
        if (opcode == Prefix1)
        {
            return il[offset + 1] == LdlocWide;
        }

        return opcode is 0x06 or 0x07 or 0x08 or 0x09 or 0x11; // ldloc.0-3, ldloc.s
    }

    private static int GetInstructionSize(byte[] il, int offset)
    {
        var opcode = il[offset];
        if (opcode == Prefix1)
        {
            var second = il[offset + 1];
            // ldloc / stloc / ldarg / starg: FE xx + int16
            if (second is LdlocWide or 0x0E or 0x09 or 0x0B)
            {
                return 4;
            }

            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "Unexpected prefix opcode 0xFE 0x{0:X2} at IL_{1:X4}", second, offset));
        }

        return opcode switch
        {
            0x00 => 1, // nop
            0x02 or 0x03 or 0x04 or 0x05 => 1, // ldarg.0-3
            0x06 or 0x07 or 0x08 or 0x09 => 1, // ldloc.0-3
            0x0A or 0x0B or 0x0C or 0x0D => 1, // stloc.0-3
            0x11 or 0x13 => 2, // ldloc.s, stloc.s
            0x26 => 1, // pop
            0x28 or 0x6F or 0x7B or 0x7C => 5, // call, callvirt, ldfld, ldflda
            0x2A => 1, // ret
            0xDD => 5, // leave
            0xDE => 2, // leave.s
            _ => throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "Unexpected opcode 0x{0:X2} at IL_{1:X4}", opcode, offset))
        };
    }

    private static string FormatIl(byte[] il, int start, int end)
    {
        var builder = new StringBuilder();
        for (var index = start; index < end; index++)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", il[index]);
        }

        return builder.ToString();
    }
}

#endif
