// <copyright file="ScopeMember.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Expressions
{
    internal enum ScopeMemberKind
    {
        Argument,
        Local,
        This,
        Return,
        Exception,
        Duration,
        None
    }

    internal readonly struct ScopeMember
    {
        internal readonly object Value;
        internal readonly string Name;
        internal readonly Type Type;
        internal readonly ScopeMemberKind ElementType;

        internal ScopeMember(string name, Type type, object value, ScopeMemberKind elementType)
        {
            Name = name;
            Type = type;
            Value = value;
            ElementType = elementType;
        }
    }
}
