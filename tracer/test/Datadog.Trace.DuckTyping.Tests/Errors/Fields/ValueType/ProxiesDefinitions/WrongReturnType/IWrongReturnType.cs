// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IPublicStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            char PublicStaticReadonlyValueTypeField { get; }
        }

        public interface IInternalStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            char InternalStaticReadonlyValueTypeField { get; }
        }

        public interface IProtectedStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            char ProtectedStaticReadonlyValueTypeField { get; }
        }

        public interface IPrivateStaticReadonlyValueTypeField
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            char PrivateStaticReadonlyValueTypeField { get; }
        }

        // *

        public interface IPublicStaticValueTypeField
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            char PublicStaticValueTypeField { get; set; }
        }

        public interface IInternalStaticValueTypeField
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            char InternalStaticValueTypeField { get; set; }
        }

        public interface IProtectedStaticValueTypeField
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            char ProtectedStaticValueTypeField { get; set; }
        }

        public interface IPrivateStaticValueTypeField
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            char PrivateStaticValueTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlyValueTypeField
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            char PublicReadonlyValueTypeField { get; }
        }

        public interface IInternalReadonlyValueTypeField
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            char InternalReadonlyValueTypeField { get; }
        }

        public interface IProtectedReadonlyValueTypeField
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            char ProtectedReadonlyValueTypeField { get; }
        }

        public interface IPrivateReadonlyValueTypeField
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            char PrivateReadonlyValueTypeField { get; }
        }

        // *

        public interface IPublicValueTypeField
        {
            [DuckField(Name = "_publicValueTypeField")]
            char PublicValueTypeField { get; set; }
        }

        public interface IInternalValueTypeField
        {
            [DuckField(Name = "_internalValueTypeField")]
            char InternalValueTypeField { get; set; }
        }

        public interface IProtectedValueTypeField
        {
            [DuckField(Name = "_protectedValueTypeField")]
            char ProtectedValueTypeField { get; set; }
        }

        public interface IPrivateValueTypeField
        {
            [DuckField(Name = "_privateValueTypeField")]
            char PrivateValueTypeField { get; set; }
        }

        // *

        public interface IPublicStaticNullableIntField
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            char? PublicStaticNullableIntField { get; set; }
        }

        public interface IPrivateStaticNullableIntField
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            char? PrivateStaticNullableIntField { get; set; }
        }

        public interface IPublicNullableIntField
        {
            [DuckField(Name = "_publicNullableIntField")]
            char? PublicNullableIntField { get; set; }
        }

        public interface IPrivateNullableIntField
        {
            [DuckField(Name = "_privateNullableIntField")]
            char? PrivateNullableIntField { get; set; }
        }
    }
}
