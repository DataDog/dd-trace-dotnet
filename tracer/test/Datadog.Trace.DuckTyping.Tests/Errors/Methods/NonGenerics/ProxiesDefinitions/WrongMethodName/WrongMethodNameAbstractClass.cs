// <copyright file="WrongMethodNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongMethodName
{
    public abstract class WrongMethodNameAbstractClass
    {
        public abstract class Sum1AbstractClass
        {
            public abstract int NotSum(int a, int b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum2AbstractClass
        {
            public abstract float NotSum(float a, float b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum3AbstractClass
        {
            public abstract double NotSum(double a, double b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Sum4AbstractClass
        {
            public abstract short NotSum(short a, short b);

            public void NormalMethod()
            {
            }
        }

        public abstract class ShowEnumAbstractClass
        {
            public abstract TestEnum2 NotShowEnum(TestEnum2 val);

            public void NormalMethod()
            {
            }
        }

        public abstract class InternalSumAbstractClass
        {
            public abstract object NotInternalSum(int a, int b);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add1AbstractClass
        {
            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public abstract void NotAdd(string name, object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add2AbstractClass
        {
            public abstract void NotAdd(string name, int obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class Add3AbstractClass
        {
            public abstract void NotAdd(string name, string obj = "none");

            public void NormalMethod()
            {
            }
        }

        public abstract class Pow2AbstractClass
        {
            public abstract void NotPow2(ref int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputAbstractClass
        {
            public abstract void NotGetOutput(out int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputObjectAbstractClass
        {
            [Duck(Name = "NotGetOutput")]
            public abstract void GetOutputObject(out object value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureAbstractClass
        {
            public abstract bool NotTryGetObscure(out IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureObjectAbstractClass
        {
            [Duck(Name = "NotTryGetObscure")]
            public abstract bool TryGetObscureObject(out object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceAbstractClass
        {
            public abstract void NotGetReference(ref int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceObjectAbstractClass
        {
            [Duck(Name = "NotGetReference")]
            public abstract void GetReferenceObject(ref object value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceAbstractClass
        {
            public abstract bool NotTryGetReference(ref IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceObjectAbstractClass
        {
            [Duck(Name = "NotTryGetReference")]
            public abstract bool TryGetReferenceObject(ref object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureAbstractClass
        {
            public abstract bool NotTryGetPrivateObscure(out IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureObjectAbstractClass
        {
            [Duck(Name = "NotTryGetPrivateObscure")]
            public abstract bool TryGetPrivateObscureObject(out object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceAbstractClass
        {
            public abstract bool NotTryGetPrivateReference(ref IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceObjectAbstractClass
        {
            [Duck(Name = "NotTryGetPrivateReference")]
            public abstract bool TryGetPrivateReferenceObject(ref object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class BypassAbstractClass
        {
            public abstract IDummyFieldObject NotBypass(IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }
    }
}
