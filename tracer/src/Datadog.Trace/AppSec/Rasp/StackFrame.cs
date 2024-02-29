// <copyright file="StackFrame.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rasp;

internal readonly struct StackFrame
{
    // "id": <unsigned integer: index of the stack frame(0 = top of stack)>,
    // "text": <string: raw stack frame> (optional),
    // "file": <string> (optional),
    // "line": <unsigned integer> (optional),
    // "column": <unsigned integer> (optional),
    // "namespace": <string> (optional),
    // "class_name": <string> (optional),
    // "function": <string> (optional),

    private readonly uint _id;
    private readonly string? _text;
    private readonly string? _file;
    private readonly uint? _line;
    private readonly uint? _column;
    private readonly string? _namespace;
    private readonly string? _className;
    private readonly string? _function;

    public StackFrame(uint id, string? text, string? file, uint? line, uint? column, string? ns, string? className, string? function)
    {
        _id = id;
        _text = text;
        _file = file;
        _line = line;
        _column = column;
        _namespace = ns;
        _className = className;
        _function = function;
    }

    public uint Id => _id;

    public string? Text => _text;

    public string? File => _file;

    public uint? Line => _line;

    public uint? Column => _column;

    public string? Namespace => _namespace;

    public string? ClassName => _className;

    public string? Function => _function;
}
