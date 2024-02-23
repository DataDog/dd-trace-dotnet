// <copyright file="Cursor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using System.Linq;
using Mono.Cecil.Cil;

namespace Datadog.Trace.NativeAotTask;

internal class Cursor
{
    private readonly ILProcessor _ilProcessor;
    private Instruction? _instruction;

    public Cursor(ILProcessor ilProcessor, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin)
        {
            _instruction = null;
        }
        else if (origin == SeekOrigin.End)
        {
            _instruction = ilProcessor.Body.Instructions.Last();
        }
        else
        {
            throw new System.ArgumentException($"Origin {origin} is not supported");
        }

        _ilProcessor = ilProcessor;
    }

    public Cursor(ILProcessor ilProcessor, Instruction? instruction)
    {
        _ilProcessor = ilProcessor;
        _instruction = instruction;
    }

    public Instruction Instruction
    {
        get
        {
            if (_instruction == null)
            {
                throw new System.InvalidOperationException("Cursor is not pointing to an instruction");
            }

            return _instruction;
        }
    }

    public Cursor Append(Instruction instruction)
    {
        if (_instruction == null)
        {
            if (_ilProcessor.Body.Instructions.Count == 0)
            {
                _ilProcessor.Append(instruction);
            }
            else
            {
                _ilProcessor.InsertBefore(_ilProcessor.Body.Instructions[0], instruction);
            }
        }
        else
        {
            _ilProcessor.InsertAfter(Instruction, instruction);
        }

        _instruction = instruction;

        return this;
    }

    public Cursor Clone() => new(_ilProcessor, _instruction);
}
