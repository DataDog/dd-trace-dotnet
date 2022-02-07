// <copyright file="IObscureDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
#if INTERFACE_DEFAULTS
        int Sum(int a, int b) => a + b;
#else
        int Sum(int a, int b);
#endif

        float Sum(float a, float b);

        double Sum(double a, double b);

        short Sum(short a, short b);

        TestEnum2 ShowEnum(TestEnum2 val);

        object InternalSum(int a, int b);

        [Duck(ParameterTypeNames = new string[] { "System.String", "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests" })]
        void Add(string name, object obj);

        void Add(string name, int obj);

        void Add(string name, string obj = "none");

        StringValues StringValuesIdentityFunc(StringValues input);

        [Duck(Name = "CustomStringIdentityFunc", ParameterTypeNames = new[] { "Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions.CustomString, Datadog.Trace.DuckTyping.Tests" })]
        CustomString CustomStringIdentityFunc_StringArg(string input);

        [Duck(Name = "CustomStringIdentityFunc", ParameterTypeNames = new[] { "Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions.CustomString, Datadog.Trace.DuckTyping.Tests" })]
        CustomString CustomStringIdentityFunc_StringValuesArg(StringValues input);

        [Duck(Name = "StringIdentityFunc", ParameterTypeNames = new[] { "System.String" })]
        string StringIdentityFunc_StringValuesArg(StringValues input);

        [Duck(Name = "StringValuesIdentityFunc", ParameterTypeNames = new[] { "Microsoft.Extensions.Primitives.StringValues, Microsoft.Extensions.Primitives" })]
        StringValues StringValuesIdentityFunc_StringArg(string input);

        void Pow2(ref int value);

        void GetOutput(out int value);

        [Duck(Name = "GetOutput")]
        void GetOutputObject(out object value);

        bool TryGetObscure(out IDummyFieldObject obj);

        [Duck(Name = "TryGetObscure")]
        bool TryGetObscureObject(out object obj);

        void GetReference(ref int value);

        [Duck(Name = "GetReference")]
        void GetReferenceObject(ref object value);

        bool TryGetReference(ref IDummyFieldObject obj);

        [Duck(Name = "TryGetReference")]
        bool TryGetReferenceObject(ref object obj);

        bool TryGetPrivateObscure(out IDummyFieldObject obj);

        [Duck(Name = "TryGetPrivateObscure")]
        bool TryGetPrivateObscureObject(out object obj);

        bool TryGetPrivateReference(ref IDummyFieldObject obj);

        [Duck(Name = "TryGetPrivateReference")]
        bool TryGetPrivateReferenceObject(ref object obj);

        IDummyFieldObject Bypass(IDummyFieldObject obj);
    }
}
