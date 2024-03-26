// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.TypeChaining.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
        public virtual IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
        public virtual IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
        public virtual IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
        public virtual IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticSelfTypeField")]
        public virtual IDummyFieldObject PublicStaticSelfTypeField { get; set; }

        [DuckField(Name = "_internalStaticSelfTypeField")]
        public virtual IDummyFieldObject InternalStaticSelfTypeField { get; set; }

        [DuckField(Name = "_protectedStaticSelfTypeField")]
        public virtual IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }

        [DuckField(Name = "_privateStaticSelfTypeField")]
        public virtual IDummyFieldObject PrivateStaticSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlySelfTypeField")]
        public virtual IDummyFieldObject PublicReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalReadonlySelfTypeField")]
        public virtual IDummyFieldObject InternalReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedReadonlySelfTypeField")]
        public virtual IDummyFieldObject ProtectedReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateReadonlySelfTypeField")]
        public virtual IDummyFieldObject PrivateReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicSelfTypeField")]
        public virtual IDummyFieldObject PublicSelfTypeField { get; set; }

        [DuckField(Name = "_internalSelfTypeField")]
        public virtual IDummyFieldObject InternalSelfTypeField { get; set; }

        [DuckField(Name = "_protectedSelfTypeField")]
        public virtual IDummyFieldObject ProtectedSelfTypeField { get; set; }

        [DuckField(Name = "_privateSelfTypeField")]
        public virtual IDummyFieldObject PrivateSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicSelfTypeField")]
        public virtual ValueWithType<IDummyFieldObject> PublicSelfTypeFieldWithType { get; set; }
    }
}
