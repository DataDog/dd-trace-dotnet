// <copyright file="WrongFieldNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongFieldName
{
    public class WrongFieldNameVirtualClass
    {
        public class PublicStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "publicStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public class InternalStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "internalStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public class ProtectedStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public class PrivateStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "privateStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public class PublicStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "publicStaticSelfTypeField")]
            public virtual IDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public class InternalStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "internalStaticSelfTypeField")]
            public virtual IDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public class ProtectedStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedStaticSelfTypeField")]
            public virtual IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public class PrivateStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "privateStaticSelfTypeField")]
            public virtual IDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public class PublicReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "publicReadonlySelfTypeField")]
            public virtual IDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public class InternalReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "internalReadonlySelfTypeField")]
            public virtual IDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public class ProtectedReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedReadonlySelfTypeField")]
            public virtual IDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public class PrivateReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "privateReadonlySelfTypeField")]
            public virtual IDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public class PublicSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "publicSelfTypeField")]
            public virtual IDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public class InternalSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "internalSelfTypeField")]
            public virtual IDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public class ProtectedSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedSelfTypeField")]
            public virtual IDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public class PrivateSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "privateSelfTypeField")]
            public virtual IDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
