// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class PublicStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public class InternalStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public class ProtectedStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public class PrivateStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public virtual IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public class PublicStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public virtual IDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public class InternalStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public virtual IDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public class ProtectedStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public virtual IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public class PrivateStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public virtual IDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public class PublicReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public virtual IDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public class InternalReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public virtual IDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public class ProtectedReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public virtual IDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public class PrivateReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public virtual IDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public class PublicSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public virtual IDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public class InternalSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public virtual IDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public class ProtectedSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public virtual IDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public class PrivateSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public virtual IDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
