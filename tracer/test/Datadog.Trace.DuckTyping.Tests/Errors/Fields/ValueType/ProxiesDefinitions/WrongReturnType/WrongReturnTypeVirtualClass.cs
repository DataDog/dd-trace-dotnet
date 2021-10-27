// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class PublicStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            public virtual char PublicStaticReadonlyValueTypeField { get; }
        }

        public class InternalStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            public virtual char InternalStaticReadonlyValueTypeField { get; }
        }

        public class ProtectedStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            public virtual char ProtectedStaticReadonlyValueTypeField { get; }
        }

        public class PrivateStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            public virtual char PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public class PublicStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            public virtual char PublicStaticValueTypeField { get; set; }
        }

        public class InternalStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            public virtual char InternalStaticValueTypeField { get; set; }
        }

        public class ProtectedStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            public virtual char ProtectedStaticValueTypeField { get; set; }
        }

        public class PrivateStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            public virtual char PrivateStaticValueTypeField { get; set; }
        }

        // *

        public class PublicReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            public virtual char PublicReadonlyValueTypeField { get; }
        }

        public class InternalReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            public virtual char InternalReadonlyValueTypeField { get; }
        }

        public class ProtectedReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            public virtual char ProtectedReadonlyValueTypeField { get; }
        }

        public class PrivateReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            public virtual char PrivateReadonlyValueTypeField { get; }
        }

        // *

        public class PublicValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicValueTypeField")]
            public virtual char PublicValueTypeField { get; set; }
        }

        public class InternalValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalValueTypeField")]
            public virtual char InternalValueTypeField { get; set; }
        }

        public class ProtectedValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedValueTypeField")]
            public virtual char ProtectedValueTypeField { get; set; }
        }

        public class PrivateValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateValueTypeField")]
            public virtual char PrivateValueTypeField { get; set; }
        }

        // *

        public class PublicStaticNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            public virtual char? PublicStaticNullableIntField { get; set; }
        }

        public class PrivateStaticNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            public virtual char? PrivateStaticNullableIntField { get; set; }
        }

        public class PublicNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_publicNullableIntField")]
            public virtual char? PublicNullableIntField { get; set; }
        }

        public class PrivateNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_privateNullableIntField")]
            public virtual char? PrivateNullableIntField { get; set; }
        }
    }
}
