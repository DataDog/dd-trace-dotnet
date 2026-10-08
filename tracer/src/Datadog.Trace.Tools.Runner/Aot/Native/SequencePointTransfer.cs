// <copyright file="SequencePointTransfer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Moves the sequence points of an original method body to its rewritten body (A8): the native rewriter doesn't give an
/// IL map for CallTarget, but the original instructions are still there, in the same order (a <c>ret</c> becomes a store
/// and a <c>leave</c> to the epilogue), around the prologue and epilogue it adds. The instructions are aligned (longest
/// common subsequence, or a greedy match for very large methods), and each sequence point goes to its instruction.
/// </summary>
internal static class SequencePointTransfer
{
    /// <summary>Above this many cells (original × rewritten instructions), the greedy alignment is used.</summary>
    private const long MaxAlignmentCells = 4_000_000;

    /// <returns>The number of sequence points moved.</returns>
    public static int Transfer(CilBody original, CilBody rewritten)
    {
        var source = original.Instructions;
        var target = rewritten.Instructions;
        if (source.Count == 0 || target.Count == 0 || !HasSequencePoints(source))
        {
            return 0;
        }

        var map = (long)source.Count * target.Count <= MaxAlignmentCells ? AlignLcs(source, target) : AlignGreedy(source, target);
        var moved = 0;

        // The prologue the rewriter adds (BeginMethod) belongs to the first line of the method, as when the profiler
        // rewrites it at runtime and the original PDB maps its offsets to the first sequence point.
        foreach (var instruction in source)
        {
            if (instruction.SequencePoint is { } first)
            {
                if (target[0].SequencePoint is null && map.Length > 0 && map[0] != 0)
                {
                    target[0].SequencePoint = first;
                    moved++;
                }

                break;
            }
        }

        var nextTarget = 0;
        for (var i = 0; i < source.Count; i++)
        {
            var mapped = map[i];
            if (mapped >= 0)
            {
                nextTarget = mapped + 1;
            }

            if (source[i].SequencePoint is not { } sequencePoint)
            {
                continue;
            }

            // An instruction the rewriter replaced (a ret) gives its line to what replaced it: the next instruction after
            // the previous aligned one.
            var index = mapped >= 0 ? mapped : nextTarget;
            if (index < target.Count && target[index].SequencePoint is null)
            {
                target[index].SequencePoint = sequencePoint;
                moved++;
            }
        }

        return moved;
    }

    private static bool HasSequencePoints(IList<Instruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.SequencePoint is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Longest common subsequence of equivalent instructions: map[i] is the rewritten index of original instruction i, or -1.
    /// </summary>
    private static int[] AlignLcs(IList<Instruction> source, IList<Instruction> target)
    {
        int n = source.Count, m = target.Count;
        var lengths = new int[(n + 1) * (m + 1)];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lengths[(i * (m + 1)) + j] = Equivalent(source[i], target[j])
                                                 ? lengths[((i + 1) * (m + 1)) + j + 1] + 1
                                                 : Math.Max(lengths[((i + 1) * (m + 1)) + j], lengths[(i * (m + 1)) + j + 1]);
            }
        }

        var map = new int[n];
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (Equivalent(source[x], target[y]) && lengths[(x * (m + 1)) + y] == lengths[((x + 1) * (m + 1)) + y + 1] + 1)
            {
                map[x++] = y++;
            }
            else if (lengths[((x + 1) * (m + 1)) + y] >= lengths[(x * (m + 1)) + y + 1])
            {
                map[x++] = -1;
            }
            else
            {
                y++;
            }
        }

        while (x < n)
        {
            map[x++] = -1;
        }

        return map;
    }

    /// <summary>
    /// For very large methods: each original instruction is matched with the next equivalent rewritten one, starting where
    /// the first few original instructions line up (after the prologue).
    /// </summary>
    private static int[] AlignGreedy(IList<Instruction> source, IList<Instruction> target)
    {
        var map = new int[source.Count];
        var start = 0;
        var anchor = Math.Min(4, source.Count);
        for (var j = 0; j + anchor <= target.Count; j++)
        {
            var matches = true;
            for (var k = 0; k < anchor && matches; k++)
            {
                matches = Equivalent(source[k], target[j + k]);
            }

            if (matches)
            {
                start = j;
                break;
            }
        }

        var next = start;
        for (var i = 0; i < source.Count; i++)
        {
            map[i] = -1;
            for (var j = next; j < target.Count; j++)
            {
                if (Equivalent(source[i], target[j]))
                {
                    map[i] = j;
                    next = j + 1;
                    break;
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Same opcode and same operand: members and types resolve to the same objects (original tokens), branch targets,
    /// locals and parameters are compared by kind and index (the rewriter adds locals after the original ones).
    /// </summary>
    private static bool Equivalent(Instruction left, Instruction right)
    {
        // The rewriter replaces every original ret (by a leave to the epilogue, which ends with its own ret): a ret is never
        // aligned, so its line goes to the instruction that replaced it, and the epilogue inherits it.
        if (left.OpCode.Code != right.OpCode.Code || left.OpCode.Code == Code.Ret)
        {
            return false;
        }

        return (left.Operand, right.Operand) switch
        {
            (null, null) => true,
            (Instruction, Instruction) or (Instruction[], Instruction[]) => true,
            (Local leftLocal, Local rightLocal) => leftLocal.Index == rightLocal.Index,
            (Parameter leftParameter, Parameter rightParameter) => leftParameter.Index == rightParameter.Index,
            (IMDTokenProvider leftMember, IMDTokenProvider rightMember) => ReferenceEquals(leftMember, rightMember) || leftMember.MDToken == rightMember.MDToken,
            ({ } leftValue, { } rightValue) => leftValue.Equals(rightValue),
            _ => false,
        };
    }
}
#endif
