// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [Duck(FallbackToBaseTypes = true)]
        int PublicStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        int InternalStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        int ProtectedStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        int PrivateStaticGetValueType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        int PublicStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int InternalStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int ProtectedStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int PrivateStaticGetSetValueType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        int PublicGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        int InternalGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        int ProtectedGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        int PrivateGetValueType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        int PublicGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int InternalGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int ProtectedGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int PrivateGetSetValueType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        int? PublicStaticNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int? PrivateStaticNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int? PublicNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        int? PrivateNullableInt { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        TaskStatus Status { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetValueType")]
        ValueWithType<int> PublicGetSetValueTypeWithType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        int this[int index] { get; set; }
    }
}
