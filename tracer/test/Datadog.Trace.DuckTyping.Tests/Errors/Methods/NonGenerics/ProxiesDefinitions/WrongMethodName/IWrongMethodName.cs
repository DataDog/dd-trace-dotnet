// <copyright file="IWrongMethodName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongMethodName
{
    public interface IWrongMethodName
    {
#if INTERFACE_DEFAULTS
        public interface ISum1
        {
            int NotSum(int a, int b) => a + b;
        }
#else
        public interface ISum1
        {
            int NotSum(int a, int b);
        }
#endif

        public interface ISum2
        {
            float NotSum(float a, float b);
        }

        public interface ISum3
        {
            double NotSum(double a, double b);
        }

        public interface ISum4
        {
            short NotSum(short a, short b);
        }

        public interface IShowEnum
        {
            TestEnum2 NotShowEnum(TestEnum2 val);
        }

        public interface IInternalSum
        {
            object NotInternalSum(int a, int b);
        }

        public interface IAdd1
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            void NotAdd(string name, object obj);
        }

        public interface IAdd2
        {
            void NotAdd(string name, int obj);
        }

        public interface IAdd3
        {
            void NotAdd(string name, string obj = "none");
        }

        public interface IPow2
        {
            void NotPow2(ref int value);
        }

        public interface IGetOutput
        {
            void NotGetOutput(out int value);
        }

        public interface IGetOutputObject
        {
            [Duck(Name = "NotGetOutput")]
            void GetOutputObject(out object value);
        }

        public interface ITryGetObscure
        {
            bool NotTryGetObscure(out IDummyFieldObject obj);
        }

        public interface ITryGetObscureObject
        {
            [Duck(Name = "NotTryGetObscure")]
            bool TryGetObscureObject(out object obj);
        }

        public interface IGetReference
        {
            void NotGetReference(ref int value);
        }

        public interface IGetReferenceObject
        {
            [Duck(Name = "NotGetReference")]
            void GetReferenceObject(ref object value);
        }

        public interface ITryGetReference
        {
            bool NotTryGetReference(ref IDummyFieldObject obj);
        }

        public interface ITryGetReferenceObject
        {
            [Duck(Name = "NotTryGetReference")]
            bool TryGetReferenceObject(ref object obj);
        }

        public interface ITryGetPrivateObscure
        {
            bool NotTryGetPrivateObscure(out IDummyFieldObject obj);
        }

        public interface ITryGetPrivateObscureObject
        {
            [Duck(Name = "NotTryGetPrivateObscure")]
            bool TryGetPrivateObscureObject(out object obj);
        }

        public interface ITryGetPrivateReference
        {
            bool NotTryGetPrivateReference(ref IDummyFieldObject obj);
        }

        public interface ITryGetPrivateReferenceObject
        {
            [Duck(Name = "NotTryGetPrivateReference")]
            bool TryGetPrivateReferenceObject(ref object obj);
        }

        public interface IBypass
        {
            IDummyFieldObject NotBypass(IDummyFieldObject obj);
        }
    }
}
