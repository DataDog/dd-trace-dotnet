using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace;
using MessagePack.Formatters;
using Microsoft.AspNetCore.Mvc;

namespace Samples.Security.AspNetCore5;

[Route("[controller]")]
[ApiController]
public class MetaStructController : ControllerBase
{
    private static readonly Type _tracerType = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace");
    private static readonly Type _scopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
    private static MethodInfo _spanProperty = _scopeType.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static readonly Type _spanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
    private static MethodInfo _tagsProperty = _spanType.GetProperty("Tags", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static readonly Type _tagsListType = Type.GetType("Datadog.Trace.Tagging.TagsList, Datadog.Trace");        
    private static MethodInfo _AddMetaStructMethod = _tagsListType.GetMethod("SetMetaStruct", BindingFlags.Public | BindingFlags.Instance);
    private static MethodInfo _internalActiveScopeProperty = _tracerType.GetProperty("InternalActiveScope", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _rootProperty = _scopeType.GetProperty("Root", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
    private static MethodInfo _setTagMethod = _spanType.GetMethod("SetTag", BindingFlags.Instance | BindingFlags.NonPublic);
   
    // This code is used to test the MetaStruct feature in the UI

    const string stackId = "1212121";
    List<object> stack =new List<object>()
            {
                new Dictionary<string, object>()
                {
                    { "type", "type" },
                    { "language", "dotnet" },
                    { "id", stackId },
                    {
                        "frames", new List<object>()
                        {
                            new Dictionary<string, object>()
                            {
                                { "id", "1" },
                                { "text", "text" },
                                { "file", "file.cs" },
                                { "line", 33U },
                                { "namespace", "testnamespace" },
                                { "class_name", "class1" },
                                { "function", "method1" },
                            },
                            new Dictionary<string, object>()
                            {
                                { "id", "2" },
                                { "text", "text2" },
                                { "file", "file2.cs" },
                                { "line", 55U },
                                { "namespace", "testnamespace" },
                                { "class_name", "class2" },
                                { "function", "method2" },
                            }
                        }
                    }
                },
                new Dictionary<string, object>()
                {
                { "type", "type2222" },
                { "language", "dotnet" },
                { "id", "test55555" },
                {
                    "frames", new List<object>()
                    {
                        new Dictionary<string, object>()
                        {
                            { "id", "frameid" },
                            { "text", "text" },
                            { "file", "file.cs" },
                            { "line", 33U },
                        },
                        new Dictionary<string, object>()
                        {
                            { "id", "frameid2" },
                            { "text", "text2" },
                            { "file", "file2.cs" },
                            { "line", 55U },
                        }
                    }
                }
                }
            };

    [HttpGet("MetaStructTest")]
    public IActionResult MetaStructTest()
    {
        var tracerInstance = Tracer.Instance;
        var internalActiveScope = _internalActiveScopeProperty.Invoke(tracerInstance, null);
        var root = _rootProperty.Invoke(internalActiveScope, null);
        var rootSpan = _spanProperty.Invoke(root, null);
        var tags = _tagsProperty.Invoke(rootSpan, null);
        if (rootSpan != null)
        {
            _AddMetaStructMethod.Invoke(tags, ["_dd.stack.exploit", ObjectToByteArray(stack)]);
            _setTagMethod.Invoke(rootSpan, ["_dd.appsec.json", "{\"triggers\":[{\"stack_id\": \"" + stackId + "\",\"rule\":{\"id\":\"rasp-001-001\",\"name\":\"Path traversal attack\",\"tags\":{\"category\":\"vulnerability_trigger\",\"type\":\"lfi\"}},\"rule_matches\":[{\"operator\":\"lfi_detector\",\"operator_value\":null,\"parameters\":[{\"address\":null,\"highlight\":[\"/etc/password\"],\"key_path\":null,\"value\":null}]}]}]}"]);
            _setTagMethod.Invoke(rootSpan, ["_dd.origin", "appsec"]);
            _setTagMethod.Invoke(rootSpan, ["appsec.blocked", "true"]);
            _setTagMethod.Invoke(rootSpan, ["appsec.event", "true"]);
        }

        return Content($"test Launched\n");
    }

    [HttpGet("VulnWithoutMetaStructTest")]
    public IActionResult VulnWithoutMetaStructTest()
    {
        var tracerInstance = Tracer.Instance;
        var internalActiveScope = _internalActiveScopeProperty.Invoke(tracerInstance, null);
        var root = _rootProperty.Invoke(internalActiveScope, null);
        var rootSpan = _spanProperty.Invoke(root, null);
        var tags = _tagsProperty.Invoke(rootSpan, null);
        if (rootSpan != null)
        {
            _setTagMethod.Invoke(rootSpan, ["_dd.appsec.json", "{\"triggers\":[{\"rule\":{\"id\":\"rasp-001-001\",\"name\":\"Path traversal attack\",\"tags\":{\"category\":\"vulnerability_trigger\",\"type\":\"lfi\"}},\"rule_matches\":[{\"operator\":\"lfi_detector\",\"operator_value\":null,\"parameters\":[{\"address\":null,\"highlight\":[\"/etc/password\"],\"key_path\":null,\"value\":null}]}]}]}"]);
            _setTagMethod.Invoke(rootSpan, ["_dd.origin", "appsec"]);
            _setTagMethod.Invoke(rootSpan, ["appsec.blocked", "true"]);
            _setTagMethod.Invoke(rootSpan, ["appsec.event", "true"]);
        }

        return Content($"test Launched\n");
    }

    private static byte[] ObjectToByteArray(object value)
    {
        // 256 is the size that the serializer would reserve initially for empty arrays, so we create
        // the buffer with that size to avoid this first resize. If a bigger size is required later, the serializer
        // will resize it.

        var buffer = new byte[256];
        var bytesCopied = PrimitiveObjectFormatter.Instance.Serialize(ref buffer, 0, value, null);
        Array.Resize(ref buffer, bytesCopied);

        return buffer;
    }
}
