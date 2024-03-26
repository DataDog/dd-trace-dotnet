// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
        public virtual int PublicStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
        public virtual int InternalStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
        public virtual int ProtectedStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
        public virtual int PrivateStaticReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticValueTypeField")]
        public virtual int PublicStaticValueTypeField { get; set; }

        [DuckField(Name = "_internalStaticValueTypeField")]
        public virtual int InternalStaticValueTypeField { get; set; }

        [DuckField(Name = "_protectedStaticValueTypeField")]
        public virtual int ProtectedStaticValueTypeField { get; set; }

        [DuckField(Name = "_privateStaticValueTypeField")]
        public virtual int PrivateStaticValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyValueTypeField")]
        public virtual int PublicReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalReadonlyValueTypeField")]
        public virtual int InternalReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedReadonlyValueTypeField")]
        public virtual int ProtectedReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateReadonlyValueTypeField")]
        public virtual int PrivateReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicValueTypeField")]
        public virtual int PublicValueTypeField { get; set; }

        [DuckField(Name = "_internalValueTypeField")]
        public virtual int InternalValueTypeField { get; set; }

        [DuckField(Name = "_protectedValueTypeField")]
        public virtual int ProtectedValueTypeField { get; set; }

        [DuckField(Name = "_privateValueTypeField")]
        public virtual int PrivateValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField")]
        public virtual int? PublicStaticNullableIntField { get; set; }

        [DuckField(Name = "_privateStaticNullableIntField")]
        public virtual int? PrivateStaticNullableIntField { get; set; }

        [DuckField(Name = "_publicNullableIntField")]
        public virtual int? PublicNullableIntField { get; set; }

        [DuckField(Name = "_privateNullableIntField")]
        public virtual int? PrivateNullableIntField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField")]
        public virtual ValueWithType<int?> PublicStaticNullableIntFieldWithType { get; set; }
    }
}
