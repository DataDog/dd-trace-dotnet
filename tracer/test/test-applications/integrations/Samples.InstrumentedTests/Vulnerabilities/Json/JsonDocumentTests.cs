using System;
using System.Text.Json;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Json;

public class JsonDocumentTests : InstrumentationTestsBase
{
    private readonly string _taintedJson = "{ \"key\": \"value\" }";
    private readonly string _taintedJsonMultiple = "{\"key1\": \"value1\", \"key2\": \"value2\"}";
    private readonly string _taintedJsonArray = "[\"value1\", \"value2\"]";
    private readonly string _taintedJsonDeepObject = "{\"key\": {\"key2\": \"value\"}}";

    public JsonDocumentTests()
    {
        // Add all tainted values
        AddTainted(_taintedJson);
        AddTainted(_taintedJsonMultiple);
        AddTainted(_taintedJsonArray);
        AddTainted(_taintedJsonDeepObject);
    }

    [Fact]
    public void Parse_ShouldTaintStringValues()
    {
        var json = JsonDocument.Parse(_taintedJson, new JsonDocumentOptions());
        var element = json.RootElement;
        var value = element.GetProperty("key");
        var str = value.GetString();

        AssertTainted(str);
    }
    
}
