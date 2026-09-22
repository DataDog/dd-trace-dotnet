// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [DuckField(Name = "_publicStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PublicStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string InternalStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string ProtectedStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PrivateStaticReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PublicStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_internalStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string InternalStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string ProtectedStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_privateStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PrivateStaticReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PublicReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string InternalReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string ProtectedReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PrivateReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PublicReferenceTypeField { get; set; }

        [DuckField(Name = "_internalReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string InternalReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string ProtectedReferenceTypeField { get; set; }

        [DuckField(Name = "_privateReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract string PrivateReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReferenceTypeField", FallbackToBaseTypes = true)]
        public abstract ValueWithType<string> PublicReferenceTypeFieldWithType { get; set; }
    }
}
