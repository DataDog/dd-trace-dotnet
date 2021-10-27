// <copyright file="WrongPropertyNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongPropertyName
{
    public abstract class WrongPropertyNameAbstractClass
    {
        public abstract class PublicStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPublicStaticGetSelfType { get; }
        }

        public abstract class InternalStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotInternalStaticGetSelfType { get; }
        }

        public abstract class ProtectedStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotProtectedStaticGetSelfType { get; }
        }

        public abstract class PrivateStaticGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPrivateStaticGetSelfType { get; }
        }

        // *

        public abstract class PublicStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPublicStaticGetSetSelfType { get; set; }
        }

        public abstract class InternalStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotInternalStaticGetSetSelfType { get; set; }
        }

        public abstract class ProtectedStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotProtectedStaticGetSetSelfType { get; set; }
        }

        public abstract class PrivateStaticGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public abstract class PublicGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPublicGetSelfType { get; }
        }

        public abstract class InternalGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotInternalGetSelfType { get; }
        }

        public abstract class ProtectedGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotProtectedGetSelfType { get; }
        }

        public abstract class PrivateGetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPrivateGetSelfType { get; }
        }

        // *

        public abstract class PublicGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPublicGetSetSelfType { get; set; }
        }

        public abstract class InternalGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotInternalGetSetSelfType { get; set; }
        }

        public abstract class ProtectedGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotProtectedGetSetSelfType { get; set; }
        }

        public abstract class PrivateGetSetSelfTypeAbstractClass
        {
            public abstract IDummyFieldObject NotPrivateGetSetSelfType { get; set; }
        }
    }
}
