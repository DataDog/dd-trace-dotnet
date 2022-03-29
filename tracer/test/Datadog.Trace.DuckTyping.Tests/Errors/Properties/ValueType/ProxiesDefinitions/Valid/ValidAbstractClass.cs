// <copyright file="ValidAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.Valid
{
    public abstract class ValidAbstractClass
    {
        public abstract class PublicStaticGetValueTypeAbstractClass
        {
            public abstract int PublicStaticGetValueType { get; }
        }

        public abstract class InternalStaticGetValueTypeAbstractClass
        {
            public abstract int InternalStaticGetValueType { get; }
        }

        public abstract class ProtectedStaticGetValueTypeAbstractClass
        {
            public abstract int ProtectedStaticGetValueType { get; }
        }

        public abstract class PrivateStaticGetValueTypeAbstractClass
        {
            public abstract int PrivateStaticGetValueType { get; }
        }

        // *
        public abstract class PublicStaticGetSetValueTypeAbstractClass
        {
            public abstract int PublicStaticGetSetValueType { get; set; }
        }

        public abstract class InternalStaticGetSetValueTypeAbstractClass
        {
            public abstract int InternalStaticGetSetValueType { get; set; }
        }

        public abstract class ProtectedStaticGetSetValueTypeAbstractClass
        {
            public abstract int ProtectedStaticGetSetValueType { get; set; }
        }

        public abstract class PrivateStaticGetSetValueTypeAbstractClass
        {
            public abstract int PrivateStaticGetSetValueType { get; set; }
        }

        // *
        public abstract class PublicGetValueTypeAbstractClass
        {
            public abstract int PublicGetValueType { get; }
        }

        public abstract class InternalGetValueTypeAbstractClass
        {
            public abstract int InternalGetValueType { get; }
        }

        public abstract class ProtectedGetValueTypeAbstractClass
        {
            public abstract int ProtectedGetValueType { get; }
        }

        public abstract class PrivateGetValueTypeAbstractClass
        {
            public abstract int PrivateGetValueType { get; }
        }

        // *
        public abstract class PublicGetSetValueTypeAbstractClass
        {
            public abstract int PublicGetSetValueType { get; set; }
        }

        public abstract class InternalGetSetValueTypeAbstractClass
        {
            public abstract int InternalGetSetValueType { get; set; }
        }

        public abstract class ProtectedGetSetValueTypeAbstractClass
        {
            public abstract int ProtectedGetSetValueType { get; set; }
        }

        public abstract class PrivateGetSetValueTypeAbstractClass
        {
            public abstract int PrivateGetSetValueType { get; set; }
        }

        // *
        public abstract class PublicStaticNullableIntAbstractClass
        {
            public abstract int? PublicStaticNullableInt { get; set; }
        }

        public abstract class PrivateStaticNullableIntAbstractClass
        {
            public abstract int? PrivateStaticNullableInt { get; set; }
        }

        public abstract class PublicNullableIntAbstractClass
        {
            public abstract int? PublicNullableInt { get; set; }
        }

        public abstract class PrivateNullableIntAbstractClass
        {
            public abstract int? PrivateNullableInt { get; set; }
        }

        // *
        public abstract class StatusAbstractClass
        {
            public abstract TaskStatus Status { get; set; }
        }

        // *
        public abstract class IndexAbstractClass
        {
            public abstract int this[int index] { get; set; }
        }
    }
}
