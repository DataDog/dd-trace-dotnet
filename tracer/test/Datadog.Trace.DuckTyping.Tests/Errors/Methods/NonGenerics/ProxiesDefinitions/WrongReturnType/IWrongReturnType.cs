// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
#if INTERFACE_DEFAULTS
        public interface ISum1
        {
            string Sum(int a, int b) => string.Empty;
        }
#else
        public interface ISum1
        {
            string Sum(int a, int b);
        }
#endif

        public interface ISum2
        {
            string Sum(float a, float b);
        }

        public interface ISum3
        {
            string Sum(double a, double b);
        }

        public interface ISum4
        {
            string Sum(short a, short b);
        }

        public interface IShowEnum
        {
            string ShowEnum(TestEnum2 val);
        }

        public interface IAdd1
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            string Add(string name, object obj);
        }

        public interface IAdd2
        {
            string Add(string name, int obj);
        }

        public interface IAdd3
        {
            string Add(string name, string obj = "none");
        }

        public interface IPow2
        {
            string Pow2(ref int value);
        }

        public interface IGetOutput
        {
            string GetOutput(out int value);
        }

        public interface IGetOutputObject
        {
            [Duck(Name = "GetOutput")]
            string GetOutputObject(out object value);
        }

        public interface ITryGetObscure
        {
            string TryGetObscure(out IDummyFieldObject obj);
        }

        public interface ITryGetObscureObject
        {
            [Duck(Name = "TryGetObscure")]
            string TryGetObscureObject(out object obj);
        }

        public interface IGetReference
        {
            string GetReference(ref int value);
        }

        public interface IGetReferenceObject
        {
            [Duck(Name = "GetReference")]
            string GetReferenceObject(ref object value);
        }

        public interface ITryGetReference
        {
            string TryGetReference(ref IDummyFieldObject obj);
        }

        public interface ITryGetReferenceObject
        {
            [Duck(Name = "TryGetReference")]
            string TryGetReferenceObject(ref object obj);
        }

        public interface ITryGetPrivateObscure
        {
            string TryGetPrivateObscure(out IDummyFieldObject obj);
        }

        public interface ITryGetPrivateObscureObject
        {
            [Duck(Name = "TryGetPrivateObscure")]
            string TryGetPrivateObscureObject(out object obj);
        }

        public interface ITryGetPrivateReference
        {
            string TryGetPrivateReference(ref IDummyFieldObject obj);
        }

        public interface ITryGetPrivateReferenceObject
        {
            [Duck(Name = "TryGetPrivateReference")]
            string TryGetPrivateReferenceObject(ref object obj);
        }

        public interface IBypass
        {
            string Bypass(IDummyFieldObject obj);
        }
    }
}
