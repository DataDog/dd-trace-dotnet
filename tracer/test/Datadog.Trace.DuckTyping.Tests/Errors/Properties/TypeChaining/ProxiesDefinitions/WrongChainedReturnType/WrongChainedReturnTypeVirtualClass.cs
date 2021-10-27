// <copyright file="WrongChainedReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public class WrongChainedReturnTypeVirtualClass
    {
        public class PublicStaticGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PublicStaticGetSelfType { get; }
        }

        public class InternalStaticGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject InternalStaticGetSelfType { get; }
        }

        public class ProtectedStaticGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject ProtectedStaticGetSelfType { get; }
        }

        public class PrivateStaticGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PrivateStaticGetSelfType { get; }
        }

        // *

        public class PublicStaticGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PublicStaticGetSetSelfType { get; set; }
        }

        public class InternalStaticGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject InternalStaticGetSetSelfType { get; set; }
        }

        public class ProtectedStaticGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject ProtectedStaticGetSetSelfType { get; set; }
        }

        public class PrivateStaticGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public class PublicGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PublicGetSelfType { get; }
        }

        public class InternalGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject InternalGetSelfType { get; }
        }

        public class ProtectedGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject ProtectedGetSelfType { get; }
        }

        public class PrivateGetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PrivateGetSelfType { get; }
        }

        // *

        public class PublicGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PublicGetSetSelfType { get; set; }
        }

        public class InternalGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject InternalGetSetSelfType { get; set; }
        }

        public class ProtectedGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject ProtectedGetSetSelfType { get; set; }
        }

        public class PrivateGetSetSelfTypeVirtualClass
        {
            public virtual IWrongFieldObject PrivateGetSetSelfType { get; set; }
        }
    }
}
