// <copyright file="IWrongFieldName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongFieldName
{
    public interface IWrongFieldName
    {
        public interface IPublicStaticReadonlyValueTypeField
        {
            [DuckField(Name = "publicStaticReadonlyValueTypeField")]
            int PublicStaticReadonlyValueTypeField { get; }
        }

        public interface IInternalStaticReadonlyValueTypeField
        {
            [DuckField(Name = "internalStaticReadonlyValueTypeField")]
            int InternalStaticReadonlyValueTypeField { get; }
        }

        public interface IProtectedStaticReadonlyValueTypeField
        {
            [DuckField(Name = "protectedStaticReadonlyValueTypeField")]
            int ProtectedStaticReadonlyValueTypeField { get; }
        }

        public interface IPrivateStaticReadonlyValueTypeField
        {
            [DuckField(Name = "privateStaticReadonlyValueTypeField")]
            int PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public interface IPublicStaticValueTypeField
        {
            [DuckField(Name = "publicStaticValueTypeField")]
            int PublicStaticValueTypeField { get; set; }
        }

        public interface IInternalStaticValueTypeField
        {
            [DuckField(Name = "internalStaticValueTypeField")]
            int InternalStaticValueTypeField { get; set; }
        }

        public interface IProtectedStaticValueTypeField
        {
            [DuckField(Name = "protectedStaticValueTypeField")]
            int ProtectedStaticValueTypeField { get; set; }
        }

        public interface IPrivateStaticValueTypeField
        {
            [DuckField(Name = "privateStaticValueTypeField")]
            int PrivateStaticValueTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlyValueTypeField
        {
            [DuckField(Name = "publicReadonlyValueTypeField")]
            int PublicReadonlyValueTypeField { get; }
        }

        public interface IInternalReadonlyValueTypeField
        {
            [DuckField(Name = "internalReadonlyValueTypeField")]
            int InternalReadonlyValueTypeField { get; }
        }

        public interface IProtectedReadonlyValueTypeField
        {
            [DuckField(Name = "protectedReadonlyValueTypeField")]
            int ProtectedReadonlyValueTypeField { get; }
        }

        public interface IPrivateReadonlyValueTypeField
        {
            [DuckField(Name = "privateReadonlyValueTypeField")]
            int PrivateReadonlyValueTypeField { get; }
        }

        // *

        public interface IPublicValueTypeField
        {
            [DuckField(Name = "publicValueTypeField")]
            int PublicValueTypeField { get; set; }
        }

        public interface IInternalValueTypeField
        {
            [DuckField(Name = "internalValueTypeField")]
            int InternalValueTypeField { get; set; }
        }

        public interface IProtectedValueTypeField
        {
            [DuckField(Name = "protectedValueTypeField")]
            int ProtectedValueTypeField { get; set; }
        }

        public interface IPrivateValueTypeField
        {
            [DuckField(Name = "privateValueTypeField")]
            int PrivateValueTypeField { get; set; }
        }

        // *

        public interface IPublicStaticNullableIntField
        {
            [DuckField(Name = "publicStaticNullableIntField")]
            int? PublicStaticNullableIntField { get; set; }
        }

        public interface IPrivateStaticNullableIntField
        {
            [DuckField(Name = "privateStaticNullableIntField")]
            int? PrivateStaticNullableIntField { get; set; }
        }

        public interface IPublicNullableIntField
        {
            [DuckField(Name = "publicNullableIntField")]
            int? PublicNullableIntField { get; set; }
        }

        public interface IPrivateNullableIntField
        {
            [DuckField(Name = "privateNullableIntField")]
            int? PrivateNullableIntField { get; set; }
        }
    }
}
