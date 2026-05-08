// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [Duck(FallbackToBaseTypes = true)]
        public abstract string PublicStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string InternalStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string ProtectedStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PrivateStaticGetReferenceType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PublicStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string InternalStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string ProtectedStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PrivateStaticGetSetReferenceType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PublicGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string InternalGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string ProtectedGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PrivateGetReferenceType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PublicGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string InternalGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string ProtectedGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract string PrivateGetSetReferenceType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        public abstract ValueWithType<string> PublicStaticOnlyGetWithType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract string this[string index] { get; set; }
    }
}
