// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class PublicStaticGetValueTypeVirtualClass
        {
            public virtual int PublicStaticGetValueType { get; }
        }

        public class InternalStaticGetValueTypeVirtualClass
        {
            public virtual int InternalStaticGetValueType { get; }
        }

        public class ProtectedStaticGetValueTypeVirtualClass
        {
            public virtual int ProtectedStaticGetValueType { get; }
        }

        public class PrivateStaticGetValueTypeVirtualClass
        {
            public virtual int PrivateStaticGetValueType { get; }
        }

        // *

        public class PublicStaticGetSetValueTypeVirtualClass
        {
            public virtual int PublicStaticGetSetValueType { get; set; }
        }

        public class InternalStaticGetSetValueTypeVirtualClass
        {
            public virtual int InternalStaticGetSetValueType { get; set; }
        }

        public class ProtectedStaticGetSetValueTypeVirtualClass
        {
            public virtual int ProtectedStaticGetSetValueType { get; set; }
        }

        public class PrivateStaticGetSetValueTypeVirtualClass
        {
            public virtual int PrivateStaticGetSetValueType { get; set; }
        }

        // *

        public class PublicGetValueTypeVirtualClass
        {
            public virtual int PublicGetValueType { get; }
        }

        public class InternalGetValueTypeVirtualClass
        {
            public virtual int InternalGetValueType { get; }
        }

        public class ProtectedGetValueTypeVirtualClass
        {
            public virtual int ProtectedGetValueType { get; }
        }

        public class PrivateGetValueTypeVirtualClass
        {
            public virtual int PrivateGetValueType { get; }
        }

        // *

        public class PublicGetSetValueTypeVirtualClass
        {
            public virtual int PublicGetSetValueType { get; set; }
        }

        public class InternalGetSetValueTypeVirtualClass
        {
            public virtual int InternalGetSetValueType { get; set; }
        }

        public class ProtectedGetSetValueTypeVirtualClass
        {
            public virtual int ProtectedGetSetValueType { get; set; }
        }

        public class PrivateGetSetValueTypeVirtualClass
        {
            public virtual int PrivateGetSetValueType { get; set; }
        }

        // *

        public class PublicStaticNullableIntVirtualClass
        {
            public virtual int? PublicStaticNullableInt { get; set; }
        }

        public class PrivateStaticNullableIntVirtualClass
        {
            public virtual int? PrivateStaticNullableInt { get; set; }
        }

        public class PublicNullableIntVirtualClass
        {
            public virtual int? PublicNullableInt { get; set; }
        }

        public class PrivateNullableIntVirtualClass
        {
            public virtual int? PrivateNullableInt { get; set; }
        }

        // *

        public class StatusVirtualClass
        {
            public virtual TaskStatus Status
            {
                get => default;
                set { }
            }
        }

        // *

        public class IndexVirtualClass
        {
            public virtual int this[int index]
            {
                get => default;
                set { }
            }
        }
    }
}
