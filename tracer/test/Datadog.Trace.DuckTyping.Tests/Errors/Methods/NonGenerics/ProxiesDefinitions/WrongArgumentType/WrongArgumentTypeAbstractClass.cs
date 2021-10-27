// <copyright file="WrongArgumentTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentType
{
    public abstract class WrongArgumentTypeAbstractClass
    {
        public abstract class Sum1AbstractClass
        {
            public abstract int Sum(int a, string b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum2AbstractClass
        {
            public abstract float Sum(float a, string b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum3AbstractClass
        {
            public abstract double Sum(double a, string b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum4AbstractClass
        {
            public abstract short Sum(short a, string b);

            public void NormalMethod()
            {
            }
        }

        public abstract class ShowEnumAbstractClass
        {
            public abstract TestEnum2 ShowEnum(string val);

            public void NormalMethod()
            {
            }
        }

        public abstract class InternalSumAbstractClass
        {
            public abstract object InternalSum(int a, string b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add1AbstractClass
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public abstract void Add(int name, object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add2AbstractClass
        {
            public abstract void Add(int name, int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add3AbstractClass
        {
            public abstract void Add(int name, string obj = "none");

            public void NormalMethod()
            {
            }
        }

        public abstract class Pow2AbstractClass
        {
            public abstract void Pow2(ref string value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputAbstractClass
        {
            public abstract void GetOutput(out string value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputObjectAbstractClass
        {
            [Duck(Name = "GetOutput")]
            public abstract void GetOutputObject(out string value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureAbstractClass
        {
            public abstract bool TryGetObscure(out string obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureObjectAbstractClass
        {
            [Duck(Name = "TryGetObscure")]
            public abstract bool TryGetObscureObject(out int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceAbstractClass
        {
            public abstract void GetReference(ref string value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceObjectAbstractClass
        {
            [Duck(Name = "GetReference")]
            public abstract void GetReferenceObject(ref string value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceAbstractClass
        {
            public abstract bool TryGetReference(ref string obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceObjectAbstractClass
        {
            [Duck(Name = "TryGetReference")]
            public abstract bool TryGetReferenceObject(ref int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureAbstractClass
        {
            public abstract bool TryGetPrivateObscure(out string obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureObjectAbstractClass
        {
            [Duck(Name = "TryGetPrivateObscure")]
            public abstract bool TryGetPrivateObscureObject(out int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceAbstractClass
        {
            public abstract bool TryGetPrivateReference(ref string obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceObjectAbstractClass
        {
            [Duck(Name = "TryGetPrivateReference")]
            public abstract bool TryGetPrivateReferenceObject(ref int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class BypassAbstractClass
        {
            public abstract IDummyFieldObject Bypass(string obj);

            public void NormalMethod()
            {
            }
        }
    }
}
