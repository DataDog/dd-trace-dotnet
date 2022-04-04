// <copyright file="WafErrorsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafErrorsTests
    {
        [SkippableTheory]
        [InlineData(@"{""missing key 'name'"":[""crs-913-110"",""crs-913-120"",""crs-920-260""],""missing key 'tags'"":[""crs-921-110"",""crs-921-140""]}", "erroneous-rule-set-tags-name-wrong.json", 5)]
        [InlineData("{\"missing key 'tags'\":[\"crs-913-110\",\"crs-913-120\",\"crs-920-260\",\"crs-921-110\",\"crs-921-140\",\"crs-941-300\"]}", "erroneous-rule-set-tags-wrong.json", 6)]
        public void HasErrors(string errorMessage, string filename, ushort failedtoLoadRules)
        {
            using var waf = Waf.Create(filename);
            waf.Should().NotBeNull();
            waf.InitializedSuccessfully.Should().BeTrue();
            waf.InitializationResult.LoadedRules.Should().BeGreaterThan(0);
            waf.InitializationResult.FailedToLoadRules.Should().Be(failedtoLoadRules);
            waf.InitializationResult.Errors.Should().NotBeEmpty();
            waf.InitializationResult.HasErrors.Should().BeTrue();
            waf.InitializationResult.ErrorMessage.Should().Be(errorMessage);
        }

        [SkippableFact]
        public void HasNoError()
        {
            using var waf = Waf.Create();
            waf.Should().NotBeNull();
            waf.InitializedSuccessfully.Should().BeTrue();
            waf.InitializationResult.FailedToLoadRules.Should().Be(0);
            waf.InitializationResult.LoadedRules.Should().Be(125);
            waf.InitializationResult.Errors.Should().BeEmpty();
            waf.InitializationResult.HasErrors.Should().BeFalse();
            waf.InitializationResult.ErrorMessage.Should().BeNullOrEmpty();
        }

        [SkippableFact]
        public void FileNotFound()
        {
            using var waf = Waf.Create("unexisting-rule-set.json");
            waf.Should().NotBeNull();
            waf.InitializedSuccessfully.Should().BeFalse();
            waf.InitializationResult.FailedToLoadRules.Should().Be(0);
            waf.InitializationResult.LoadedRules.Should().Be(0);
            waf.InitializationResult.Errors.Should().BeEmpty();
            waf.InitializationResult.HasErrors.Should().BeFalse();
            waf.InitializationResult.ErrorMessage.Should().BeNullOrEmpty();
        }
    }
}
