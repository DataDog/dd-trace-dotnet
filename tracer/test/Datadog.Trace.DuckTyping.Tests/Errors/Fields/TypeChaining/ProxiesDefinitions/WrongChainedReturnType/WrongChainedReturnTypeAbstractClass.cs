// <copyright file="WrongChainedReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public abstract class WrongChainedReturnTypeAbstractClass
    {
        public abstract class PublicStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public abstract class InternalStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public abstract class PrivateStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public abstract IInvalidDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public abstract class InternalStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public abstract IInvalidDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public abstract class ProtectedStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public abstract IInvalidDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public abstract class PrivateStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public abstract IInvalidDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public abstract class InternalReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public abstract class PrivateReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public abstract IInvalidDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public abstract IInvalidDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public abstract class InternalSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public abstract IInvalidDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public abstract class ProtectedSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public abstract IInvalidDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public abstract class PrivateSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public abstract IInvalidDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
