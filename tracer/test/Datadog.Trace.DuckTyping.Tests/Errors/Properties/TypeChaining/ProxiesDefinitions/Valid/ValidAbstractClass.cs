// <copyright file="ValidAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.Valid
{
    public abstract class ValidAbstractClass
    {
        public abstract class PublicStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PublicStaticGetSelfType { get; }
        }

        public abstract class InternalStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject InternalStaticGetSelfType { get; }
        }

        public abstract class ProtectedStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject ProtectedStaticGetSelfType { get; }
        }

        public abstract class PrivateStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PrivateStaticGetSelfType { get; }
        }

        // *

        public abstract class PublicStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PublicStaticGetSetSelfType { get; set; }
        }

        public abstract class InternalStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject InternalStaticGetSetSelfType { get; set; }
        }

        public abstract class ProtectedStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }
        }

        public abstract class PrivateStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public abstract class PublicGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PublicGetSelfType { get; }
        }

        public abstract class InternalGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject InternalGetSelfType { get; }
        }

        public abstract class ProtectedGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject ProtectedGetSelfType { get; }
        }

        public abstract class PrivateGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PrivateGetSelfType { get; }
        }

        // *

        public abstract class PublicGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PublicGetSetSelfType { get; set; }
        }

        public abstract class InternalGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject InternalGetSetSelfType { get; set; }
        }

        public abstract class ProtectedGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject ProtectedGetSetSelfType { get; set; }
        }

        public abstract class PrivateGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject PrivateGetSetSelfType { get; set; }
        }
    }
}
