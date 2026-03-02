// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [Duck(FallbackToBaseTypes = true)]
        public virtual string PublicStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string InternalStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string ProtectedStaticGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PrivateStaticGetReferenceType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PublicStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string InternalStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string ProtectedStaticGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PrivateStaticGetSetReferenceType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PublicGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string InternalGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string ProtectedGetReferenceType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PrivateGetReferenceType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PublicGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string InternalGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string ProtectedGetSetReferenceType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual string PrivateGetSetReferenceType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true, Name = "PublicStaticGetSetReferenceType")]
        public virtual ValueWithType<string> PublicStaticOnlyGetWithType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual string this[string index]
        {
            get => default;
            set { }
        }
    }
}
