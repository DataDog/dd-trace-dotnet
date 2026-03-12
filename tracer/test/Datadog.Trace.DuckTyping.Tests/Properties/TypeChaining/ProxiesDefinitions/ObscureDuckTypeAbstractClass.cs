// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PublicStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject InternalStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject ProtectedStaticGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PublicGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject InternalGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject ProtectedGetSelfType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PrivateGetSelfType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PublicGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject InternalGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract IDummyFieldObject PrivateGetSetSelfType { get; set; }

        // *
        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetSelfType")]
        public abstract ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType { get; set; }
    }
}
