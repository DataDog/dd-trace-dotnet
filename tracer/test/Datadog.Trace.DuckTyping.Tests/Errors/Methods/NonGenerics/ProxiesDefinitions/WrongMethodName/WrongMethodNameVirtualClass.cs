// <copyright file="WrongMethodNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongMethodName
{
    public class WrongMethodNameVirtualClass
    {
        public class Sum1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual int NotSum(int a, int b) => default;
        }

        public class Sum2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual float NotSum(float a, float b) => default;
        }

        public class Sum3VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual double NotSum(double a, double b) => default;
        }

        public class Sum4VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual short NotSum(short a, short b) => default;
        }

        public class ShowEnumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual TestEnum2 NotShowEnum(TestEnum2 val) => default;
        }

        public class InternalSumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual object NotInternalSum(int a, int b) => default;
        }

        public class AddVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public virtual void NotAdd(string name, object obj)
            {
            }
        }

        public class Add1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void NotAdd(string name, int obj)
            {
            }
        }

        public class Add2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void NotAdd(string name, string obj = "none")
            {
            }
        }

        public class Pow2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void NotPow2(ref int value)
            {
            }
        }

        public class GetOutputVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void NotGetOutput(out int value)
            {
                value = default;
            }
        }

        public class GetOutputObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "NotGetOutput")]
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

            public virtual bool NotTryGetObscure(out IDummyFieldObject obj)
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

            [Duck(Name = "NotTryGetObscure")]
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

            public virtual void NotGetReference(ref int value)
            {
            }
        }

        public class GetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "NotGetReference")]
            public virtual void GetReferenceObject(ref object value)
            {
            }
        }

        public class TryGetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool NotTryGetReference(ref IDummyFieldObject obj)
            {
                return false;
            }
        }

        public class TryGetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "NotTryGetReference")]
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

            public virtual bool NotTryGetPrivateObscure(out IDummyFieldObject obj)
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

            [Duck(Name = "NotTryGetPrivateObscure")]
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

            public virtual bool NotTryGetPrivateReference(ref IDummyFieldObject obj)
            {
                return false;
            }
        }

        public class TryGetPrivateReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "NotTryGetPrivateReference")]
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

            public virtual IDummyFieldObject NotBypass(IDummyFieldObject obj) => null;
        }
    }
}
