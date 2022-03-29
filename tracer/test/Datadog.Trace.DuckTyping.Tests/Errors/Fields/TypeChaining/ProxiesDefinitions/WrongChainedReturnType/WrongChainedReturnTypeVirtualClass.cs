// <copyright file="WrongChainedReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public class WrongChainedReturnTypeVirtualClass
    {
        public class PublicStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public class InternalStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public class ProtectedStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public class PrivateStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public class PublicStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public virtual IInvalidDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public class InternalStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public virtual IInvalidDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public class ProtectedStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public virtual IInvalidDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public class PrivateStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public virtual IInvalidDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public class PublicReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public class InternalReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public class ProtectedReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public class PrivateReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public virtual IInvalidDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public class PublicSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public virtual IInvalidDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public class InternalSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public virtual IInvalidDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public class ProtectedSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public virtual IInvalidDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public class PrivateSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public virtual IInvalidDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
