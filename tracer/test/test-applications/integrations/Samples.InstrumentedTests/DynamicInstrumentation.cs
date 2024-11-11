using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Samples.InstrumentedTests.Iast.Vulnerabilities;
using Xunit;

namespace Samples.InstrumentedTests;

public class DynamicInstrumentation: InstrumentationTestsBase
{
    [Fact]
    public void GivenATaintedObject_WhenRequest_ObjectIsTainted()
    {
        Method1();
        AssertNotVulnerable();

        // Instrumentation EnableTracerInstrumentations
        // EnableTracerInstrumentations(InstrumentationCategory categories, Stopwatch sw = null, bool raspEnabled = false)
        // EnableTracerInstrumentations
        var _instrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation, Datadog.Trace");
        var _enableTracerInstrumentations = _instrumentationType.GetMethod("EnableTracerInstrumentations", BindingFlags.Static | BindingFlags.NonPublic);
        _enableTracerInstrumentations.Invoke(null, new object[] { 0x4, null, false });

        Method2();
        Method1();
    }

    private static void Method1()
    {
        try
        {
            File.ReadAllBytes(Guid.NewGuid().ToString());
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private static void Method2()
    {
        try
        {
            File.ReadAllBytes(Guid.NewGuid().ToString());
        }
        catch (Exception)
        {
            // ignored
        }
    }

}
