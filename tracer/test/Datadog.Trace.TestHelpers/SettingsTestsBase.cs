// <copyright file="SettingsTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.TestHelpers
{
    public abstract class SettingsTestsBase
    {
        public static IEnumerable<object[]> BooleanTestCases(bool defaultValue)
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

        public static IEnumerable<object[]> StringTestCases()
        {
            yield return new object[] { "test", "test" };
            yield return new object[] { null, null };
            yield return new object[] { string.Empty, string.Empty };
        }

        public static IEnumerable<object[]> StringTestCases(string defaultValue, bool allowEmpty)
        {
            yield return new object[] { "test", "test" };
            yield return new object[] { null, defaultValue };
            yield return new object[] { string.Empty, allowEmpty ? string.Empty : defaultValue };
        }
    }
}
