// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
        public abstract int PublicStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
        public abstract int InternalStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
        public abstract int ProtectedStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
        public abstract int PrivateStaticReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticValueTypeField")]
        public abstract int PublicStaticValueTypeField { get; set; }

        [DuckField(Name = "_internalStaticValueTypeField")]
        public abstract int InternalStaticValueTypeField { get; set; }

        [DuckField(Name = "_protectedStaticValueTypeField")]
        public abstract int ProtectedStaticValueTypeField { get; set; }

        [DuckField(Name = "_privateStaticValueTypeField")]
        public abstract int PrivateStaticValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyValueTypeField")]
        public abstract int PublicReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalReadonlyValueTypeField")]
        public abstract int InternalReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedReadonlyValueTypeField")]
        public abstract int ProtectedReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateReadonlyValueTypeField")]
        public abstract int PrivateReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicValueTypeField")]
        public abstract int PublicValueTypeField { get; set; }

        [DuckField(Name = "_internalValueTypeField")]
        public abstract int InternalValueTypeField { get; set; }

        [DuckField(Name = "_protectedValueTypeField")]
        public abstract int ProtectedValueTypeField { get; set; }

        [DuckField(Name = "_privateValueTypeField")]
        public abstract int PrivateValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField")]
        public abstract int? PublicStaticNullableIntField { get; set; }

        [DuckField(Name = "_privateStaticNullableIntField")]
        public abstract int? PrivateStaticNullableIntField { get; set; }

        [DuckField(Name = "_publicNullableIntField")]
        public abstract int? PublicNullableIntField { get; set; }

        [DuckField(Name = "_privateNullableIntField")]
        public abstract int? PrivateNullableIntField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField")]
        public abstract ValueWithType<int?> PublicStaticNullableIntFieldWithType { get; set; }
    }
}
