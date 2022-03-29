// <copyright file="WrongFieldNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongFieldName
{
    public abstract class WrongFieldNameAbstractClass
    {
        public abstract class PublicStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "publicStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public abstract class InternalStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "internalStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public abstract class PrivateStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "privateStaticReadonlySelfTypeField")]
            public abstract IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "publicStaticSelfTypeField")]
            public abstract IDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public abstract class InternalStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "internalStaticSelfTypeField")]
            public abstract IDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public abstract class ProtectedStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedStaticSelfTypeField")]
            public abstract IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public abstract class PrivateStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "privateStaticSelfTypeField")]
            public abstract IDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "publicReadonlySelfTypeField")]
            public abstract IDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public abstract class InternalReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "internalReadonlySelfTypeField")]
            public abstract IDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedReadonlySelfTypeField")]
            public abstract IDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public abstract class PrivateReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "privateReadonlySelfTypeField")]
            public abstract IDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "publicSelfTypeField")]
            public abstract IDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public abstract class InternalSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "internalSelfTypeField")]
            public abstract IDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public abstract class ProtectedSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedSelfTypeField")]
            public abstract IDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public abstract class PrivateSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "privateSelfTypeField")]
            public abstract IDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
