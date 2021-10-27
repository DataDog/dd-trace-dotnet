// <copyright file="IWrongNumberOfArguments.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongNumberOfArguments
{
    public interface IWrongNumberOfArguments
    {
#if INTERFACE_DEFAULTS
        public interface ISum1
        {
            int Sum(int a, int b, string wrong) => a + b;
        }
#else
        public interface ISum1
        {
            int Sum(int a, int b, string wrong);
        }
#endif

        public interface ISum2
        {
            float Sum(float a, float b, string wrong);
        }

        public interface ISum3
        {
            double Sum(double a, double b, string wrong);
        }

        public interface ISum4
        {
            short Sum(short a, short b, string wrong);
        }

        public interface IShowEnum
        {
            TestEnum2 ShowEnum(TestEnum2 val, string wrong);
        }

        public interface IInternalSum
        {
            object InternalSum(int a, int b, string wrong);
        }

        public interface IAdd1
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            void Add(string name, object obj, string wrong);
        }

        public interface IAdd2
        {
            void Add(string name, int obj, string wrong);
        }

        public interface IAdd3
        {
            void Add(string name, string wrong, string obj = "none");
        }

        public interface IPow2
        {
            void Pow2(ref int value, string wrong);
        }

        public interface IGetOutput
        {
            void GetOutput(out int value, string wrong);
        }

        public interface IGetOutputObject
        {
            [Duck(Name = "GetOutput")]
            void GetOutputObject(out object value, string wrong);
        }

        public interface ITryGetObscure
        {
            bool TryGetObscure(out IDummyFieldObject obj, string wrong);
        }

        public interface ITryGetObscureObject
        {
            [Duck(Name = "TryGetObscure")]
            bool TryGetObscureObject(out object obj, string wrong);
        }

        public interface IGetReference
        {
            void GetReference(ref int value, string wrong);
        }

        public interface IGetReferenceObject
        {
            [Duck(Name = "GetReference")]
            void GetReferenceObject(ref object value, string wrong);
        }

        public interface ITryGetReference
        {
            bool TryGetReference(ref IDummyFieldObject obj, string wrong);
        }

        public interface ITryGetReferenceObject
        {
            [Duck(Name = "TryGetReference")]
            bool TryGetReferenceObject(ref object obj, string wrong);
        }

        public interface ITryGetPrivateObscure
        {
            bool TryGetPrivateObscure(out IDummyFieldObject obj, string wrong);
        }

        public interface ITryGetPrivateObscureObject
        {
            [Duck(Name = "TryGetPrivateObscure")]
            bool TryGetPrivateObscureObject(out object obj, string wrong);
        }

        public interface ITryGetPrivateReference
        {
            bool TryGetPrivateReference(ref IDummyFieldObject obj, string wrong);
        }

        public interface ITryGetPrivateReferenceObject
        {
            [Duck(Name = "TryGetPrivateReference")]
            bool TryGetPrivateReferenceObject(ref object obj, string wrong);
        }

        public interface IBypass
        {
            IDummyFieldObject Bypass(IDummyFieldObject obj, string wrong);
        }
    }
}
