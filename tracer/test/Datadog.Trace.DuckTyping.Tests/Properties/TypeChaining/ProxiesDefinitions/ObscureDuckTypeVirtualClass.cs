// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateGetSelfType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PublicGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject InternalGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual IDummyFieldObject PrivateGetSetSelfType { get; set; }

        // *
        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetSelfType")]
        public virtual ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType { get; set; }
    }
}
