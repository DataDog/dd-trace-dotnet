// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
        public abstract string PublicStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
        public abstract string InternalStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
        public abstract string ProtectedStaticReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
        public abstract string PrivateStaticReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicStaticReferenceTypeField")]
        public abstract string PublicStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_internalStaticReferenceTypeField")]
        public abstract string InternalStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedStaticReferenceTypeField")]
        public abstract string ProtectedStaticReferenceTypeField { get; set; }

        [DuckField(Name = "_privateStaticReferenceTypeField")]
        public abstract string PrivateStaticReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReadonlyReferenceTypeField")]
        public abstract string PublicReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_internalReadonlyReferenceTypeField")]
        public abstract string InternalReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
        public abstract string ProtectedReadonlyReferenceTypeField { get; }

        [DuckField(Name = "_privateReadonlyReferenceTypeField")]
        public abstract string PrivateReadonlyReferenceTypeField { get; }

        // *

        [DuckField(Name = "_publicReferenceTypeField")]
        public abstract string PublicReferenceTypeField { get; set; }

        [DuckField(Name = "_internalReferenceTypeField")]
        public abstract string InternalReferenceTypeField { get; set; }

        [DuckField(Name = "_protectedReferenceTypeField")]
        public abstract string ProtectedReferenceTypeField { get; set; }

        [DuckField(Name = "_privateReferenceTypeField")]
        public abstract string PrivateReferenceTypeField { get; set; }

        // *

        [DuckField(Name = "_publicReferenceTypeField")]
        public abstract ValueWithType<string> PublicReferenceTypeFieldWithType { get; set; }
    }
}
