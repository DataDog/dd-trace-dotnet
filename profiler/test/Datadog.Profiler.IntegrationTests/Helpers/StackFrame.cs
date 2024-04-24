// <copyright file="StackFrame.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal readonly struct StackFrame : IEquatable<StackFrame>
    {
        public readonly string Module;
        public readonly string Namespace;
        public readonly string Type;
        public readonly string TypeAdornment;
        public readonly string Function;
        public readonly string FunctionAdornment;
        public readonly string Signature;
        public readonly string Filename;
        public readonly long StartLine;
        public readonly long Line;

        public StackFrame(string rawStackFrame)
            : this(rawStackFrame, string.Empty, 0, 0)
        {
        }

        public StackFrame(string rawStackFrame, string filename, long startLine, long line)
        {
            // |lm:Datadog.Demos.ExceptionGenerator |ns:ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw1_2
            var match = Regex.Match(rawStackFrame, @"^\|lm:(?<module>.*) \|ns:(?<namespace>.*) \|ct:(?<type>.*) \|cg:(?<typeAdorn>.*) \|fn:(?<function>.*) \|fg:(?<functionArdorn>.*) \|sg:(?<signature>.*)$");

            if (!match.Success)
            {
                throw new FormatException("Could not parse stackframe: " + rawStackFrame);
            }

            Module = match.Groups["module"].Value;
            Namespace = match.Groups["namespace"].Value;
            Type = match.Groups["type"].Value;
            TypeAdornment = match.Groups["typeAdorn"].Value;
            Function = match.Groups["function"].Value;
            FunctionAdornment = match.Groups["functionAdorn"].Value;
            Signature = match.Groups["signature"].Value;
            Filename = filename;
            StartLine = startLine;
            Line = line;
        }

        public StackFrame(string module, string ns, string type, string function, string signature)
        {
            Module = module;
            Namespace = ns;
            Type = type;
            Function = function;
            Signature = signature;
        }

        public override string ToString() => $"|lm:{Module} |ns:{Namespace} |ct:{Type} |cg:{TypeAdornment} |fn:{Function} |fg:{FunctionAdornment} |sg:{Signature}";

        public bool Equals(StackFrame other)
        {
            // for now we do not take into account Filename and StartLine/Line.
            // Expecially Filename since the path can vary in CI
            return
                Module == other.Module &&
                Namespace == other.Namespace &&
                Type == other.Type &&
                TypeAdornment == other.TypeAdornment &&
                Function == other.Function &&
                FunctionAdornment == other.FunctionAdornment &&
                Signature == other.Signature;
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
                hashCode = (hashCode * 397) ^ (TypeAdornment != null ? TypeAdornment.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Function != null ? Function.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (FunctionAdornment != null ? FunctionAdornment.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Signature != null ? Signature.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
