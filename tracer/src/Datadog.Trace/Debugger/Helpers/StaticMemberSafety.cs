// <copyright file="StaticMemberSafety.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class StaticMemberSafety
    {
        public static bool CanReadStaticMember(MemberInfo memberInfo)
        {
            var declaringType = memberInfo.DeclaringType;
            return declaringType is { TypeInitializer: null };
        }

        public static object? GetRawConstantValue(FieldInfo field)
        {
            var value = field.GetRawConstantValue();
            return value is not null && field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, value) : value;
        }
    }
}
