// <copyright file="WrongNumberOfArgumentsVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongNumberOfArguments
{
    public class WrongNumberOfArgumentsVirtualClass
    {
        public class Sum1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual int Sum(int a, int b, string wrong) => default;
        }

        public class Sum2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual float Sum(float a, float b, string wrong) => default;
        }

        public class Sum3VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual double Sum(double a, double b, string wrong) => default;
        }

        public class Sum4VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual short Sum(short a, short b, string wrong) => default;
        }

        public class ShowEnumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual TestEnum2 ShowEnum(TestEnum2 val, string wrong) => default;
        }

        public class InternalSumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual object InternalSum(int a, int b, string wrong) => default;
        }

        public class AddVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests", "System.String" })]
            public virtual void Add(string name, object obj, string wrong)
            {
            }
        }

        public class Add1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Add(string name, int obj, string wrong)
            {
            }
        }

        public class Add2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Add(string name, string wrong, string obj = "none")
            {
            }
        }

        public class Pow2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Pow2(ref int value, string wrong)
            {
            }
        }

        public class GetOutputVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void GetOutput(out int value, string wrong)
            {
                value = default;
            }
        }

        public class GetOutputObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetOutput")]
            public virtual void GetOutputObject(out object value, string wrong)
            {
                value = default;
            }
        }

        public class TryGetObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetObscure(out IDummyFieldObject obj, string wrong)
            {
                obj = default;
                return false;
            }
        }

        public class TryGetObscureObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetObscure")]
            public virtual bool TryGetObscureObject(out object obj, string wrong)
            {
                obj = default;
                return false;
            }
        }

        public class GetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void GetReference(ref int value, string wrong)
            {
            }
        }

        public class GetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetReference")]
            public virtual void GetReferenceObject(ref object value, string wrong)
            {
            }
        }

        public class TryGetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetReference(ref IDummyFieldObject obj, string wrong)
            {
                return false;
            }
        }

        public class TryGetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetReference")]
            public virtual bool TryGetReferenceObject(ref object obj, string wrong)
            {
                return false;
            }
        }

        public class TryGetPrivateObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetPrivateObscure(out IDummyFieldObject obj, string wrong)
            {
                obj = default;
                return false;
            }
        }

        public class TryGetPrivateObscureObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetPrivateObscure")]
            public virtual bool TryGetPrivateObscureObject(out object obj, string wrong)
            {
                obj = default;
                return false;
            }
        }

        public class TryGetPrivateReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetPrivateReference(ref IDummyFieldObject obj, string wrong)
            {
                return false;
            }
        }

        public class TryGetPrivateReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetPrivateReference")]
            public virtual bool TryGetPrivateReferenceObject(ref object obj, string wrong)
            {
                return false;
            }
        }

        public class BypassVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual IDummyFieldObject Bypass(IDummyFieldObject obj, string wrong) => null;
        }
    }
}
