// <copyright file="GetCustomAttributesCrashTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Bugs;

public class GetCustomAttributesCrashTests : Samples.InstrumentedTests.Iast.InstrumentationTestsBase
{
    public GetCustomAttributesCrashTests()
    {
    }

    [Fact]
    public void GivenACustomAttributeSelfTagged_WhenInstrumented_NoCrashHappens()
    {
        _ = MyCustomClass.Test(); // This made the process crash before the fix
    }

    class MyCustomAttribute : Attribute
    {
        [MyCustom]
        public MyCustomAttribute() { }

        [MyCustom]
        public void TestMethod() { }
    }

    static class MyCustomClass
    {
        [MyCustom]
        public static string Test()
        { 
            return GetString() + GetString();
        }

        public static string GetString()
        {
            return "String";
        }
    }
}
