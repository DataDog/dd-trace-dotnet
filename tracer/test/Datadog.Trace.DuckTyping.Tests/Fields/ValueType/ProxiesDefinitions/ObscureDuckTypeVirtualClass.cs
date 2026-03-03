// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [DuckField(Name = "_publicStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PublicStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int InternalStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int ProtectedStaticReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PrivateStaticReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PublicStaticValueTypeField { get; set; }

        [DuckField(Name = "_internalStaticValueTypeField", FallbackToBaseTypes = true)]
        public virtual int InternalStaticValueTypeField { get; set; }

        [DuckField(Name = "_protectedStaticValueTypeField", FallbackToBaseTypes = true)]
        public virtual int ProtectedStaticValueTypeField { get; set; }

        [DuckField(Name = "_privateStaticValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PrivateStaticValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PublicReadonlyValueTypeField { get; }

        [DuckField(Name = "_internalReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int InternalReadonlyValueTypeField { get; }

        [DuckField(Name = "_protectedReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int ProtectedReadonlyValueTypeField { get; }

        [DuckField(Name = "_privateReadonlyValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PrivateReadonlyValueTypeField { get; }

        // *

        [DuckField(Name = "_publicValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PublicValueTypeField { get; set; }

        [DuckField(Name = "_internalValueTypeField", FallbackToBaseTypes = true)]
        public virtual int InternalValueTypeField { get; set; }

        [DuckField(Name = "_protectedValueTypeField", FallbackToBaseTypes = true)]
        public virtual int ProtectedValueTypeField { get; set; }

        [DuckField(Name = "_privateValueTypeField", FallbackToBaseTypes = true)]
        public virtual int PrivateValueTypeField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField", FallbackToBaseTypes = true)]
        public virtual int? PublicStaticNullableIntField { get; set; }

        [DuckField(Name = "_privateStaticNullableIntField", FallbackToBaseTypes = true)]
        public virtual int? PrivateStaticNullableIntField { get; set; }

        [DuckField(Name = "_publicNullableIntField", FallbackToBaseTypes = true)]
        public virtual int? PublicNullableIntField { get; set; }

        [DuckField(Name = "_privateNullableIntField", FallbackToBaseTypes = true)]
        public virtual int? PrivateNullableIntField { get; set; }

        // *

        [DuckField(Name = "_publicStaticNullableIntField", FallbackToBaseTypes = true)]
        public virtual ValueWithType<int?> PublicStaticNullableIntFieldWithType { get; set; }
    }
}
