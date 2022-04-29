// <copyright file="StackFrame.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;

namespace Datadog.Profiler.IntegrationTests.Helpers;

internal readonly struct StackFrame : IEquatable<StackFrame>
{
    public readonly string Module;
    public readonly string Namespace;
    public readonly string Type;
    public readonly string Function;

    public StackFrame(string rawStackFrame)
    {
        // |lm:Datadog.Demos.ExceptionGenerator |ns:ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw1_2
        var match = Regex.Match(rawStackFrame, @"^\|lm:(?<module>.*) \|ns:(?<namespace>.*) \|ct:(?<type>.*) \|fn:(?<function>.*)$");

        if (!match.Success)
        {
            throw new FormatException("Could not parse stackframe: " + rawStackFrame);
        }

        Module = match.Groups["module"].Value;
        Namespace = match.Groups["namespace"].Value;
        Type = match.Groups["type"].Value;
        Function = match.Groups["function"].Value;
    }

    public StackFrame(string module, string ns, string type, string function)
    {
        Module = module;
        Namespace = ns;
        Type = type;
        Function = function;
    }

    public override string ToString() => $"|lm:{Module} |ns:{Namespace} |ct:{Type} |fn:{Function}";

    public bool Equals(StackFrame other)
    {
        return Module == other.Module && Namespace == other.Namespace && Type == other.Type && Function == other.Function;
    }

    public override bool Equals(object obj)
    {
        return obj is StackFrame other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Module != null ? Module.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Namespace != null ? Namespace.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Function != null ? Function.GetHashCode() : 0);
            return hashCode;
        }
    }
}
