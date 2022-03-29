// <copyright file="ValidAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.Valid
{
    public abstract class ValidAbstractClass
    {
        public abstract class PublicStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            public abstract int PublicStaticReadonlyValueTypeField { get; }
        }

        public abstract class InternalStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            public abstract int InternalStaticReadonlyValueTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            public abstract int ProtectedStaticReadonlyValueTypeField { get; }
        }

        public abstract class PrivateStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            public abstract int PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public abstract class PublicStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            public abstract int PublicStaticValueTypeField { get; set; }
        }

        public abstract class InternalStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            public abstract int InternalStaticValueTypeField { get; set; }
        }

        public abstract class ProtectedStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            public abstract int ProtectedStaticValueTypeField { get; set; }
        }

        public abstract class PrivateStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            public abstract int PrivateStaticValueTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            public abstract int PublicReadonlyValueTypeField { get; }
        }

        public abstract class InternalReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            public abstract int InternalReadonlyValueTypeField { get; }
        }

        public abstract class ProtectedReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            public abstract int ProtectedReadonlyValueTypeField { get; }
        }

        public abstract class PrivateReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            public abstract int PrivateReadonlyValueTypeField { get; }
        }

        // *

        public abstract class PublicValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicValueTypeField")]
            public abstract int PublicValueTypeField { get; set; }
        }

        public abstract class InternalValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalValueTypeField")]
            public abstract int InternalValueTypeField { get; set; }
        }

        public abstract class ProtectedValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedValueTypeField")]
            public abstract int ProtectedValueTypeField { get; set; }
        }

        public abstract class PrivateValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateValueTypeField")]
            public abstract int PrivateValueTypeField { get; set; }
        }

        // *

        public abstract class PublicStaticNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            public abstract int? PublicStaticNullableIntField { get; set; }
        }

        public abstract class PrivateStaticNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            public abstract int? PrivateStaticNullableIntField { get; set; }
        }

        public abstract class PublicNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_publicNullableIntField")]
            public abstract int? PublicNullableIntField { get; set; }
        }

        public abstract class PrivateNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_privateNullableIntField")]
            public abstract int? PrivateNullableIntField { get; set; }
        }
    }
}
