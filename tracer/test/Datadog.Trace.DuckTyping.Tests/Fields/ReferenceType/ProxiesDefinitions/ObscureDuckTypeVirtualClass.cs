// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [DuckField(Name = "_publicStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PublicStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string InternalStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string ProtectedStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PrivateStaticReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PublicStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_internalStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string InternalStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string ProtectedStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_privateStaticReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PrivateStaticReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PublicReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string InternalReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string ProtectedReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateReadonlyReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PrivateReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PublicReferenceTypeField { get; set; }

        [DuckField(Name = "_internalReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string InternalReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string ProtectedReferenceTypeField { get; set; }

        [DuckField(Name = "_privateReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual string PrivateReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReferenceTypeField", FallbackToBaseTypes = true)]
        public virtual ValueWithType<string> PublicReferenceTypeFieldWithType { get; set; }
    }
}
