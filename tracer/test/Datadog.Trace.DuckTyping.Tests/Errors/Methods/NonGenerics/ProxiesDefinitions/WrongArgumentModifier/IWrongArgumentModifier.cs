// <copyright file="IWrongArgumentModifier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentModifier
{
    public interface IWrongArgumentModifier
    {
        public interface IPow2
        {
            void Pow2(in int value);
        }

        public interface IGetOutput
        {
            void GetOutput(in int value);
        }

        public interface IGetOutputObject
        {
            [Duck(Name = "GetOutput")]
            void GetOutputObject(in object value);
        }

        public interface ITryGetObscure
        {
            bool TryGetObscure(in IDummyFieldObject obj);
        }

        public interface ITryGetObscureObject
        {
            [Duck(Name = "TryGetObscure")]
            bool TryGetObscureObject(in object obj);
        }

        public interface IGetReference
        {
            void GetReference(in int value);
        }

        public interface IGetReferenceObject
        {
            [Duck(Name = "GetReference")]
            void GetReferenceObject(in object value);
        }

        public interface ITryGetReference
        {
            bool TryGetReference(in IDummyFieldObject obj);
        }

        public interface ITryGetReferenceObject
        {
            [Duck(Name = "TryGetReference")]
            bool TryGetReferenceObject(in object obj);
        }

        public interface ITryGetPrivateObscure
        {
            bool TryGetPrivateObscure(in IDummyFieldObject obj);
        }

        public interface ITryGetPrivateObscureObject
        {
            [Duck(Name = "TryGetPrivateObscure")]
            bool TryGetPrivateObscureObject(in object obj);
        }

        public interface ITryGetPrivateReference
        {
            bool TryGetPrivateReference(in IDummyFieldObject obj);
        }

        public interface ITryGetPrivateReferenceObject
        {
            [Duck(Name = "TryGetPrivateReference")]
            bool TryGetPrivateReferenceObject(in object obj);
        }
    }
}
