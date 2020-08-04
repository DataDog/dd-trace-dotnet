using Newtonsoft.Json;

namespace Datadog.Trace.MSBuild
{
    internal struct LogItem
    {
        public LogItem(string level, string message, string type, string code, int? lineNumber, int? columnNumber, int? endLineNumber, int? endColumnNumber, string projectFile, string filePath, string stack, string subCategory)
        {
            Level = level;
            Message = message;
            Type = type;
            Code = code;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            EndLineNumber = endLineNumber;
            EndColumnNumber = endColumnNumber;
            ProjectFile = projectFile;
            FilePath = filePath;
            Stack = stack;
            SubCategory = subCategory;
        }

        [JsonProperty(PropertyName = "level", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Level { get; }

        [JsonProperty(PropertyName = "message", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message { get; }

        [JsonProperty(PropertyName = "type", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Type { get; }

        [JsonProperty(PropertyName = "code", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Code { get; }

        [JsonProperty(PropertyName = "lineNumber", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? LineNumber { get; }

        [JsonProperty(PropertyName = "columnNumber", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? ColumnNumber { get; }

        [JsonProperty(PropertyName = "projectFile", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ProjectFile { get; }

        [JsonProperty(PropertyName = "filePath", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FilePath { get; }

        [JsonProperty(PropertyName = "stack", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Stack { get; }

        [JsonProperty(PropertyName = "subCategory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string SubCategory { get; }

        [JsonProperty(PropertyName = "endLineNumber", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? EndLineNumber { get; }

        [JsonProperty(PropertyName = "endColumnNumber", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? EndColumnNumber { get; }
    }
}
