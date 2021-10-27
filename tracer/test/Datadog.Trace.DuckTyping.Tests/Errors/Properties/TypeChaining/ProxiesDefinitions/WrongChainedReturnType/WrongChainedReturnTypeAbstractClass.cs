// <copyright file="WrongChainedReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public abstract class WrongChainedReturnTypeAbstractClass
    {
        public abstract class PublicStaticGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PublicStaticGetSelfType { get; }
        }

        public abstract class InternalStaticGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject InternalStaticGetSelfType { get; }
        }

        public abstract class ProtectedStaticGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject ProtectedStaticGetSelfType { get; }
        }

        public abstract class PrivateStaticGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PrivateStaticGetSelfType { get; }
        }

        // *

        public abstract class PublicStaticGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PublicStaticGetSetSelfType { get; set; }
        }

        public abstract class InternalStaticGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject InternalStaticGetSetSelfType { get; set; }
        }

        public abstract class ProtectedStaticGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject ProtectedStaticGetSetSelfType { get; set; }
        }

        public abstract class PrivateStaticGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public abstract class PublicGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PublicGetSelfType { get; }
        }

        public abstract class InternalGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject InternalGetSelfType { get; }
        }

        public abstract class ProtectedGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject ProtectedGetSelfType { get; }
        }

        public abstract class PrivateGetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PrivateGetSelfType { get; }
        }

        // *

        public abstract class PublicGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PublicGetSetSelfType { get; set; }
        }

        public abstract class InternalGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject InternalGetSetSelfType { get; set; }
        }

        public abstract class ProtectedGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject ProtectedGetSetSelfType { get; set; }
        }

        public abstract class PrivateGetSetSelfTypeAbstractClass
        {
            public abstract IWrongFieldObject PrivateGetSetSelfType { get; set; }
        }
    }
}
