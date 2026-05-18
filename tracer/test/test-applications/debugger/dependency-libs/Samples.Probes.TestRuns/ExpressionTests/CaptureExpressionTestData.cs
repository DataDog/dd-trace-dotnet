namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal static class CaptureExpressionTestData
    {
        internal const string BasicExpressionsJson = @"[
    {
        ""name"": ""inputValue"",
        ""expr"": {
            ""dsl"": ""inputValue"",
            ""json"": { ""ref"": ""inputValue"" }
        },
        ""capture"": { ""maxReferenceDepth"": 1 }
    },
    {
        ""name"": ""localValue"",
        ""expr"": {
            ""dsl"": ""localValue"",
            ""json"": { ""ref"": ""localValue"" }
        },
        ""capture"": { ""maxReferenceDepth"": 1 }
    },
    {
        ""name"": ""testStruct"",
        ""expr"": {
            ""dsl"": ""testStruct"",
            ""json"": { ""ref"": ""testStruct"" }
        },
        ""capture"": { ""maxReferenceDepth"": 2 }
    }
]";

        internal const string ComplexExpressionsJson = @"[
    {
        ""name"": ""collection_first_element"",
        ""expr"": {
            ""dsl"": ""testStruct.Collection[0]"",
            ""json"": { ""index"": [{ ""getmember"": [{ ""ref"": ""testStruct"" }, ""Collection""] }, 0] }
        },
        ""capture"": { ""maxReferenceDepth"": 2 }
    },
    {
        ""name"": ""dictionary_value"",
        ""expr"": {
            ""dsl"": ""testStruct.Dictionary['two']"",
            ""json"": { ""index"": [{ ""getmember"": [{ ""ref"": ""testStruct"" }, ""Dictionary""] }, ""two""] }
        },
        ""capture"": { ""maxReferenceDepth"": 2 }
    },
    {
        ""name"": ""nested_field"",
        ""expr"": {
            ""dsl"": ""testStruct.StringValue"",
            ""json"": { ""getmember"": [{ ""ref"": ""testStruct"" }, ""StringValue""] }
        },
        ""capture"": { ""maxReferenceDepth"": 2 }
    }
]";
    }
}
