// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        public abstract string PublicStaticGetReferenceType { get; }

        public abstract string InternalStaticGetReferenceType { get; }

        public abstract string ProtectedStaticGetReferenceType { get; }

        public abstract string PrivateStaticGetReferenceType { get; }

        // *

        public abstract string PublicStaticGetSetReferenceType { get; set; }

        public abstract string InternalStaticGetSetReferenceType { get; set; }

        public abstract string ProtectedStaticGetSetReferenceType { get; set; }

        public abstract string PrivateStaticGetSetReferenceType { get; set; }

        // *

        public abstract string PublicGetReferenceType { get; }

        public abstract string InternalGetReferenceType { get; }

        public abstract string ProtectedGetReferenceType { get; }

        public abstract string PrivateGetReferenceType { get; }

        // *

        public abstract string PublicGetSetReferenceType { get; set; }

        public abstract string InternalGetSetReferenceType { get; set; }

        public abstract string ProtectedGetSetReferenceType { get; set; }

        public abstract string PrivateGetSetReferenceType { get; set; }

        // *

        [Duck(Name = "PublicStaticGetSetReferenceType")]
        public abstract ValueWithType<string> PublicStaticOnlyGetWithType { get; }

        // *

        public abstract string this[string index] { get; set; }
    }
}
