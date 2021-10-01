// <copyright file="IntegrationSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Configuration
{
    public class IntegrationSettingsTests
    {
        [TestCase("DD_TRACE_FOO_ENABLED", "true", true)]
        [TestCase("DD_TRACE_FOO_ENABLED", "false", false)]
        [TestCase("DD_FOO_ENABLED", "true", true)]
        [TestCase("DD_FOO_ENABLED", "false", false)]
        public void IntegrationEnabled(string settingName, string settingValue, bool expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
                                                          {
                                                              { settingName, settingValue }
                                                          });

            var settings = new IntegrationSettings("FOO", source);
            Assert.AreEqual(expected, settings.Enabled);
        }

        [TestCase("DD_TRACE_FOO_ANALYTICS_ENABLED", "true", true)]
        [TestCase("DD_TRACE_FOO_ANALYTICS_ENABLED", "false", false)]
        [TestCase("DD_FOO_ANALYTICS_ENABLED", "true", true)]
        [TestCase("DD_FOO_ANALYTICS_ENABLED", "false", false)]
        public void IntegrationAnalyticsEnabled(string settingName, string settingValue, bool expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
                                                          {
                                                              { settingName, settingValue }
                                                          });

            var settings = new IntegrationSettings("FOO", source);
            Assert.AreEqual(expected, settings.AnalyticsEnabled);
        }

        [TestCase("DD_TRACE_FOO_ANALYTICS_SAMPLE_RATE", "0.2", 0.2)]
        [TestCase("DD_FOO_ANALYTICS_SAMPLE_RATE", "0.6", 0.6)]
        public void IntegrationAnalyticsSampleRate(string settingName, string settingValue, double expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
                                                          {
                                                              { settingName, settingValue }
                                                          });

            var settings = new IntegrationSettings("FOO", source);
            Assert.AreEqual(expected, settings.AnalyticsSampleRate);
        }
    }
}
