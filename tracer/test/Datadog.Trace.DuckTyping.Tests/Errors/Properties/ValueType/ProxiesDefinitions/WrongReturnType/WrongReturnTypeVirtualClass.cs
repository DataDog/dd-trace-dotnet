// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class PublicStaticGetValueTypeVirtualClass
        {
            public virtual char PublicStaticGetValueType { get; }
        }

        public class InternalStaticGetValueTypeVirtualClass
        {
            public virtual char InternalStaticGetValueType { get; }
        }

        public class ProtectedStaticGetValueTypeVirtualClass
        {
            public virtual char ProtectedStaticGetValueType { get; }
        }

        public class PrivateStaticGetValueTypeVirtualClass
        {
            public virtual char PrivateStaticGetValueType { get; }
        }

        // *

        public class PublicStaticGetSetValueTypeVirtualClass
        {
            public virtual char PublicStaticGetSetValueType { get; set; }
        }

        public class InternalStaticGetSetValueTypeVirtualClass
        {
            public virtual char InternalStaticGetSetValueType { get; set; }
        }

        public class ProtectedStaticGetSetValueTypeVirtualClass
        {
            public virtual char ProtectedStaticGetSetValueType { get; set; }
        }

        public class PrivateStaticGetSetValueTypeVirtualClass
        {
            public virtual char PrivateStaticGetSetValueType { get; set; }
        }

        // *

        public class PublicGetValueTypeVirtualClass
        {
            public virtual char PublicGetValueType { get; }
        }

        public class InternalGetValueTypeVirtualClass
        {
            public virtual char InternalGetValueType { get; }
        }

        public class ProtectedGetValueTypeVirtualClass
        {
            public virtual char ProtectedGetValueType { get; }
        }

        public class PrivateGetValueTypeVirtualClass
        {
            public virtual char PrivateGetValueType { get; }
        }

        // *

        public class PublicGetSetValueTypeVirtualClass
        {
            public virtual char PublicGetSetValueType { get; set; }
        }

        public class InternalGetSetValueTypeVirtualClass
        {
            public virtual char InternalGetSetValueType { get; set; }
        }

        public class ProtectedGetSetValueTypeVirtualClass
        {
            public virtual char ProtectedGetSetValueType { get; set; }
        }

        public class PrivateGetSetValueTypeVirtualClass
        {
            public virtual char PrivateGetSetValueType { get; set; }
        }

        // *

        public class PublicStaticNullableIntVirtualClass
        {
            public virtual char? PublicStaticNullablechar { get; set; }
        }

        public class PrivateStaticNullableIntVirtualClass
        {
            public virtual char? PrivateStaticNullablechar { get; set; }
        }

        public class PublicNullableIntVirtualClass
        {
            public virtual char? PublicNullablechar { get; set; }
        }

        public class PrivateNullableIntVirtualClass
        {
            public virtual char? PrivateNullablechar { get; set; }
        }

        // *

        public class StatusVirtualClass
        {
            public virtual char Status
            {
                get => default;
                set { }
            }
        }

        // *

        public class IndexVirtualClass
        {
            public virtual char this[int index]
            {
                get => default;
                set { }
            }
        }
    }
}
