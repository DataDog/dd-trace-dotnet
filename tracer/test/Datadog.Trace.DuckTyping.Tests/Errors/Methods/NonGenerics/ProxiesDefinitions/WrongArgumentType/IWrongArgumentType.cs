// <copyright file="IWrongArgumentType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentType
{
    public interface IWrongArgumentType
    {
#if INTERFACE_DEFAULTS
        public interface ISum1
        {
            int Sum(int a, string b) => a;
        }
#else
        public interface ISum1
        {
            int Sum(int a, string b);
        }
#endif

        public interface ISum2
        {
            float Sum(float a, string b);
        }

        public interface ISum3
        {
            double Sum(double a, string b);
        }

        public interface ISum4
        {
            short Sum(short a, string b);
        }

        public interface IShowEnum
        {
            TestEnum2 ShowEnum(string val);
        }

        public interface IInternalSum
        {
            object InternalSum(int a, string b);
        }

        public interface IAdd1
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            void Add(int name, object obj);
        }

        public interface IAdd2
        {
            void Add(int name, int obj);
        }

        public interface IAdd3
        {
            void Add(int name, string obj = "none");
        }

        public interface IPow2
        {
            void Pow2(ref string value);
        }

        public interface IGetOutput
        {
            void GetOutput(out string value);
        }

        public interface IGetOutputObject
        {
            [Duck(Name = "GetOutput")]
            void GetOutputObject(out string value);
        }

        public interface ITryGetObscure
        {
            bool TryGetObscure(out string obj);
        }

        public interface ITryGetObscureObject
        {
            [Duck(Name = "TryGetObscure")]
            bool TryGetObscureObject(out int obj);
        }

        public interface IGetReference
        {
            void GetReference(ref string value);
        }

        public interface IGetReferenceObject
        {
            [Duck(Name = "GetReference")]
            void GetReferenceObject(ref string value);
        }

        public interface ITryGetReference
        {
            bool TryGetReference(ref string obj);
        }

        public interface ITryGetReferenceObject
        {
            [Duck(Name = "TryGetReference")]
            bool TryGetReferenceObject(ref int obj);
        }

        public interface ITryGetPrivateObscure
        {
            bool TryGetPrivateObscure(out string obj);
        }

        public interface ITryGetPrivateObscureObject
        {
            [Duck(Name = "TryGetPrivateObscure")]
            bool TryGetPrivateObscureObject(out int obj);
        }

        public interface ITryGetPrivateReference
        {
            bool TryGetPrivateReference(ref string obj);
        }

        public interface ITryGetPrivateReferenceObject
        {
            [Duck(Name = "TryGetPrivateReference")]
            bool TryGetPrivateReferenceObject(ref int obj);
        }

        public interface IBypass
        {
            IDummyFieldObject Bypass(string obj);
        }
    }
}
