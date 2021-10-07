// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class PublicStaticGetValueTypeAbstractClass
        {
            public abstract char PublicStaticGetValueType { get; }
        }

        public abstract class InternalStaticGetValueTypeAbstractClass
        {
            public abstract char InternalStaticGetValueType { get; }
        }

        public abstract class ProtectedStaticGetValueTypeAbstractClass
        {
            public abstract char ProtectedStaticGetValueType { get; }
        }

        public abstract class PrivateStaticGetValueTypeAbstractClass
        {
            public abstract char PrivateStaticGetValueType { get; }
        }

        // *
        public abstract class PublicStaticGetSetValueTypeAbstractClass
        {
            public abstract char PublicStaticGetSetValueType { get; set; }
        }

        public abstract class InternalStaticGetSetValueTypeAbstractClass
        {
            public abstract char InternalStaticGetSetValueType { get; set; }
        }

        public abstract class ProtectedStaticGetSetValueTypeAbstractClass
        {
            public abstract char ProtectedStaticGetSetValueType { get; set; }
        }

        public abstract class PrivateStaticGetSetValueTypeAbstractClass
        {
            public abstract char PrivateStaticGetSetValueType { get; set; }
        }

        // *
        public abstract class PublicGetValueTypeAbstractClass
        {
            public abstract char PublicGetValueType { get; }
        }

        public abstract class InternalGetValueTypeAbstractClass
        {
            public abstract char InternalGetValueType { get; }
        }

        public abstract class ProtectedGetValueTypeAbstractClass
        {
            public abstract char ProtectedGetValueType { get; }
        }

        public abstract class PrivateGetValueTypeAbstractClass
        {
            public abstract char PrivateGetValueType { get; }
        }

        // *
        public abstract class PublicGetSetValueTypeAbstractClass
        {
            public abstract char PublicGetSetValueType { get; set; }
        }

        public abstract class InternalGetSetValueTypeAbstractClass
        {
            public abstract char InternalGetSetValueType { get; set; }
        }

        public abstract class ProtectedGetSetValueTypeAbstractClass
        {
            public abstract char ProtectedGetSetValueType { get; set; }
        }

        public abstract class PrivateGetSetValueTypeAbstractClass
        {
            public abstract char PrivateGetSetValueType { get; set; }
        }

        // *
        public abstract class PublicStaticNullableIntAbstractClass
        {
            public abstract char? PublicStaticNullablechar { get; set; }
        }

        public abstract class PrivateStaticNullableIntAbstractClass
        {
            public abstract char? PrivateStaticNullablechar { get; set; }
        }

        public abstract class PublicNullableIntAbstractClass
        {
            public abstract char? PublicNullablechar { get; set; }
        }

        public abstract class PrivateNullableIntAbstractClass
        {
            public abstract char? PrivateNullablechar { get; set; }
        }

        // *
        public abstract class StatusAbstractClass
        {
            public abstract char Status { get; set; }
        }

        // *
        public abstract class IndexAbstractClass
        {
            public abstract char this[int index] { get; set; }
        }
    }
}
