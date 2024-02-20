using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Json.Newtonsoft.Json;

public class ParseTests : InstrumentationTestsBase
{
    private readonly string _taintedJson = "{ \"key\": \"value\" }";
    private readonly string _taintedJsonMultiple = "{\"key1\": \"value1\", \"key2\": \"value2\"}";
    private readonly string _taintedJsonObjectWithArray = "{\"key\": [\"value1\", \"value2\"]}";
    private readonly string _taintedJsonArray = "[\"value1\", \"value2\"]";
    private readonly string _taintedJsonDeepObject = "{\"key\": {\"key2\": \"value\"}}";

    public ParseTests()
    {
        // Add all tainted values
        AddTainted(_taintedJson);
        AddTainted(_taintedJsonMultiple);
        AddTainted(_taintedJsonObjectWithArray);
        AddTainted(_taintedJsonArray);
        AddTainted(_taintedJsonDeepObject);
    }

    [Fact]
    public void GivenASimpleJSON_WhenParsing_Value_Vulnerable()
    {
        var json = JObject.Parse(_taintedJson);
        var keyStr = json.Value<string>("key");
        AssertTainted(keyStr);
    }
    
    [Fact]
    public void GivenASimpleJSON_WhenParsing_TryGetValue_Vulnerable()
    {
        var json = JObject.Parse(_taintedJson);
        var keyStr = json.TryGetValue("key", out var value) ? value.ToString() : null;
        AssertTainted(keyStr);
    }
    
    [Fact]
    public void GivenASimpleJSON_WhenParsing_ArrayAccess_Vulnerable()
    {
        var json = JObject.Parse(_taintedJson);
        var keyStr = json["key"]?.ToString();
        AssertTainted(keyStr);
    }

    [Fact]
    public void GivenObjectArrayJSON_WhenParsing_MultipleValues_Vulnerable()
    {
        var json = JObject.Parse(_taintedJsonObjectWithArray);
        json.TryGetValue("key", out var value);
        json.Value<object[]>(value);
        // TODO: Chercher comment les gens ils font sur internet pour get une string dans un array json
        var val1 = json["key"]?[0]?.ToString();
        var val2 = json["key"]?[1]?.ToString();
        AssertTainted(val1);
        AssertTainted(val2);
    }
}
