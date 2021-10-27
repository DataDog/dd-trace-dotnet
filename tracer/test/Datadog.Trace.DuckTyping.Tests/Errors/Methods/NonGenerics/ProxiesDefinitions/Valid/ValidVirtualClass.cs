// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class Sum1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual int Sum(int a, int b) => default;
        }

        public class Sum2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual float Sum(float a, float b) => default;
        }

        public class Sum3VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual double Sum(double a, double b) => default;
        }

        public class Sum4VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual short Sum(short a, short b) => default;
        }

        public class ShowEnumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual TestEnum2 ShowEnum(TestEnum2 val) => default;
        }

        public class InternalSumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual object InternalSum(int a, int b) => default;
        }

        public class AddVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public virtual void Add(string name, object obj)
            {
            }
        }

        public class Add1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Add(string name, int obj)
            {
            }
        }

        public class Add2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Add(string name, string obj = "none")
            {
            }
        }

        public class Pow2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Pow2(ref int value)
            {
            }
        }

        public class GetOutputVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void GetOutput(out int value)
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
            public virtual void GetOutputObject(out object value)
            {
                value = default;
            }
        }

        public class TryGetObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetObscure(out IDummyFieldObject obj)
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
            public virtual bool TryGetObscureObject(out object obj)
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

            public virtual void GetReference(ref int value)
            {
            }
        }

        public class GetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetReference")]
            public virtual void GetReferenceObject(ref object value)
            {
            }
        }

        public class TryGetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetReference(ref IDummyFieldObject obj)
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
            public virtual bool TryGetReferenceObject(ref object obj)
            {
                return false;
            }
        }

        public class TryGetPrivateObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetPrivateObscure(out IDummyFieldObject obj)
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
            public virtual bool TryGetPrivateObscureObject(out object obj)
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

            public virtual bool TryGetPrivateReference(ref IDummyFieldObject obj)
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
            public virtual bool TryGetPrivateReferenceObject(ref object obj)
            {
                return false;
            }
        }

        public class BypassVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual IDummyFieldObject Bypass(IDummyFieldObject obj) => null;
        }
    }
}
