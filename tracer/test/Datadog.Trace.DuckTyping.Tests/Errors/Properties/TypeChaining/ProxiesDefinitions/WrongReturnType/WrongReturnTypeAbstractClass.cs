// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class PublicStaticGetSelfTypeAbstractClass
        {
            public abstract string PublicStaticGetSelfType { get; }
        }

        public abstract class InternalStaticGetSelfTypeAbstractClass
        {
            public abstract string InternalStaticGetSelfType { get; }
        }

        public abstract class ProtectedStaticGetSelfTypeAbstractClass
        {
            public abstract string ProtectedStaticGetSelfType { get; }
        }

        public abstract class PrivateStaticGetSelfTypeAbstractClass
        {
            public abstract string PrivateStaticGetSelfType { get; }
        }

        // *

        public abstract class PublicStaticGetSetSelfTypeAbstractClass
        {
            public abstract string PublicStaticGetSetSelfType { get; set; }
        }

        public abstract class InternalStaticGetSetSelfTypeAbstractClass
        {
            public abstract string InternalStaticGetSetSelfType { get; set; }
        }

        public abstract class ProtectedStaticGetSetSelfTypeAbstractClass
        {
            public abstract string ProtectedStaticGetSetSelfType { get; set; }
        }

        public abstract class PrivateStaticGetSetSelfTypeAbstractClass
        {
            public abstract string PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public abstract class PublicGetSelfTypeAbstractClass
        {
            public abstract string PublicGetSelfType { get; }
        }

        public abstract class InternalGetSelfTypeAbstractClass
        {
            public abstract string InternalGetSelfType { get; }
        }

        public abstract class ProtectedGetSelfTypeAbstractClass
        {
            public abstract string ProtectedGetSelfType { get; }
        }

        public abstract class PrivateGetSelfTypeAbstractClass
        {
            public abstract string PrivateGetSelfType { get; }
        }

        // *

        public abstract class PublicGetSetSelfTypeAbstractClass
        {
            public abstract string PublicGetSetSelfType { get; set; }
        }

        public abstract class InternalGetSetSelfTypeAbstractClass
        {
            public abstract string InternalGetSetSelfType { get; set; }
        }

        public abstract class ProtectedGetSetSelfTypeAbstractClass
        {
            public abstract string ProtectedGetSetSelfType { get; set; }
        }

        public abstract class PrivateGetSetSelfTypeAbstractClass
        {
            public abstract string PrivateGetSetSelfType { get; set; }
        }
    }
}
