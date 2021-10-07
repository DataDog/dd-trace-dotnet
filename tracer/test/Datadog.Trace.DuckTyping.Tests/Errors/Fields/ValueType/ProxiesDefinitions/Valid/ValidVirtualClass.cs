// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class PublicStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            public virtual int PublicStaticReadonlyValueTypeField { get; }
        }

        public class InternalStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            public virtual int InternalStaticReadonlyValueTypeField { get; }
        }

        public class ProtectedStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            public virtual int ProtectedStaticReadonlyValueTypeField { get; }
        }

        public class PrivateStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            public virtual int PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public class PublicStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            public virtual int PublicStaticValueTypeField { get; set; }
        }

        public class InternalStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            public virtual int InternalStaticValueTypeField { get; set; }
        }

        public class ProtectedStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            public virtual int ProtectedStaticValueTypeField { get; set; }
        }

        public class PrivateStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            public virtual int PrivateStaticValueTypeField { get; set; }
        }

        // *

        public class PublicReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            public virtual int PublicReadonlyValueTypeField { get; }
        }

        public class InternalReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            public virtual int InternalReadonlyValueTypeField { get; }
        }

        public class ProtectedReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            public virtual int ProtectedReadonlyValueTypeField { get; }
        }

        public class PrivateReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            public virtual int PrivateReadonlyValueTypeField { get; }
        }

        // *

        public class PublicValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicValueTypeField")]
            public virtual int PublicValueTypeField { get; set; }
        }

        public class InternalValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalValueTypeField")]
            public virtual int InternalValueTypeField { get; set; }
        }

        public class ProtectedValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedValueTypeField")]
            public virtual int ProtectedValueTypeField { get; set; }
        }

        public class PrivateValueTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateValueTypeField")]
            public virtual int PrivateValueTypeField { get; set; }
        }

        // *

        public class PublicStaticNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            public virtual int? PublicStaticNullableIntField { get; set; }
        }

        public class PrivateStaticNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            public virtual int? PrivateStaticNullableIntField { get; set; }
        }

        public class PublicNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_publicNullableIntField")]
            public virtual int? PublicNullableIntField { get; set; }
        }

        public class PrivateNullableIntFieldVirtualClass
        {
            [DuckField(Name = "_privateNullableIntField")]
            public virtual int? PrivateNullableIntField { get; set; }
        }
    }
}
