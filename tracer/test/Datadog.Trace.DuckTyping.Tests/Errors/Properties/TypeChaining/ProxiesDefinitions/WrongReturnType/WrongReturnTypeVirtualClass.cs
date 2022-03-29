// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class PublicStaticGetSelfTypeVirtualClass
        {
            public virtual string PublicStaticGetSelfType { get; }
        }

        public class InternalStaticGetSelfTypeVirtualClass
        {
            public virtual string InternalStaticGetSelfType { get; }
        }

        public class ProtectedStaticGetSelfTypeVirtualClass
        {
            public virtual string ProtectedStaticGetSelfType { get; }
        }

        public class PrivateStaticGetSelfTypeVirtualClass
        {
            public virtual string PrivateStaticGetSelfType { get; }
        }

        // *

        public class PublicStaticGetSetSelfTypeVirtualClass
        {
            public virtual string PublicStaticGetSetSelfType { get; set; }
        }

        public class InternalStaticGetSetSelfTypeVirtualClass
        {
            public virtual string InternalStaticGetSetSelfType { get; set; }
        }

        public class ProtectedStaticGetSetSelfTypeVirtualClass
        {
            public virtual string ProtectedStaticGetSetSelfType { get; set; }
        }

        public class PrivateStaticGetSetSelfTypeVirtualClass
        {
            public virtual string PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public class PublicGetSelfTypeVirtualClass
        {
            public virtual string PublicGetSelfType { get; }
        }

        public class InternalGetSelfTypeVirtualClass
        {
            public virtual string InternalGetSelfType { get; }
        }

        public class ProtectedGetSelfTypeVirtualClass
        {
            public virtual string ProtectedGetSelfType { get; }
        }

        public class PrivateGetSelfTypeVirtualClass
        {
            public virtual string PrivateGetSelfType { get; }
        }

        // *

        public class PublicGetSetSelfTypeVirtualClass
        {
            public virtual string PublicGetSetSelfType { get; set; }
        }

        public class InternalGetSetSelfTypeVirtualClass
        {
            public virtual string InternalGetSetSelfType { get; set; }
        }

        public class ProtectedGetSetSelfTypeVirtualClass
        {
            public virtual string ProtectedGetSetSelfType { get; set; }
        }

        public class PrivateGetSetSelfTypeVirtualClass
        {
            public virtual string PrivateGetSetSelfType { get; set; }
        }
    }
}
