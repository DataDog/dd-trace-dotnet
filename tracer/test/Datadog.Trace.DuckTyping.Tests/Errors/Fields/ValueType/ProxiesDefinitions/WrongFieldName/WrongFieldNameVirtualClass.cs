// <copyright file="WrongFieldNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongFieldName
{
    public class WrongFieldNameVirtualClass
    {
        public class PublicStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "publicStaticReadonlyValueTypeField")]
            public virtual int PublicStaticReadonlyValueTypeField { get; }
        }

        public class InternalStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "internalStaticReadonlyValueTypeField")]
            public virtual int InternalStaticReadonlyValueTypeField { get; }
        }

        public class ProtectedStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedStaticReadonlyValueTypeField")]
            public virtual int ProtectedStaticReadonlyValueTypeField { get; }
        }

        public class PrivateStaticReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "privateStaticReadonlyValueTypeField")]
            public virtual int PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public class PublicStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "publicStaticValueTypeField")]
            public virtual int PublicStaticValueTypeField { get; set; }
        }

        public class InternalStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "internalStaticValueTypeField")]
            public virtual int InternalStaticValueTypeField { get; set; }
        }

        public class ProtectedStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedStaticValueTypeField")]
            public virtual int ProtectedStaticValueTypeField { get; set; }
        }

        public class PrivateStaticValueTypeFieldVirtualClass
        {
            [DuckField(Name = "privateStaticValueTypeField")]
            public virtual int PrivateStaticValueTypeField { get; set; }
        }

        // *

        public class PublicReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "publicReadonlyValueTypeField")]
            public virtual int PublicReadonlyValueTypeField { get; }
        }

        public class InternalReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "internalReadonlyValueTypeField")]
            public virtual int InternalReadonlyValueTypeField { get; }
        }

        public class ProtectedReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedReadonlyValueTypeField")]
            public virtual int ProtectedReadonlyValueTypeField { get; }
        }

        public class PrivateReadonlyValueTypeFieldVirtualClass
        {
            [DuckField(Name = "privateReadonlyValueTypeField")]
            public virtual int PrivateReadonlyValueTypeField { get; }
        }

        // *

        public class PublicValueTypeFieldVirtualClass
        {
            [DuckField(Name = "publicValueTypeField")]
            public virtual int PublicValueTypeField { get; set; }
        }

        public class InternalValueTypeFieldVirtualClass
        {
            [DuckField(Name = "internalValueTypeField")]
            public virtual int InternalValueTypeField { get; set; }
        }

        public class ProtectedValueTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedValueTypeField")]
            public virtual int ProtectedValueTypeField { get; set; }
        }

        public class PrivateValueTypeFieldVirtualClass
        {
            [DuckField(Name = "privateValueTypeField")]
            public virtual int PrivateValueTypeField { get; set; }
        }

        // *

        public class PublicStaticNullableIntFieldVirtualClass
        {
            [DuckField(Name = "publicStaticNullableIntField")]
            public virtual int? PublicStaticNullableIntField { get; set; }
        }

        public class PrivateStaticNullableIntFieldVirtualClass
        {
            [DuckField(Name = "privateStaticNullableIntField")]
            public virtual int? PrivateStaticNullableIntField { get; set; }
        }

        public class PublicNullableIntFieldVirtualClass
        {
            [DuckField(Name = "publicNullableIntField")]
            public virtual int? PublicNullableIntField { get; set; }
        }

        public class PrivateNullableIntFieldVirtualClass
        {
            [DuckField(Name = "privateNullableIntField")]
            public virtual int? PrivateNullableIntField { get; set; }
        }
    }
}
