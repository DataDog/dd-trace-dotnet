// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class Sum1AbstractClass
        {
            public abstract string Sum(int a, int b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum2AbstractClass
        {
            public abstract string Sum(float a, float b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum3AbstractClass
        {
            public abstract string Sum(double a, double b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum4AbstractClass
        {
            public abstract string Sum(short a, short b);

            public void NormalMethod()
            {
            }
        }

        public abstract class ShowEnumAbstractClass
        {
            public abstract string ShowEnum(TestEnum2 val);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add1AbstractClass
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public abstract string Add(string name, object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add2AbstractClass
        {
            public abstract string Add(string name, int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add3AbstractClass
        {
            public abstract string Add(string name, string obj = "none");

            public void NormalMethod()
            {
            }
        }

        public abstract class Pow2AbstractClass
        {
            public abstract string Pow2(ref int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputAbstractClass
        {
            public abstract string GetOutput(out int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputObjectAbstractClass
        {
            [Duck(Name = "GetOutput")]
            public abstract string GetOutputObject(out object value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureAbstractClass
        {
            public abstract string TryGetObscure(out IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureObjectAbstractClass
        {
            [Duck(Name = "TryGetObscure")]
            public abstract string TryGetObscureObject(out object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceAbstractClass
        {
            public abstract string GetReference(ref int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceObjectAbstractClass
        {
            [Duck(Name = "GetReference")]
            public abstract string GetReferenceObject(ref object value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceAbstractClass
        {
            public abstract string TryGetReference(ref IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceObjectAbstractClass
        {
            [Duck(Name = "TryGetReference")]
            public abstract string TryGetReferenceObject(ref object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureAbstractClass
        {
            public abstract string TryGetPrivateObscure(out IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureObjectAbstractClass
        {
            [Duck(Name = "TryGetPrivateObscure")]
            public abstract string TryGetPrivateObscureObject(out object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceAbstractClass
        {
            public abstract string TryGetPrivateReference(ref IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceObjectAbstractClass
        {
            [Duck(Name = "TryGetPrivateReference")]
            public abstract string TryGetPrivateReferenceObject(ref object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class BypassAbstractClass
        {
            public abstract string Bypass(IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }
    }
}
