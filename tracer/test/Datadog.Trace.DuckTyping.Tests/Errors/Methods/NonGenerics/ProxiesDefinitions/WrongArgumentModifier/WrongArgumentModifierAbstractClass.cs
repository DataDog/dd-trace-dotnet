// <copyright file="WrongArgumentModifierAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentModifier
{
    public abstract class WrongArgumentModifierAbstractClass
    {
        public abstract class Pow2AbstractClass
        {
            public abstract void Pow2(in int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputAbstractClass
        {
            public abstract void GetOutput(in int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetOutputObjectAbstractClass
        {
            [Duck(Name = "GetOutput")]
            public abstract void GetOutputObject(in object value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureAbstractClass
        {
            public abstract bool TryGetObscure(in IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetObscureObjectAbstractClass
        {
            [Duck(Name = "TryGetObscure")]
            public abstract bool TryGetObscureObject(in object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceAbstractClass
        {
            public abstract void GetReference(in int value);

            public void NormalMethod()
            {
            }
        }

        public abstract class GetReferenceObjectAbstractClass
        {
            [Duck(Name = "GetReference")]
            public abstract void GetReferenceObject(in object value);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceAbstractClass
        {
            public abstract bool TryGetReference(in IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetReferenceObjectAbstractClass
        {
            [Duck(Name = "TryGetReference")]
            public abstract bool TryGetReferenceObject(in object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureAbstractClass
        {
            public abstract bool TryGetPrivateObscure(in IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateObscureObjectAbstractClass
        {
            [Duck(Name = "TryGetPrivateObscure")]
            public abstract bool TryGetPrivateObscureObject(in object obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceAbstractClass
        {
            public abstract bool TryGetPrivateReference(in IDummyFieldObject obj);

            public void NormalMethod()
            {
            }
        }

        public abstract class TryGetPrivateReferenceObjectAbstractClass
        {
            [Duck(Name = "TryGetPrivateReference")]
            public abstract bool TryGetPrivateReferenceObject(in object obj);

            public void NormalMethod()
            {
            }
        }
    }
}
