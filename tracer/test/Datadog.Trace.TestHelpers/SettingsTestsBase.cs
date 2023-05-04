// <copyright file="SettingsTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;

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

        public static IEnumerable<object[]> BooleanTestCases(bool? defaultValue)
        {
            yield return new object[] { "true", true };
            yield return new object[] { "1", true };
            yield return new object[] { "false", false };
            yield return new object[] { "0", false };
            yield return new object[] { "A", defaultValue };
            yield return new object[] { null, defaultValue };
            yield return new object[] { string.Empty, defaultValue };
        }

        public static IEnumerable<object[]> Int32TestCases(int defaultValue)
        {
            yield return new object[] { "1", 1 };
            yield return new object[] { "0", 0 };
            yield return new object[] { "-1", -1 };
            yield return new object[] { "A", defaultValue };
            yield return new object[] { null, defaultValue };
            yield return new object[] { string.Empty, defaultValue };
        }

        public static IEnumerable<object[]> DoubleTestCases()
        {
            yield return new object[] { "1.5", 1.5d };
            yield return new object[] { "1", 1.0d };
            yield return new object[] { "0", 0.0d };
            yield return new object[] { "-1", -1.0d };
            yield return new object[] { "A", null };
            yield return new object[] { null, null };
            yield return new object[] { string.Empty, null };
        }

        public static IEnumerable<object[]> StringTestCases()
        {
            yield return new object[] { "test", "test" };
            yield return new object[] { null, null };
            yield return new object[] { string.Empty, string.Empty };
        }

        public static IEnumerable<object[]> StringTestCases(string defaultValue, Strings emptyStringBehavior)
        {
            yield return new object[] { "test", "test" };
            yield return new object[] { null, defaultValue };
            yield return new object[] { string.Empty, emptyStringBehavior == Strings.AllowEmpty ? string.Empty : defaultValue };
        }

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
