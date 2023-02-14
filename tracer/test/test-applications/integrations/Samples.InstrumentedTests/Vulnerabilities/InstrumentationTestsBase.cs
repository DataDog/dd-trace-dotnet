// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Moq;
using Xunit;

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
        AssertInstrumented();
        SampleHelpers.CreateScope("instrumentationTests");
        var scope = SampleHelpers.GetActiveScope();
        scope.Should().NotBeNull(); 
        var span = _spanProperty.Invoke(scope, Array.Empty<object>());
        span.Should().NotBeNull();
        var context = _contextProperty.Invoke(span, Array.Empty<object>());
        context.Should().NotBeNull();
        _traceContext = _traceContextProperty.Invoke(context, Array.Empty<object>());
        _enableIastInRequestMethod.Invoke(_traceContext, Array.Empty<object>());
        _iastRequestContext = _iastRequestContextProperty.Invoke(_traceContext, Array.Empty<object>());
        _taintedObjects = _taintedObjectsField.GetValue(_iastRequestContext);
        _taintedObjects.Should().NotBeNull();
    }

    protected object AddTainted(object tainted)
    {
        var source = Activator.CreateInstance(_sourceType, new object[] { (byte)0, (string)null, (string)tainted });        
        _taintInputStringMethod.Invoke(_taintedObjects, new object[] { tainted, source });
        return tainted;
    }

    protected void AssertTainted(object tainted)
    {
        GetTainted(tainted).Should().NotBeNull(tainted.ToString() + " is not tainted.");
    }

    private object GetTainted(object tainted)
    {
        return _getTaintedObjectsMethod.Invoke(_taintedObjects, new object[] { tainted });
    }

    protected void AssertNotTainted(string value)
    {
        GetTainted(value).Should().BeNull(value + " is tainted.");
    }

    protected void AssertInstrumented()
    {
        SampleHelpers.IsProfilerAttached().Should().BeTrue();
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

    protected void AssertTaintedFormatWithOriginalCallCheck(string expected, string instrumented, Expression<Func<Object>> notInstrumented)
    {
        AssertTainted(instrumented);
        Format(instrumented).Should().Be(expected);
        var notInstrumentedCompiled = notInstrumented.Compile();
        var notInstrumentedResult = ExecuteFunc(notInstrumentedCompiled);
        instrumented.Should().Be(notInstrumentedResult.ToString());
    }

    private static object ExecuteFunc(Func<Object> function)
    {
        try
        {
            var result = function.Invoke();
            return result;
        }
        catch (Exception ex)
        {
            return ex.GetType();
        }
    }

    private static bool AlreadyInstrumented(Expression expression)
    {
        if (expression is MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.DeclaringType.FullName.Contains("Hdiv"))
            {
                return true;
            }
            foreach (var argument in methodCallExpression.Arguments)
            {
                if (AlreadyInstrumented(argument))
                {
                    return true;
                }
            }
        }

        return false;
    }

    protected string Format(object value)
    {
        AssertTainted(value);
        string result = value.ToString();
        var tainted = GetTainted(value);
        var ranges = ((Datadog.Trace.Iast.TaintedObject)tainted).Ranges.ToList();

        var ranges2 

        var rangesOrdered = ranges.OrderByDescending(x => x.Start);

        foreach(var range in rangesOrdered)
        {
            result = result.Insert(range.Start + range.Length, "-+:");
            result = result.Insert(range.Start, ":+-");            
        }

        return result;
    }

}
