// <copyright file="Where.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal class Where : IEquatable<Where>
    {
        public string? TypeName { get; set; }

        public string? MethodName { get; set; }

        public string? SourceFile { get; set; }

        public string? Signature { get; set; }

        public string[]? Lines { get; set; }

        public bool Equals(Where? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return TypeName == other.TypeName && MethodName == other.MethodName && SourceFile == other.SourceFile && Signature == other.Signature && Lines.NullableSequentialEquals(other.Lines);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((Where)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TypeName, MethodName, SourceFile, Signature, Lines);
        }
    }
}
