// <copyright file="IWrongPropertyName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongPropertyName
{
    public interface IWrongPropertyName
    {
        public interface IPublicStaticGetValueType
        {
            int NotPublicStaticGetValueType { get; }
        }

        public interface IInternalStaticGetValueType
        {
            int NotInternalStaticGetValueType { get; }
        }

        public interface IProtectedStaticGetValueType
        {
            int NotProtectedStaticGetValueType { get; }
        }

        public interface IPrivateStaticGetValueType
        {
            int NotPrivateStaticGetValueType { get; }
        }

        // *
        public interface IPublicStaticGetSetValueType
        {
            int NotPublicStaticGetSetValueType { get; set; }
        }

        public interface IInternalStaticGetSetValueType
        {
            int NotInternalStaticGetSetValueType { get; set; }
        }

        public interface IProtectedStaticGetSetValueType
        {
            int NotProtectedStaticGetSetValueType { get; set; }
        }

        public interface IPrivateStaticGetSetValueType
        {
            int NotPrivateStaticGetSetValueType { get; set; }
        }

        // *

        public interface IPublicGetValueType
        {
            int NotPublicGetValueType { get; }
        }

        public interface IInternalGetValueType
        {
            int NotInternalGetValueType { get; }
        }

        public interface IProtectedGetValueType
        {
            int NotProtectedGetValueType { get; }
        }

        public interface IPrivateGetValueType
        {
            int NotPrivateGetValueType { get; }
        }

        // *
        public interface IPublicGetSetValueType
        {
            int NotPublicGetSetValueType { get; set; }
        }

        public interface IInternalGetSetValueType
        {
            int NotInternalGetSetValueType { get; set; }
        }

        public interface IProtectedGetSetValueType
        {
            int NotProtectedGetSetValueType { get; set; }
        }

        public interface IPrivateGetSetValueType
        {
            int NotPrivateGetSetValueType { get; set; }
        }

        // *
        public interface IPublicStaticNullableInt
        {
            int? NotPublicStaticNullableInt { get; set; }
        }

        public interface IPrivateStaticNullableInt
        {
            int? NotPrivateStaticNullableInt { get; set; }
        }

        public interface IPublicNullableInt
        {
            int? NotPublicNullableInt { get; set; }
        }

        public interface IPrivateNullableInt
        {
            int? NotPrivateNullableInt { get; set; }
        }

        // *
        public interface IStatus
        {
            TaskStatus NotStatus { get; set; }
        }
    }
}
