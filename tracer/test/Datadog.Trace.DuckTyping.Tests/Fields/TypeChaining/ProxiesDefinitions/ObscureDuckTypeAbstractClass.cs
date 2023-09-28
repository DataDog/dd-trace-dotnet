// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.TypeChaining.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
        public abstract IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
        public abstract IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
        public abstract IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
        public abstract IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticSelfTypeField")]
        public abstract IDummyFieldObject PublicStaticSelfTypeField { get; set; }

        [DuckField(Name = "_internalStaticSelfTypeField")]
        public abstract IDummyFieldObject InternalStaticSelfTypeField { get; set; }

        [DuckField(Name = "_protectedStaticSelfTypeField")]
        public abstract IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }

        [DuckField(Name = "_privateStaticSelfTypeField")]
        public abstract IDummyFieldObject PrivateStaticSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlySelfTypeField")]
        public abstract IDummyFieldObject PublicReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalReadonlySelfTypeField")]
        public abstract IDummyFieldObject InternalReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedReadonlySelfTypeField")]
        public abstract IDummyFieldObject ProtectedReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateReadonlySelfTypeField")]
        public abstract IDummyFieldObject PrivateReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicSelfTypeField")]
        public abstract IDummyFieldObject PublicSelfTypeField { get; set; }

        [DuckField(Name = "_internalSelfTypeField")]
        public abstract IDummyFieldObject InternalSelfTypeField { get; set; }

        [DuckField(Name = "_protectedSelfTypeField")]
        public abstract IDummyFieldObject ProtectedSelfTypeField { get; set; }

        [DuckField(Name = "_privateSelfTypeField")]
        public abstract IDummyFieldObject PrivateSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicSelfTypeField")]
        public abstract ValueWithType<IDummyFieldObject> PublicSelfTypeFieldWithType { get; set; }
    }
}
