// <copyright file="SettingsTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.TestHelpers
{
    public abstract class SettingsTestsBase
    {
        public enum Strings
        {
            /// <summary>
            /// Empty string values are accepted
            /// </summary>
            AllowEmpty,

            /// <summary>
            /// Empty string values are replaced by the default value
            /// </summary>
            DisallowEmpty
        }

        public static TheoryData<string, bool?> BooleanTestCases(bool? defaultValue)
            => new TheoryData<string, bool?>
            {
                { "true", true },
                { "1", true },
                { "false", false },
                { "0", false },
                { "A", defaultValue },
                { null, defaultValue },
                { string.Empty, defaultValue },
            };

        public static TheoryData<string, int?> Int32TestCases(int defaultValue)
            => new TheoryData<string, int?>
            {
                { "1", 1 },
                { "0", 0 },
                { "-1", -1 },
                { "A", defaultValue },
                { null, defaultValue },
                { string.Empty, defaultValue }
            };

        public static TheoryData<string, double?> DoubleTestCases(double? defaultValue)
            => new TheoryData<string, double?>
            {
                { "1.5", 1.5d },
                { "1", 1.0d },
                { "0", 0.0d },
                { "-1", -1.0d },
                { "A", defaultValue },
                { null, defaultValue },
                { string.Empty, defaultValue }
            };

        public static TheoryData<string, string> StringTestCases()
            => new TheoryData<string, string>
            {
                { "test", "test" },
                { null, null },
                { string.Empty, string.Empty }
            };

        public static TheoryData<string, string> StringTestCases(string defaultValue, Strings emptyStringBehavior)
            => new TheoryData<string, string>
            {
                { "test", "test" },
                { null, defaultValue },
                { string.Empty, emptyStringBehavior == Strings.AllowEmpty ? string.Empty : defaultValue }
            };

        protected static IConfigurationSource CreateConfigurationSource(params (string Key, string Value)[] values)
        {
            var config = new NameValueCollection();

            foreach (var (key, value) in values)
            {
                config.Add(key, value);
            }

            return new NameValueConfigurationSource(config);
        }
    }
}
