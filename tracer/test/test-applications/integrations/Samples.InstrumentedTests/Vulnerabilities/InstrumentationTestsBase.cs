// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class InstrumentationTestsBase
{
    private object _iastRequestContext;
    private object _traceContext;
    private object _taintedObjects;
    private static readonly Type _taintedObjectsType = Type.GetType("Datadog.Trace.Iast.TaintedObjects, Datadog.Trace");
    private static readonly Type _taintedObjectType = Type.GetType("Datadog.Trace.Iast.TaintedObject, Datadog.Trace");
    private static readonly Type _iastRequestContextType = Type.GetType("Datadog.Trace.Iast.IastRequestContext, Datadog.Trace");
    private static readonly Type _scopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
    private static readonly Type _spanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
    private static readonly Type _arrayBuilderType = Type.GetType("Datadog.Trace.Util.ArrayBuilder`1, Datadog.Trace");
    private static readonly Type _arrayBuilderOfSpanType = _arrayBuilderType.MakeGenericType(new Type[] { _spanType });
    private static readonly Type _spanContextType = Type.GetType("Datadog.Trace.SpanContext, Datadog.Trace");
    private static readonly Type _traceContextType = Type.GetType("Datadog.Trace.TraceContext, Datadog.Trace");
    private static readonly Type _sourceType = Type.GetType("Datadog.Trace.Iast.Source, Datadog.Trace");
    private static readonly Type _vulnerabilityType = Type.GetType("Datadog.Trace.Iast.Vulnerability, Datadog.Trace");
    private static readonly Type _rangeType = Type.GetType("Datadog.Trace.Iast.Range, Datadog.Trace");
    private static readonly Type _vulnerabilityBatchType = Type.GetType("Datadog.Trace.Iast.VulnerabilityBatch, Datadog.Trace");
    private static readonly Type _evidenceType = Type.GetType("Datadog.Trace.Iast.Evidence, Datadog.Trace");
    private static MethodInfo _spanProperty = _scopeType.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _contextProperty = _spanType.GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _setSpanTypeProperty = _spanType.GetProperty("Type", BindingFlags.NonPublic | BindingFlags.Instance)?.SetMethod;
    private static MethodInfo _traceContextProperty = _spanContextType.GetProperty("TraceContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _iastRequestContextProperty = _traceContextType.GetProperty("IastRequestContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _operationNameProperty = _spanType.GetProperty("OperationName", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _rangesProperty = _taintedObjectType.GetProperty("Ranges", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _StartProperty = _rangeType.GetProperty("Start", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _LengthProperty = _rangeType.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _lengthProperty = _rangeType.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _vulnerabilitiesProperty = _vulnerabilityBatchType.GetProperty("Vulnerabilities", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _vulnerabilityTypeProperty = _vulnerabilityType.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _evidenceProperty = _vulnerabilityType.GetProperty("Evidence", BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _getTaintedObjectsMethod = _taintedObjectsType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
    private static MethodInfo _taintInputStringMethod = _taintedObjectsType.GetMethod("TaintInputString", BindingFlags.Instance | BindingFlags.Public);
    private static MethodInfo _taintMethod = _taintedObjectsType.GetMethod("Taint", BindingFlags.Instance | BindingFlags.Public);
    private static MethodInfo _enableIastInRequestMethod = _traceContextType.GetMethod("EnableIastInRequest", BindingFlags.Instance | BindingFlags.NonPublic);
    private static MethodInfo _getArrayMethod = _arrayBuilderOfSpanType.GetMethod("GetArray");
    private static MethodInfo _spanGetTagMethod = _spanType.GetMethod("GetTag", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo _taintedObjectsField = _iastRequestContextType.GetField("_taintedObjects", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo _spansField = _traceContextType.GetField("_spans", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo _vulnerabilityBatchField = _iastRequestContextType.GetField("_vulnerabilityBatch", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo _evidenceValueField = _evidenceType.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
    
    protected static string WeakHashVulnerabilityType = "WEAK_HASH";

    public InstrumentationTestsBase()
    {
        AssertInstrumented();
        var scope = SampleHelpers.GetActiveScope();
        scope.Should().NotBeNull();
        var span = _spanProperty.Invoke(scope, Array.Empty<object>());
        span.Should().NotBeNull();
        _setSpanTypeProperty.Invoke(span, new object[] { "web" });
        var context = _contextProperty.Invoke(span, Array.Empty<object>());
        context.Should().NotBeNull();
        _traceContext = _traceContextProperty.Invoke(context, Array.Empty<object>());
        _enableIastInRequestMethod.Invoke(_traceContext, Array.Empty<object>());
        _iastRequestContext = _iastRequestContextProperty.Invoke(_traceContext, Array.Empty<object>());
        _taintedObjects = _taintedObjectsField.GetValue(_iastRequestContext);
        _taintedObjects.Should().NotBeNull();
    }

    protected string AddTaintedString(string tainted)
    {
        return (string)AddTainted(tainted);
    }

    protected object AddTainted(object tainted)
    {
        var source = Activator.CreateInstance(_sourceType, new object[] { (byte)0, (string)null, tainted.ToString() });
        var defaultRange = Activator.CreateInstance(_rangeType, new object[] { 0, tainted.ToString().Length, source });
        var rangeArray = Array.CreateInstance(_rangeType, 1);
        rangeArray.SetValue(defaultRange, 0);
        _taintMethod.Invoke(_taintedObjects, new object[] { tainted, rangeArray });
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
        spans = spans.Where(x => (string)_operationNameProperty.Invoke(x, Array.Empty<object>()) == operationName).ToList();
        spansGenerated.Should().Be(spans.Count);
    }

    protected void AssertVulnerable(int vulnerabilities = 1)
    {
        var vulnerabilityList = GetGeneratedVulnerabilities();
        vulnerabilityList.Count.Should().Be(vulnerabilities);
    }

    protected void AssertVulnerable(string expectedType, string expectedEvidence = "", bool evidenceTainted = false)
    {
        var vulnerabilityList = GetGeneratedVulnerabilities();
        vulnerabilityList.Count.Should().Be(1);
        var vulnerabilityType = _vulnerabilityTypeProperty.Invoke(vulnerabilityList[0], Array.Empty<object>());
        vulnerabilityType.Should().Be(expectedType);
        var evidence = _evidenceProperty.Invoke(vulnerabilityList[0], Array.Empty<object>());
        var evidenceValue = _evidenceValueField.GetValue(evidence);
        if (!string.IsNullOrEmpty(expectedEvidence))
        {
            if (evidenceTainted)
            {
                FormatTainted(evidenceValue).Should().Be(expectedEvidence);
            }
            else
            {
                evidenceValue.Should().Be(expectedEvidence);
            }
        }
    }

    protected void AssertVulnerable(string expectedType, string expectedEvidence = "")
    {
        var vulnerabilityList = GetGeneratedVulnerabilities();
        vulnerabilityList.Count.Should().Be(1);
        var vulnerabilityType = _vulnerabilityTypeProperty.Invoke(vulnerabilityList[0], Array.Empty<object>());
        vulnerabilityType.Should().Be(expectedType);
        var evidence = _evidenceProperty.Invoke(vulnerabilityList[0], Array.Empty<object>());
        var evidenceValue = _evidenceValueField.GetValue(evidence);
        if (!string.IsNullOrEmpty(expectedEvidence))
        {
            FormatTainted(evidenceValue).Should().Be(expectedEvidence);
        }
    }

    protected void AssertNotVulnerable()
    {
        AssertVulnerable(0);
    }

    private List<object> GetGeneratedVulnerabilities()
    {
        var vulnerabilityBatchField = _vulnerabilityBatchField.GetValue(_iastRequestContext);

        if (vulnerabilityBatchField == null)
        {
            return new List<object>();
        }

        var vulnerabilities = _vulnerabilitiesProperty.Invoke(vulnerabilityBatchField, Array.Empty<object>());
        return ((vulnerabilities as IEnumerable).Cast<object>()).ToList();
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

    protected void AssertNotTaintedWithOriginalCallCheck(string instrumented, Expression<Func<Object>> notInstrumented)
    {
        AssertNotTainted(instrumented);
        var notInstrumentedCompiled = notInstrumented.Compile();
        var notInstrumentedResult = ExecuteFunc(notInstrumentedCompiled);
        instrumented.Should().Be(notInstrumentedResult.ToString());
    }

    protected void AssertTaintedFormatWithOriginalCallCheck(object expected, object instrumented, Expression<Func<Object>> notInstrumented)
    {
        AssertTainted(instrumented);
        FormatTainted(instrumented).Should().Be(expected.ToString());
        var notInstrumentedCompiled = notInstrumented.Compile();
        var notInstrumentedResult = ExecuteFunc(notInstrumentedCompiled);
        instrumented.ToString().Should().Be(notInstrumentedResult.ToString());
    }

    protected void AssertUntaintedWithOriginalCallCheck(object expected, object instrumented, Expression<Func<Object>> notInstrumented)
    {
        instrumented.ToString().Should().Be(expected.ToString());
        var notInstrumentedCompiled = notInstrumented.Compile();
        var notInstrumentedResult = ExecuteFunc(notInstrumentedCompiled);
        instrumented.ToString().Should().Be(notInstrumentedResult.ToString());
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

    protected string FormatTainted(object value)
    {
        AssertTainted(value);
        string result = value.ToString();
        var tainted = GetTainted(value);
        var ranges = _rangesProperty.Invoke(tainted, Array.Empty<object>()) as Array;

        List<object> rangesList = new List<object>();

        foreach (var range in ranges)
        {
            rangesList.Add(range);
        }

        rangesList.Reverse();

        foreach (var range in rangesList)
        {
            var start = (int)_StartProperty.Invoke(range, Array.Empty<object>());
            result = result.Insert(start + (int)_lengthProperty.Invoke(range, Array.Empty<object>()), "-+:");
            result = result.Insert(start, ":+-");
        }

        return result;
    }

    protected void ValidateRanges(object value)
    {
        AssertTainted(value);
        string result = value.ToString();
        var tainted = GetTainted(value);
        var ranges = _rangesProperty.Invoke(tainted, Array.Empty<object>()) as Array;

        foreach (var range in ranges)
        {
            var start = (int)_StartProperty.Invoke(range, Array.Empty<object>());
            var length = (int)_LengthProperty.Invoke(range, Array.Empty<object>());
            (start + length).Should().BeLessThanOrEqualTo(result.Length);
        }
    }
}
