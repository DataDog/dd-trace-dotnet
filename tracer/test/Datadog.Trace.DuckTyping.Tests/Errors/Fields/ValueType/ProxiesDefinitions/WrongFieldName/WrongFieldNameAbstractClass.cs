// <copyright file="WrongFieldNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongFieldName
{
    public abstract class WrongFieldNameAbstractClass
    {
        public abstract class PublicStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "publicStaticReadonlyValueTypeField")]
            public abstract int PublicStaticReadonlyValueTypeField { get; }
        }

        public abstract class InternalStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "internalStaticReadonlyValueTypeField")]
            public abstract int InternalStaticReadonlyValueTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedStaticReadonlyValueTypeField")]
            public abstract int ProtectedStaticReadonlyValueTypeField { get; }
        }

        public abstract class PrivateStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "privateStaticReadonlyValueTypeField")]
            public abstract int PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public abstract class PublicStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "publicStaticValueTypeField")]
            public abstract int PublicStaticValueTypeField { get; set; }
        }

        public abstract class InternalStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "internalStaticValueTypeField")]
            public abstract int InternalStaticValueTypeField { get; set; }
        }

        public abstract class ProtectedStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedStaticValueTypeField")]
            public abstract int ProtectedStaticValueTypeField { get; set; }
        }

        public abstract class PrivateStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "privateStaticValueTypeField")]
            public abstract int PrivateStaticValueTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "publicReadonlyValueTypeField")]
            public abstract int PublicReadonlyValueTypeField { get; }
        }

        public abstract class InternalReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "internalReadonlyValueTypeField")]
            public abstract int InternalReadonlyValueTypeField { get; }
        }

        public abstract class ProtectedReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedReadonlyValueTypeField")]
            public abstract int ProtectedReadonlyValueTypeField { get; }
        }

        public abstract class PrivateReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "privateReadonlyValueTypeField")]
            public abstract int PrivateReadonlyValueTypeField { get; }
        }

        // *

        public abstract class PublicValueTypeFieldAbstractClass
        {
            [DuckField(Name = "publicValueTypeField")]
            public abstract int PublicValueTypeField { get; set; }
        }

        public abstract class InternalValueTypeFieldAbstractClass
        {
            [DuckField(Name = "internalValueTypeField")]
            public abstract int InternalValueTypeField { get; set; }
        }

        public abstract class ProtectedValueTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedValueTypeField")]
            public abstract int ProtectedValueTypeField { get; set; }
        }

        public abstract class PrivateValueTypeFieldAbstractClass
        {
            [DuckField(Name = "privateValueTypeField")]
            public abstract int PrivateValueTypeField { get; set; }
        }

        // *

        public abstract class PublicStaticNullableIntFieldAbstractClass
        {
            [DuckField(Name = "publicStaticNullableIntField")]
            public abstract int? PublicStaticNullableIntField { get; set; }
        }

        public abstract class PrivateStaticNullableIntFieldAbstractClass
        {
            [DuckField(Name = "privateStaticNullableIntField")]
            public abstract int? PrivateStaticNullableIntField { get; set; }
        }

        public abstract class PublicNullableIntFieldAbstractClass
        {
            [DuckField(Name = "publicNullableIntField")]
            public abstract int? PublicNullableIntField { get; set; }
        }

        public abstract class PrivateNullableIntFieldAbstractClass
        {
            [DuckField(Name = "privateNullableIntField")]
            public abstract int? PrivateNullableIntField { get; set; }
        }
    }
}
