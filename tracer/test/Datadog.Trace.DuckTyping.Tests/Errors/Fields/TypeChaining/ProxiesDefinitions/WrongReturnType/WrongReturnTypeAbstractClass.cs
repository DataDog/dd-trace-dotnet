// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class PublicStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public abstract int PublicStaticReadonlySelfTypeField { get; }
        }

        public abstract class InternalStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public abstract int InternalStaticReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public abstract int ProtectedStaticReadonlySelfTypeField { get; }
        }

        public abstract class PrivateStaticReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public abstract int PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public abstract int PublicStaticSelfTypeField { get; set; }
        }

        public abstract class InternalStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public abstract int InternalStaticSelfTypeField { get; set; }
        }

        public abstract class ProtectedStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public abstract int ProtectedStaticSelfTypeField { get; set; }
        }

        public abstract class PrivateStaticSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public abstract int PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public abstract int PublicReadonlySelfTypeField { get; }
        }

        public abstract class InternalReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public abstract int InternalReadonlySelfTypeField { get; }
        }

        public abstract class ProtectedReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public abstract int ProtectedReadonlySelfTypeField { get; }
        }

        public abstract class PrivateReadonlySelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public abstract int PrivateReadonlySelfTypeField { get; }
        }

        // *

        public abstract class PublicSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public abstract int PublicSelfTypeField { get; set; }
        }

        public abstract class InternalSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public abstract int InternalSelfTypeField { get; set; }
        }

        public abstract class ProtectedSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public abstract int ProtectedSelfTypeField { get; set; }
        }

        public abstract class PrivateSelfTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public abstract int PrivateSelfTypeField { get; set; }
        }
    }
}
