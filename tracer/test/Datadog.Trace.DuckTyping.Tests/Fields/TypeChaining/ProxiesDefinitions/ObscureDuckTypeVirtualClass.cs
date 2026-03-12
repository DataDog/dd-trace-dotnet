// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.TypeChaining.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [DuckField(Name = "_publicStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicStaticSelfTypeField { get; set; }

        [DuckField(Name = "_internalStaticSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalStaticSelfTypeField { get; set; }

        [DuckField(Name = "_protectedStaticSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }

        [DuckField(Name = "_privateStaticSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateStaticSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicReadonlySelfTypeField { get; }

        [DuckField(Name = "_internalReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalReadonlySelfTypeField { get; }

        [DuckField(Name = "_protectedReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedReadonlySelfTypeField { get; }

        [DuckField(Name = "_privateReadonlySelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateReadonlySelfTypeField { get; }

        // *

        [DuckField(Name = "_publicSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicSelfTypeField { get; set; }

        [DuckField(Name = "_internalSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalSelfTypeField { get; set; }

        [DuckField(Name = "_protectedSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedSelfTypeField { get; set; }

        [DuckField(Name = "_privateSelfTypeField", FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateSelfTypeField { get; set; }

        // *

        [DuckField(Name = "_publicSelfTypeField", FallbackToBaseTypes = true)]
        public virtual ValueWithType<IDummyFieldObject> PublicSelfTypeFieldWithType { get; set; }
    }
}
