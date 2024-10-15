// <copyright file="WafErrorsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafErrorsTests : WafLibraryRequiredTest
    {
        [SkippableTheory]
        [InlineData(@"{""missing key 'name'"":[""crs-913-110"",""crs-913-120"",""crs-920-260""],""missing key 'tags'"":[""crs-921-110"",""crs-921-140""]}", "wrong-tags-name-rule-set.json", 5)]
        [InlineData("{\"missing key 'tags'\":[\"crs-913-110\",\"crs-913-120\",\"crs-920-260\",\"crs-921-110\",\"crs-921-140\",\"crs-941-300\"]}", "wrong-tags-rule-set.json", 6)]
        public void HasErrors(string errorMessage, string filename, ushort failedtoLoadRules)
        {
            var initResult = Waf.Create(WafLibraryInvoker!, string.Empty, string.Empty, filename);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            initResult.Success.Should().BeTrue();
            initResult.LoadedRules.Should().BeGreaterThan(0);
            initResult.FailedToLoadRules.Should().Be(failedtoLoadRules);
            initResult.Errors.Should().NotBeEmpty();
            initResult.HasErrors.Should().BeTrue();
            initResult.ErrorMessage.Should().Be(errorMessage);
        }

        [SkippableFact]
        public void HasNoError()
        {
            var initResult = Waf.Create(WafLibraryInvoker!, string.Empty, string.Empty);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            initResult.Success.Should().BeTrue();
            initResult.FailedToLoadRules.Should().Be(0);
            initResult.LoadedRules.Should().Be(158);
            initResult.Errors.Should().BeEmpty();
            initResult.HasErrors.Should().BeFalse();
            initResult.ErrorMessage.Should().BeNullOrEmpty();
        }

        [SkippableFact]
        public void FileNotFound()
        {
            var initResult = Waf.Create(WafLibraryInvoker!, string.Empty, string.Empty, "unexisting-rule-set.json");
            using var waf = initResult.Waf;
            waf.Should().BeNull();
            initResult.Success.Should().BeFalse();
            initResult.FailedToLoadRules.Should().Be(0);
            initResult.LoadedRules.Should().Be(0);
            initResult.UnusableRuleFile.Should().BeTrue();
        }
    }
}
