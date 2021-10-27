// <copyright file="ValidAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.Valid
{
    public abstract class ValidAbstractClass
    {
        public abstract class PublicStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public abstract class InternalStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public abstract class PrivateStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public abstract IDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public abstract class InternalStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public abstract IDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public abstract class ProtectedStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public abstract IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public abstract class PrivateStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public abstract IDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public abstract IDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public abstract class InternalReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public abstract IDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public abstract IDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public abstract class PrivateReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public abstract IDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public abstract IDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public abstract class InternalSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public abstract IDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public abstract class ProtectedSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public abstract IDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public abstract class PrivateSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public abstract IDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
