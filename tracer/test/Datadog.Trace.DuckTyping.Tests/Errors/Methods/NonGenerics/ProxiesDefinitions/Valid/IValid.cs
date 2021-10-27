// <copyright file="IValid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.Valid
{
    public interface IValid
    {
#if INTERFACE_DEFAULTS
        public interface ISum1
        {
            int Sum(int a, int b) => a + b;
        }
#else
        public interface ISum1
        {
            int Sum(int a, int b);
        }
#endif

        public interface ISum2
        {
            float Sum(float a, float b);
        }

        public interface ISum3
        {
            double Sum(double a, double b);
        }

        public interface ISum4
        {
            short Sum(short a, short b);
        }

        public interface IShowEnum
        {
            TestEnum2 ShowEnum(TestEnum2 val);
        }

        public interface IInternalSum
        {
            object InternalSum(int a, int b);
        }

        public interface IAdd1
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            void Add(string name, object obj);
        }

        public interface IAdd2
        {
            void Add(string name, int obj);
        }

        public interface IAdd3
        {
            void Add(string name, string obj = "none");
        }

        public interface IPow2
        {
            void Pow2(ref int value);
        }

        public interface IGetOutput
        {
            void GetOutput(out int value);
        }

        public interface IGetOutputObject
        {
            [Duck(Name = "GetOutput")]
            void GetOutputObject(out object value);
        }

        public interface ITryGetObscure
        {
            bool TryGetObscure(out IDummyFieldObject obj);
        }

        public interface ITryGetObscureObject
        {
            [Duck(Name = "TryGetObscure")]
            bool TryGetObscureObject(out object obj);
        }

        public interface IGetReference
        {
            void GetReference(ref int value);
        }

        public interface IGetReferenceObject
        {
            [Duck(Name = "GetReference")]
            void GetReferenceObject(ref object value);
        }

        public interface ITryGetReference
        {
            bool TryGetReference(ref IDummyFieldObject obj);
        }

        public interface ITryGetReferenceObject
        {
            [Duck(Name = "TryGetReference")]
            bool TryGetReferenceObject(ref object obj);
        }

        public interface ITryGetPrivateObscure
        {
            bool TryGetPrivateObscure(out IDummyFieldObject obj);
        }

        public interface ITryGetPrivateObscureObject
        {
            [Duck(Name = "TryGetPrivateObscure")]
            bool TryGetPrivateObscureObject(out object obj);
        }

        public interface ITryGetPrivateReference
        {
            bool TryGetPrivateReference(ref IDummyFieldObject obj);
        }

        public interface ITryGetPrivateReferenceObject
        {
            [Duck(Name = "TryGetPrivateReference")]
            bool TryGetPrivateReferenceObject(ref object obj);
        }

        public interface IBypass
        {
            IDummyFieldObject Bypass(IDummyFieldObject obj);
        }
    }
}
