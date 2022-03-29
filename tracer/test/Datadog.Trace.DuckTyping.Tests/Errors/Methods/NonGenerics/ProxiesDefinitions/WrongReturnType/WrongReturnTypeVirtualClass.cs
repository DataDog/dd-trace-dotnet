// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class Sum1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Sum(int a, int b) => default;
        }

        public class Sum2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Sum(float a, float b) => default;
        }

        public class Sum3VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Sum(double a, double b) => default;
        }

        public class Sum4VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Sum(short a, short b) => default;
        }

        public class ShowEnumVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string ShowEnum(TestEnum2 val) => default;
        }

        public class AddVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
            public virtual string Add(string name, object obj) => string.Empty;
        }

        public class Add1VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Add(string name, int obj) => string.Empty;
        }

        public class Add2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Add(string name, string obj = "none") => string.Empty;
        }

        public class Pow2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Pow2(ref int value) => string.Empty;
        }

        public class GetOutputVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string GetOutput(out int value)
            {
                value = default;
                return string.Empty;
            }
        }

        public class GetOutputObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetOutput")]
            public virtual string GetOutputObject(out object value)
            {
                value = default;
                return string.Empty;
            }
        }

        public class TryGetObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string TryGetObscure(out IDummyFieldObject obj)
            {
                obj = default;
                return string.Empty;
            }
        }

        public class TryGetObscureObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetObscure")]
            public virtual string TryGetObscureObject(out object obj)
            {
                obj = default;
                return string.Empty;
            }
        }

        public class GetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string GetReference(ref int value)
            {
                return string.Empty;
            }
        }

        public class GetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetReference")]
            public virtual string GetReferenceObject(ref object value)
            {
                return string.Empty;
            }
        }

        public class TryGetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string TryGetReference(ref IDummyFieldObject obj)
            {
                return string.Empty;
            }
        }

        public class TryGetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetReference")]
            public virtual string TryGetReferenceObject(ref object obj)
            {
                return string.Empty;
            }
        }

        public class TryGetPrivateObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string TryGetPrivateObscure(out IDummyFieldObject obj)
            {
                obj = default;
                return string.Empty;
            }
        }

        public class TryGetPrivateObscureObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetPrivateObscure")]
            public virtual string TryGetPrivateObscureObject(out object obj)
            {
                obj = default;
                return string.Empty;
            }
        }

        public class TryGetPrivateReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string TryGetPrivateReference(ref IDummyFieldObject obj)
            {
                return string.Empty;
            }
        }

        public class TryGetPrivateReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetPrivateReference")]
            public virtual string TryGetPrivateReferenceObject(ref object obj)
            {
                return string.Empty;
            }
        }

        public class BypassVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual string Bypass(IDummyFieldObject obj) => null;
        }
    }
}
