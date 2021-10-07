// <copyright file="WrongArgumentModifierVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentModifier
{
    public class WrongArgumentModifierVirtualClass
    {
        public class Pow2VirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void Pow2(in int value)
            {
            }
        }

        public class GetOutputVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void GetOutput(in int value)
            {
            }
        }

        public class GetOutputObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetOutput")]
            public virtual void GetOutputObject(in object value)
            {
            }
        }

        public class TryGetObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetObscure(in IDummyFieldObject obj)
            {
                return false;
            }
        }

        public class TryGetObscureObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetObscure")]
            public virtual bool TryGetObscureObject(in object obj)
            {
                return false;
            }
        }

        public class GetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual void GetReference(in int value)
            {
            }
        }

        public class GetReferenceObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "GetReference")]
            public virtual void GetReferenceObject(in object value)
            {
            }
        }

        public class TryGetReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetReference(in IDummyFieldObject obj)
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
            public virtual bool TryGetReferenceObject(in object obj)
            {
                return false;
            }
        }

        public class TryGetPrivateObscureVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetPrivateObscure(in IDummyFieldObject obj)
            {
                return false;
            }
        }

        public class TryGetPrivateObscureObjectVirtualClass
        {
            public void NormalMethod()
            {
            }

            [Duck(Name = "TryGetPrivateObscure")]
            public virtual bool TryGetPrivateObscureObject(in object obj)
            {
                return false;
            }
        }

        public class TryGetPrivateReferenceVirtualClass
        {
            public void NormalMethod()
            {
            }

            public virtual bool TryGetPrivateReference(in IDummyFieldObject obj)
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
            public virtual bool TryGetPrivateReferenceObject(in object obj)
            {
                return false;
            }
        }
    }
}
