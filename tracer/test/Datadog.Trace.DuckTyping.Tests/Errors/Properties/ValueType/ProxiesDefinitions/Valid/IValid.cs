// <copyright file="IValid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.Valid
{
    public interface IValid
    {
        public interface IPublicStaticGetValueType
        {
            int PublicStaticGetValueType { get; }
        }

        public interface IInternalStaticGetValueType
        {
            int InternalStaticGetValueType { get; }
        }

        public interface IProtectedStaticGetValueType
        {
            int ProtectedStaticGetValueType { get; }
        }

        public interface IPrivateStaticGetValueType
        {
            int PrivateStaticGetValueType { get; }
        }

        // *
        public interface IPublicStaticGetSetValueType
        {
            int PublicStaticGetSetValueType { get; set; }
        }

        public interface IInternalStaticGetSetValueType
        {
            int InternalStaticGetSetValueType { get; set; }
        }

        public interface IProtectedStaticGetSetValueType
        {
            int ProtectedStaticGetSetValueType { get; set; }
        }

        public interface IPrivateStaticGetSetValueType
        {
            int PrivateStaticGetSetValueType { get; set; }
        }

        // *

        public interface IPublicGetValueType
        {
            int PublicGetValueType { get; }
        }

        public interface IInternalGetValueType
        {
            int InternalGetValueType { get; }
        }

        public interface IProtectedGetValueType
        {
            int ProtectedGetValueType { get; }
        }

        public interface IPrivateGetValueType
        {
            int PrivateGetValueType { get; }
        }

        // *
        public interface IPublicGetSetValueType
        {
            int PublicGetSetValueType { get; set; }
        }

        public interface IInternalGetSetValueType
        {
            int InternalGetSetValueType { get; set; }
        }

        public interface IProtectedGetSetValueType
        {
            int ProtectedGetSetValueType { get; set; }
        }

        public interface IPrivateGetSetValueType
        {
            int PrivateGetSetValueType { get; set; }
        }

        // *
        public interface IPublicStaticNullableInt
        {
            int? PublicStaticNullableInt { get; set; }
        }

        public interface IPrivateStaticNullableInt
        {
            int? PrivateStaticNullableInt { get; set; }
        }

        public interface IPublicNullableInt
        {
            int? PublicNullableInt { get; set; }
        }

        public interface IPrivateNullableInt
        {
            int? PrivateNullableInt { get; set; }
        }

        // *
        public interface IStatus
        {
            TaskStatus Status { get; set; }
        }

        // *
        public interface IIndex
        {
            int this[int index] { get; set; }
        }
    }
}
