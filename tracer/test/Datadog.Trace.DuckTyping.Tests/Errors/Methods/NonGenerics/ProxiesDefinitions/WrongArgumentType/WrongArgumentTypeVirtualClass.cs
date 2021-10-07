// <copyright file="WrongArgumentTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentType
{
    public class WrongArgumentTypeVirtualClass
    {
        public class Sum1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual int Sum(int a, string b) => default;
        }

        public class Sum2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual float Sum(float a, string b) => default;
        }

        public class Sum3VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual double Sum(double a, string b) => default;
        }

        public class Sum4VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual short Sum(short a, string b) => default;
        }

        public class ShowEnumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual TestEnum2 ShowEnum(string val) => default;
        }

        public class InternalSumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual object InternalSum(int a, string b) => default;
        }

        public class AddVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public virtual void Add(int name, object obj)
            {
            }
        }

        public class Add1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Add(int name, int obj)
            {
            }
        }

        public class Add2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Add(int name, string obj = "none")
            {
            }
        }

        public class Pow2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Pow2(ref string value)
            {
            }
        }

        public class GetOutputVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void GetOutput(out string value)
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
            public virtual void GetOutputObject(out string value)
            {
                value = default;
            }
        }

        public class TryGetObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetObscure(out string obj)
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
            public virtual bool TryGetObscureObject(out int obj)
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

            public virtual void GetReference(ref string value)
            {
            }
        }

        public class GetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetReference")]
            public virtual void GetReferenceObject(ref string value)
            {
            }
        }

        public class TryGetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetReference(ref string obj)
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
            public virtual bool TryGetReferenceObject(ref int obj)
            {
                return false;
            }
        }

        public class TryGetPrivateObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetPrivateObscure(out string obj)
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
            public virtual bool TryGetPrivateObscureObject(out int obj)
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

            public virtual bool TryGetPrivateReference(ref string obj)
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
            public virtual bool TryGetPrivateReferenceObject(ref int obj)
            {
                return false;
            }
        }

        public class BypassVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual IDummyFieldObject Bypass(string obj) => null;
        }
    }
}
