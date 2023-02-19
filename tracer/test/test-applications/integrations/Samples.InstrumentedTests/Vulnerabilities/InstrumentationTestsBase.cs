// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class InstrumentationTestsBase
{
    private object _iastRequestContext;
    private object _traceContext;
    private object _taintedObjects;
    private static readonly Type _taintedObjectsType = Type.GetType("Datadog.Trace.Iast.TaintedObjects, Datadog.Trace");
    private static readonly Type _iastRequestContextType = Type.GetType("Datadog.Trace.Iast.IastRequestContext, Datadog.Trace");
    private static readonly Type _scopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
    private static readonly Type _spanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
    private static readonly Type _arrayBuilderType = Type.GetType("Datadog.Trace.Util.ArrayBuilder`1, Datadog.Trace");
    private static readonly Type _arrayBuilderOfSpanType = _arrayBuilderType.MakeGenericType(new Type[] { _spanType });
    private static readonly Type _spanContextType = Type.GetType("Datadog.Trace.SpanContext, Datadog.Trace");
    private static readonly Type _traceContextType = Type.GetType("Datadog.Trace.TraceContext, Datadog.Trace");
    private static readonly Type _sourceType = Type.GetType("Datadog.Trace.Iast.Source, Datadog.Trace");
    private static MethodInfo _spanProperty = _scopeType.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _contextProperty = _spanType.GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _traceContextProperty = _spanContextType.GetProperty("TraceContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _iastRequestContextProperty = _traceContextType.GetProperty("IastRequestContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _operationNameProperty = _spanType.GetProperty("OperationName", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _getTaintedObjectsMethod = _taintedObjectsType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
    private static MethodInfo _taintInputStringMethod = _taintedObjectsType.GetMethod("TaintInputString", BindingFlags.Instance | BindingFlags.Public);
    private static MethodInfo _enableIastInRequestMethod = _traceContextType.GetMethod("EnableIastInRequest", BindingFlags.Instance | BindingFlags.NonPublic);
    private static MethodInfo _getArrayMethod = _arrayBuilderOfSpanType.GetMethod("GetArray");
    private static MethodInfo _spanGetTagMethod = _spanType.GetMethod("GetTag", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo _taintedObjectsField = _iastRequestContextType.GetField("_taintedObjects", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo _spansField = _traceContextType.GetField("_spans", BindingFlags.NonPublic | BindingFlags.Instance);

    public InstrumentationTestsBase()
    {
        SampleHelpers.CreateScope("instrumentationTests");
        var scope = SampleHelpers.GetActiveScope();
        var span = _spanProperty.Invoke(scope, Array.Empty<object>());
        var context = _contextProperty.Invoke(span, Array.Empty<object>());
        _traceContext = _traceContextProperty.Invoke(context, Array.Empty<object>());
        AssertInstrumented();
        _enableIastInRequestMethod.Invoke(_traceContext, Array.Empty<object>());
        _iastRequestContext = _iastRequestContextProperty.Invoke(_traceContext, Array.Empty<object>());
        _taintedObjects = _taintedObjectsField.GetValue(_iastRequestContext);
    }

    protected void AddTainted(string tainted)
    {
        var source = Activator.CreateInstance(_sourceType, new object[] { (byte)0, (string)null, (string)tainted });        
        _taintInputStringMethod.Invoke(_taintedObjects, new object[] { tainted, source });
    }

    protected void AssertTainted(string tainted)
    {
        GetTainted(tainted).Should().NotBeNull();
    }

    private object GetTainted(string tainted)
    {
        return _getTaintedObjectsMethod.Invoke(_taintedObjects, new object[] { tainted });
    }

    protected void AssertNotTainted(string value)
    {
        GetTainted(value).Should().BeNull();
    }

    protected void AssertInstrumented()
    {
        SampleHelpers.IsProfilerAttached().Should().BeTrue(because: GetErrorMessage(AppDomain.CurrentDomain.GetAssemblies()));
    }

    protected void AssertSpanGenerated(string operationName, int spansGenerated = 1)
    {
        var spans = GetGeneratedSpans(_traceContext);
        spans = spans.Where(x => (string) _operationNameProperty.Invoke(x, Array.Empty<object>()) == operationName).ToList();
        spansGenerated.Should().Be(spans.Count);
    }

    protected void AssertVulnerable(int vulnerabilities = 1)
    {
        var spans = GetGeneratedSpans(_traceContext);
        GetIastSpansCount(spans).Should().Be(vulnerabilities);
    }

    protected void AssertNotVulnerable()
    {
        AssertVulnerable(0);
    }

    private static string GetErrorMessage(Assembly[] assemblies)
    {
        var assemblyListString = "DD Assemblies:" + Environment.NewLine + string.Join(Environment.NewLine, assemblies.Where(x => x.GetName().Name.IndexOf("datadog", StringComparison.OrdinalIgnoreCase) >= 0).Select(x => x.GetName().Name));

        return "Test is not instrumented." + Environment.NewLine +
            EnvironmentVariableMessage("CORECLR_ENABLE_PROFILING") +
            EnvironmentVariableMessage("CORECLR_PROFILER_PATH") + EnvironmentVariableMessage("CORECLR_PROFILER_PATH_64") + EnvironmentVariableMessage("CORECLR_PROFILER_PATH_32") +
            EnvironmentVariableMessage("COR_ENABLE_PROFILING") + EnvironmentVariableMessage("COR_PROFILER_PATH_32") +
            EnvironmentVariableMessage("COR_PROFILER_PATH") +
            EnvironmentVariableMessage("COR_PROFILER_PATH_64") + EnvironmentVariableMessage("DD_DOTNET_TRACER_HOME") + Environment.NewLine +
            assemblyListString + Environment.NewLine;
    }

    private static string EnvironmentVariableMessage(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return variable + ": " +  (string.IsNullOrEmpty(value) ? "Empty" : value) + Environment.NewLine;
    }

    private int GetIastSpansCount(List<object> spans)
    {
        return spans.Where(x => GetTag(x, "_dd.iast.enabled") != null).Count();
    }

    private object GetTag(object span, string tag)
    {
        return _spanGetTagMethod.Invoke(span, new object[] { tag });
    }

    private List<object> GetGeneratedSpans(object context)
    {
        var spans = new List<object>();
        var spansArray = _spansField.GetValue(context);
        var contextSpans = _getArrayMethod.Invoke(spansArray, Array.Empty<object>()) as IEnumerable<object>;

        foreach (var span in contextSpans)
        {
            spans.Add(span);
        }

        return spans;
    }
}
