// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IPublicStaticGetValueType
        {
            char PublicStaticGetValueType { get; }
        }

        public interface IInternalStaticGetValueType
        {
            char InternalStaticGetValueType { get; }
        }

        public interface IProtectedStaticGetValueType
        {
            char ProtectedStaticGetValueType { get; }
        }

        public interface IPrivateStaticGetValueType
        {
            char PrivateStaticGetValueType { get; }
        }

        // *
        public interface IPublicStaticGetSetValueType
        {
            char PublicStaticGetSetValueType { get; set; }
        }

        public interface IInternalStaticGetSetValueType
        {
            char InternalStaticGetSetValueType { get; set; }
        }

        public interface IProtectedStaticGetSetValueType
        {
            char ProtectedStaticGetSetValueType { get; set; }
        }

        public interface IPrivateStaticGetSetValueType
        {
            char PrivateStaticGetSetValueType { get; set; }
        }

        // *

        public interface IPublicGetValueType
        {
            char PublicGetValueType { get; }
        }

        public interface IInternalGetValueType
        {
            char InternalGetValueType { get; }
        }

        public interface IProtectedGetValueType
        {
            char ProtectedGetValueType { get; }
        }

        public interface IPrivateGetValueType
        {
            char PrivateGetValueType { get; }
        }

        // *
        public interface IPublicGetSetValueType
        {
            char PublicGetSetValueType { get; set; }
        }

        public interface IInternalGetSetValueType
        {
            char InternalGetSetValueType { get; set; }
        }

        public interface IProtectedGetSetValueType
        {
            char ProtectedGetSetValueType { get; set; }
        }

        public interface IPrivateGetSetValueType
        {
            char PrivateGetSetValueType { get; set; }
        }

        // *
        public interface IPublicStaticNullableInt
        {
            char? PublicStaticNullablechar { get; set; }
        }

        public interface IPrivateStaticNullableInt
        {
            char? PrivateStaticNullablechar { get; set; }
        }

        public interface IPublicNullableInt
        {
            char? PublicNullablechar { get; set; }
        }

        public interface IPrivateNullableInt
        {
            char? PrivateNullablechar { get; set; }
        }

        // *
        public interface IStatus
        {
            char Status { get; set; }
        }

        // *
        public interface IIndex
        {
            char this[int index] { get; set; }
        }
    }
}
