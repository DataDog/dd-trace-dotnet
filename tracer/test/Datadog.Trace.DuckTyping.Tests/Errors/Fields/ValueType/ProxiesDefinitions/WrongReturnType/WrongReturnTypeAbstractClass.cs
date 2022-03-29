// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class PublicStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            public abstract char PublicStaticReadonlyValueTypeField { get; }
        }

        public abstract class InternalStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            public abstract char InternalStaticReadonlyValueTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            public abstract char ProtectedStaticReadonlyValueTypeField { get; }
        }

        public abstract class PrivateStaticReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            public abstract char PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public abstract class PublicStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            public abstract char PublicStaticValueTypeField { get; set; }
        }

        public abstract class InternalStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            public abstract char InternalStaticValueTypeField { get; set; }
        }

        public abstract class ProtectedStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            public abstract char ProtectedStaticValueTypeField { get; set; }
        }

        public abstract class PrivateStaticValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            public abstract char PrivateStaticValueTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            public abstract char PublicReadonlyValueTypeField { get; }
        }

        public abstract class InternalReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            public abstract char InternalReadonlyValueTypeField { get; }
        }

        public abstract class ProtectedReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            public abstract char ProtectedReadonlyValueTypeField { get; }
        }

        public abstract class PrivateReadonlyValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            public abstract char PrivateReadonlyValueTypeField { get; }
        }

        // *

        public abstract class PublicValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicValueTypeField")]
            public abstract char PublicValueTypeField { get; set; }
        }

        public abstract class InternalValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalValueTypeField")]
            public abstract char InternalValueTypeField { get; set; }
        }

        public abstract class ProtectedValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedValueTypeField")]
            public abstract char ProtectedValueTypeField { get; set; }
        }

        public abstract class PrivateValueTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateValueTypeField")]
            public abstract char PrivateValueTypeField { get; set; }
        }

        // *

        public abstract class PublicStaticNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            public abstract char? PublicStaticNullableIntField { get; set; }
        }

        public abstract class PrivateStaticNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            public abstract char? PrivateStaticNullableIntField { get; set; }
        }

        public abstract class PublicNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_publicNullableIntField")]
            public abstract char? PublicNullableIntField { get; set; }
        }

        public abstract class PrivateNullableIntFieldAbstractClass
        {
            [DuckField(Name = "_privateNullableIntField")]
            public abstract char? PrivateNullableIntField { get; set; }
        }
    }
}
