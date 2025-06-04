// <copyright file="TypeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class TypeExtensions
    {
        internal static bool IsNumeric(this Type type)
        {
            while (true)
            {
                if (type == null)
                {
                    return false;
                }

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = Nullable.GetUnderlyingType(type);
                    continue;
                }

                return Type.GetTypeCode(type) switch
                {
                    TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Double or TypeCode.Single or TypeCode.Decimal => true,
                    TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.Boolean or TypeCode.Char or TypeCode.DateTime or TypeCode.String => false,
                    _ => false
                };
            }
        }

        internal static bool IsSimple(Type type)
        {
            while (true)
            {
                if (type == null)
                {
                    return false;
                }

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = Nullable.GetUnderlyingType(type);
                    continue;
                }

                return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
            }
        }

        internal static bool IsDefaultValue<T>(ref T value)
        {
            return EqualityComparer<T>.Default.Equals(value, default);
        }
    }
}
