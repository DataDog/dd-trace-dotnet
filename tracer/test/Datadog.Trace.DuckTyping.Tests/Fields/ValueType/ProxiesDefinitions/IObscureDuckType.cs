// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
        int PublicStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
        int InternalStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
        int ProtectedStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
        int PrivateStaticReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticValueTypeField")]
        int PublicStaticValueTypeField { get; set; }

        [DuckField(Name = "_internalStaticValueTypeField")]
        int InternalStaticValueTypeField { get; set; }

        [DuckField(Name = "_protectedStaticValueTypeField")]
        int ProtectedStaticValueTypeField { get; set; }

        [DuckField(Name = "_privateStaticValueTypeField")]
        int PrivateStaticValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyValueTypeField")]
        int PublicReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalReadonlyValueTypeField")]
        int InternalReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedReadonlyValueTypeField")]
        int ProtectedReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateReadonlyValueTypeField")]
        int PrivateReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicValueTypeField")]
        int PublicValueTypeField { get; set; }

        [DuckField(Name = "_internalValueTypeField")]
        int InternalValueTypeField { get; set; }

        [DuckField(Name = "_protectedValueTypeField")]
        int ProtectedValueTypeField { get; set; }

        [DuckField(Name = "_privateValueTypeField")]
        int PrivateValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField")]
        int? PublicStaticNullableIntField { get; set; }

        [DuckField(Name = "_privateStaticNullableIntField")]
        int? PrivateStaticNullableIntField { get; set; }

        [DuckField(Name = "_publicNullableIntField")]
        int? PublicNullableIntField { get; set; }

        [DuckField(Name = "_privateNullableIntField")]
        int? PrivateNullableIntField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField")]
        ValueWithType<int?> PublicStaticNullableIntFieldWithType { get; set; }
    }
}
