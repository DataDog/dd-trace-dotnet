// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [DuckField(Name = "_publicStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PublicStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int InternalStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int ProtectedStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PrivateStaticReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PublicStaticValueTypeField { get; set; }

        [DuckField(Name = "_internalStaticValueTypeField", FallbackToBaseTypes = true)]
        public abstract int InternalStaticValueTypeField { get; set; }

        [DuckField(Name = "_protectedStaticValueTypeField", FallbackToBaseTypes = true)]
        public abstract int ProtectedStaticValueTypeField { get; set; }

        [DuckField(Name = "_privateStaticValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PrivateStaticValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PublicReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int InternalReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int ProtectedReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PrivateReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PublicValueTypeField { get; set; }

        [DuckField(Name = "_internalValueTypeField", FallbackToBaseTypes = true)]
        public abstract int InternalValueTypeField { get; set; }

        [DuckField(Name = "_protectedValueTypeField", FallbackToBaseTypes = true)]
        public abstract int ProtectedValueTypeField { get; set; }

        [DuckField(Name = "_privateValueTypeField", FallbackToBaseTypes = true)]
        public abstract int PrivateValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField", FallbackToBaseTypes = true)]
        public abstract int? PublicStaticNullableIntField { get; set; }

        [DuckField(Name = "_privateStaticNullableIntField", FallbackToBaseTypes = true)]
        public abstract int? PrivateStaticNullableIntField { get; set; }

        [DuckField(Name = "_publicNullableIntField", FallbackToBaseTypes = true)]
        public abstract int? PublicNullableIntField { get; set; }

        [DuckField(Name = "_privateNullableIntField", FallbackToBaseTypes = true)]
        public abstract int? PrivateNullableIntField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField", FallbackToBaseTypes = true)]
        public abstract ValueWithType<int?> PublicStaticNullableIntFieldWithType { get; set; }
    }
}
