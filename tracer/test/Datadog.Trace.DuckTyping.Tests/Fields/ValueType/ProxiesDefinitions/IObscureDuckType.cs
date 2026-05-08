// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        [DuckField(Name = "_publicStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int PublicStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int InternalStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int ProtectedStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int PrivateStaticReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticValueTypeField", FallbackToBaseTypes = true)]
        int PublicStaticValueTypeField { get; set; }

        [DuckField(Name = "_internalStaticValueTypeField", FallbackToBaseTypes = true)]
        int InternalStaticValueTypeField { get; set; }

        [DuckField(Name = "_protectedStaticValueTypeField", FallbackToBaseTypes = true)]
        int ProtectedStaticValueTypeField { get; set; }

        [DuckField(Name = "_privateStaticValueTypeField", FallbackToBaseTypes = true)]
        int PrivateStaticValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int PublicReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int InternalReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int ProtectedReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateReadonlyValueTypeField", FallbackToBaseTypes = true)]
        int PrivateReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicValueTypeField", FallbackToBaseTypes = true)]
        int PublicValueTypeField { get; set; }

        [DuckField(Name = "_internalValueTypeField", FallbackToBaseTypes = true)]
        int InternalValueTypeField { get; set; }

        [DuckField(Name = "_protectedValueTypeField", FallbackToBaseTypes = true)]
        int ProtectedValueTypeField { get; set; }

        [DuckField(Name = "_privateValueTypeField", FallbackToBaseTypes = true)]
        int PrivateValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField", FallbackToBaseTypes = true)]
        int? PublicStaticNullableIntField { get; set; }

        [DuckField(Name = "_privateStaticNullableIntField", FallbackToBaseTypes = true)]
        int? PrivateStaticNullableIntField { get; set; }

        [DuckField(Name = "_publicNullableIntField", FallbackToBaseTypes = true)]
        int? PublicNullableIntField { get; set; }

        [DuckField(Name = "_privateNullableIntField", FallbackToBaseTypes = true)]
        int? PrivateNullableIntField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField", FallbackToBaseTypes = true)]
        ValueWithType<int?> PublicStaticNullableIntFieldWithType { get; set; }
    }
}
