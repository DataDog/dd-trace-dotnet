// <copyright file="WrongPropertyNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongPropertyName
{
    public abstract class WrongPropertyNameAbstractClass
    {
        public abstract class PublicStaticGetValueTypeAbstractClass
        {
            public abstract int NotPublicStaticGetValueType { get; }
        }

        public abstract class InternalStaticGetValueTypeAbstractClass
        {
            public abstract int NotInternalStaticGetValueType { get; }
        }

        public abstract class ProtectedStaticGetValueTypeAbstractClass
        {
            public abstract int NotProtectedStaticGetValueType { get; }
        }

        public abstract class PrivateStaticGetValueTypeAbstractClass
        {
            public abstract int NotPrivateStaticGetValueType { get; }
        }

        // *
        public abstract class PublicStaticGetSetValueTypeAbstractClass
        {
            public abstract int NotPublicStaticGetSetValueType { get; set; }
        }

        public abstract class InternalStaticGetSetValueTypeAbstractClass
        {
            public abstract int NotInternalStaticGetSetValueType { get; set; }
        }

        public abstract class ProtectedStaticGetSetValueTypeAbstractClass
        {
            public abstract int NotProtectedStaticGetSetValueType { get; set; }
        }

        public abstract class PrivateStaticGetSetValueTypeAbstractClass
        {
            public abstract int NotPrivateStaticGetSetValueType { get; set; }
        }

        // *
        public abstract class PublicGetValueTypeAbstractClass
        {
            public abstract int NotPublicGetValueType { get; }
        }

        public abstract class InternalGetValueTypeAbstractClass
        {
            public abstract int NotInternalGetValueType { get; }
        }

        public abstract class ProtectedGetValueTypeAbstractClass
        {
            public abstract int NotProtectedGetValueType { get; }
        }

        public abstract class PrivateGetValueTypeAbstractClass
        {
            public abstract int NotPrivateGetValueType { get; }
        }

        // *
        public abstract class PublicGetSetValueTypeAbstractClass
        {
            public abstract int NotPublicGetSetValueType { get; set; }
        }

        public abstract class InternalGetSetValueTypeAbstractClass
        {
            public abstract int NotInternalGetSetValueType { get; set; }
        }

        public abstract class ProtectedGetSetValueTypeAbstractClass
        {
            public abstract int NotProtectedGetSetValueType { get; set; }
        }

        public abstract class PrivateGetSetValueTypeAbstractClass
        {
            public abstract int NotPrivateGetSetValueType { get; set; }
        }

        // *
        public abstract class PublicStaticNullableIntAbstractClass
        {
            public abstract int? NotPublicStaticNullableInt { get; set; }
        }

        public abstract class PrivateStaticNullableIntAbstractClass
        {
            public abstract int? NotPrivateStaticNullableInt { get; set; }
        }

        public abstract class PublicNullableIntAbstractClass
        {
            public abstract int? NotPublicNullableInt { get; set; }
        }

        public abstract class PrivateNullableIntAbstractClass
        {
            public abstract int? NotPrivateNullableInt { get; set; }
        }

        // *
        public abstract class StatusAbstractClass
        {
            public abstract TaskStatus NotStatus { get; set; }
        }
    }
}
