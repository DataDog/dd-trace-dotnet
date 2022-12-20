// <copyright file="ScopeMember.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Conditions
{
    internal enum ScopeMemberKind
    {
        Argument,
        Local,
        This,
        Exception,
        Return
    }

    internal struct ScopeMember
    {
        public object Value;
        public string Name;
        public Type Type;
        public ScopeMemberKind ElementType;

        public ScopeMember(string name, Type type, object value, ScopeMemberKind elementType)
        {
            Name = name;
            Type = type;
            Value = value;
            ElementType = elementType;
        }
    }
}
