// <copyright file="WrongPropertyNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongPropertyName
{
    public class WrongPropertyNameVirtualClass
    {
        public class PublicStaticGetValueTypeVirtualClass
        {
            public virtual int NotPublicStaticGetValueType { get; }
        }

        public class InternalStaticGetValueTypeVirtualClass
        {
            public virtual int NotInternalStaticGetValueType { get; }
        }

        public class ProtectedStaticGetValueTypeVirtualClass
        {
            public virtual int NotProtectedStaticGetValueType { get; }
        }

        public class PrivateStaticGetValueTypeVirtualClass
        {
            public virtual int NotPrivateStaticGetValueType { get; }
        }

        // *

        public class PublicStaticGetSetValueTypeVirtualClass
        {
            public virtual int NotPublicStaticGetSetValueType { get; set; }
        }

        public class InternalStaticGetSetValueTypeVirtualClass
        {
            public virtual int NotInternalStaticGetSetValueType { get; set; }
        }

        public class ProtectedStaticGetSetValueTypeVirtualClass
        {
            public virtual int NotProtectedStaticGetSetValueType { get; set; }
        }

        public class PrivateStaticGetSetValueTypeVirtualClass
        {
            public virtual int NotPrivateStaticGetSetValueType { get; set; }
        }

        // *

        public class PublicGetValueTypeVirtualClass
        {
            public virtual int NotPublicGetValueType { get; }
        }

        public class InternalGetValueTypeVirtualClass
        {
            public virtual int NotInternalGetValueType { get; }
        }

        public class ProtectedGetValueTypeVirtualClass
        {
            public virtual int NotProtectedGetValueType { get; }
        }

        public class PrivateGetValueTypeVirtualClass
        {
            public virtual int NotPrivateGetValueType { get; }
        }

        // *

        public class PublicGetSetValueTypeVirtualClass
        {
            public virtual int NotPublicGetSetValueType { get; set; }
        }

        public class InternalGetSetValueTypeVirtualClass
        {
            public virtual int NotInternalGetSetValueType { get; set; }
        }

        public class ProtectedGetSetValueTypeVirtualClass
        {
            public virtual int NotProtectedGetSetValueType { get; set; }
        }

        public class PrivateGetSetValueTypeVirtualClass
        {
            public virtual int NotPrivateGetSetValueType { get; set; }
        }

        // *

        public class PublicStaticNullableIntVirtualClass
        {
            public virtual int? NotPublicStaticNullableInt { get; set; }
        }

        public class PrivateStaticNullableIntVirtualClass
        {
            public virtual int? NotPrivateStaticNullableInt { get; set; }
        }

        public class PublicNullableIntVirtualClass
        {
            public virtual int? NotPublicNullableInt { get; set; }
        }

        public class PrivateNullableIntVirtualClass
        {
            public virtual int? NotPrivateNullableInt { get; set; }
        }

        // *

        public class StatusVirtualClass
        {
            public virtual TaskStatus NotStatus
            {
                get => default;
                set { }
            }
        }
    }
}
