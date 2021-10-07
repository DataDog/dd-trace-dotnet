// <copyright file="IValid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.Valid
{
    public interface IValid
    {
        public interface IPublicStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            int PublicStaticReadonlyValueTypeField { get; }
        }

        public interface IInternalStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            int InternalStaticReadonlyValueTypeField { get; }
        }

        public interface IProtectedStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            int ProtectedStaticReadonlyValueTypeField { get; }
        }

        public interface IPrivateStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            int PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public interface IPublicStaticValueTypeField
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            int PublicStaticValueTypeField { get; set; }
        }

        public interface IInternalStaticValueTypeField
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            int InternalStaticValueTypeField { get; set; }
        }

        public interface IProtectedStaticValueTypeField
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            int ProtectedStaticValueTypeField { get; set; }
        }

        public interface IPrivateStaticValueTypeField
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            int PrivateStaticValueTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlyValueTypeField
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            int PublicReadonlyValueTypeField { get; }
        }

        public interface IInternalReadonlyValueTypeField
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            int InternalReadonlyValueTypeField { get; }
        }

        public interface IProtectedReadonlyValueTypeField
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            int ProtectedReadonlyValueTypeField { get; }
        }

        public interface IPrivateReadonlyValueTypeField
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            int PrivateReadonlyValueTypeField { get; }
        }

        // *

        public interface IPublicValueTypeField
        {
            [DuckField(Name = "_publicValueTypeField")]
            int PublicValueTypeField { get; set; }
        }

        public interface IInternalValueTypeField
        {
            [DuckField(Name = "_internalValueTypeField")]
            int InternalValueTypeField { get; set; }
        }

        public interface IProtectedValueTypeField
        {
            [DuckField(Name = "_protectedValueTypeField")]
            int ProtectedValueTypeField { get; set; }
        }

        public interface IPrivateValueTypeField
        {
            [DuckField(Name = "_privateValueTypeField")]
            int PrivateValueTypeField { get; set; }
        }

        // *

        public interface IPublicStaticNullableIntField
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            int? PublicStaticNullableIntField { get; set; }
        }

        public interface IPrivateStaticNullableIntField
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            int? PrivateStaticNullableIntField { get; set; }
        }

        public interface IPublicNullableIntField
        {
            [DuckField(Name = "_publicNullableIntField")]
            int? PublicNullableIntField { get; set; }
        }

        public interface IPrivateNullableIntField
        {
            [DuckField(Name = "_privateNullableIntField")]
            int? PrivateNullableIntField { get; set; }
        }
    }
}
