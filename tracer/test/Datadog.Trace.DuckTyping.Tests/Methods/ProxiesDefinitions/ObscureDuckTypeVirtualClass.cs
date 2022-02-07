// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        public virtual int Sum(int a, int b) => default;

        public virtual float Sum(float a, float b) => default;

        public virtual double Sum(double a, double b) => default;

        public virtual short Sum(short a, short b) => default;

        public virtual TestEnum2 ShowEnum(TestEnum2 val) => default;

        public virtual object InternalSum(int a, int b) => default;

        [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
        public virtual void Add(string name, object obj)
        {
        }

        public virtual void Add(string name, int obj)
        {
        }

        public virtual void Add(string name, string obj = "none")
        {
        }

        public virtual StringValues StringValuesIdentityFunc(StringValues input)
        {
            return default;
        }

        [Duck(Name = "CustomStringIdentityFunc", ParameterTypeNames = new[] { "Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions.CustomString, Datadog.Trace.DuckTyping.Tests" })]
        public virtual CustomString CustomStringIdentityFunc_StringArg(string input)
        {
            return default;
        }

        [Duck(Name = "CustomStringIdentityFunc", ParameterTypeNames = new[] { "Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions.CustomString, Datadog.Trace.DuckTyping.Tests" })]
        public virtual CustomString CustomStringIdentityFunc_StringValuesArg(StringValues input)
        {
            return default;
        }

        [Duck(Name = "StringIdentityFunc", ParameterTypeNames = new[] { "System.String" })]
        public virtual string StringIdentityFunc_StringValuesArg(StringValues input)
        {
            return default;
        }

        [Duck(Name = "StringValuesIdentityFunc", ParameterTypeNames = new[] { "Microsoft.Extensions.Primitives.StringValues, Microsoft.Extensions.Primitives" })]
        public virtual StringValues StringValuesIdentityFunc_StringArg(string input)
        {
            return default;
        }

        public virtual void Pow2(ref int value)
        {
        }

        public virtual void GetOutput(out int value)
        {
            value = default;
        }

        [Duck(Name = "GetOutput")]
        public virtual void GetOutputObject(out object value)
        {
            value = default;
        }

        public virtual bool TryGetObscure(out IDummyFieldObject obj)
        {
            obj = default;
            return false;
        }

        [Duck(Name = "TryGetObscure")]
        public virtual bool TryGetObscureObject(out object obj)
        {
            obj = default;
            return false;
        }

        public virtual void GetReference(ref int value)
        {
        }

        [Duck(Name = "GetReference")]
        public virtual void GetReferenceObject(ref object value)
        {
        }

        public virtual bool TryGetReference(ref IDummyFieldObject obj)
        {
            return false;
        }

        [Duck(Name = "TryGetReference")]
        public virtual bool TryGetReferenceObject(ref object obj)
        {
            return false;
        }

        public virtual bool TryGetPrivateObscure(out IDummyFieldObject obj)
        {
            obj = default;
            return false;
        }

        [Duck(Name = "TryGetPrivateObscure")]
        public virtual bool TryGetPrivateObscureObject(out object obj)
        {
            obj = default;
            return false;
        }

        public virtual bool TryGetPrivateReference(ref IDummyFieldObject obj)
        {
            return false;
        }

        [Duck(Name = "TryGetPrivateReference")]
        public virtual bool TryGetPrivateReferenceObject(ref object obj)
        {
            return false;
        }

        public void NormalMethod()
        {
            // .
        }

        public virtual IDummyFieldObject Bypass(IDummyFieldObject obj) => null;
    }
}
