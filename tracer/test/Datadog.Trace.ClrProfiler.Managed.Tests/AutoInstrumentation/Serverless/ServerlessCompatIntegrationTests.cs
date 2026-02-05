// <copyright file="ServerlessCompatIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless;
using Datadog.Trace.ClrProfiler.CallTarget;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Serverless;

public class ServerlessCompatIntegrationTests
{
    [Theory]
    [InlineData(null, "dd_trace")] // Null base name should use default
    [InlineData("custom_pipe", "custom_pipe")] // Custom base name should be used
    [InlineData("short", "short")] // Short name should work
    [InlineData("this_is_a_very_long_pipe_name_that_exceeds_the_maximum_allowed_length_for_windows_named_pipes_which_is_256_characters_total_including_the_prefix_and_we_need_to_make_sure_this_gets_truncated_properly_to_214_characters_before_appending_guid_suffix_extra_padding", "this_is_a_very_long_pipe_name_that_exceeds_the_maximum_allowed_length_for_windows_named_pipes_which_is_256_characters_total_including_the_prefix_and_we_need_to_make_sure_this_gets_truncated_properly_to_214_cha")] // Long name should be truncated to 214 chars
    public void CalculateTracePipeName_GeneratesValidUniquePipeName(string compatLayerValue, string expectedBase)
    {
        // Arrange
        var state = new CallTargetState();

        // Act - First call generates the pipe name
        var result1 = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            instance: null,
            returnValue: compatLayerValue,
            exception: null,
            in state);

        // Second call should return the same cached value
        var result2 = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            instance: null,
            returnValue: compatLayerValue,
            exception: null,
            in state);

        // Assert
        result1.GetReturnValue().Should().NotBeNullOrEmpty();
        result2.GetReturnValue().Should().NotBeNullOrEmpty();

        // Should return the same cached value
        result1.GetReturnValue().Should().Be(result2.GetReturnValue());

        // Should start with expected base name
        result1.GetReturnValue().Should().StartWith(expectedBase);

        // Should have GUID suffix (underscore + 32 hex chars)
        result1.GetReturnValue().Should().MatchRegex($"^{System.Text.RegularExpressions.Regex.Escape(expectedBase)}_[0-9a-f]{{32}}$");

        // If input was longer than 214, verify truncation
        if (compatLayerValue?.Length > 214)
        {
            var pipeName = result1.GetReturnValue();
            var baseNamePart = pipeName.Substring(0, pipeName.LastIndexOf('_'));
            baseNamePart.Length.Should().Be(214);
        }
    }

    [Fact]
    public void CalculateTracePipeName_HandlesException_ReturnsFallback()
    {
        // Arrange
        var state = new CallTargetState();
        var expectedException = new InvalidOperationException("Test exception");
        var fallbackValue = "fallback_pipe_name";

        // Act - Pass exception to OnMethodEnd
        var result = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            instance: null,
            returnValue: fallbackValue,
            exception: expectedException,
            in state);

        // Assert - Should return the fallback value when exception is present
        result.GetReturnValue().Should().Be(fallbackValue);
    }

    [Theory]
    [InlineData(null, "dd_dogstatsd")] // Null base name should use default
    [InlineData("custom_statsd", "custom_statsd")] // Custom base name should be used
    [InlineData("short", "short")] // Short name should work
    [InlineData("this_is_a_very_long_dogstatsd_pipe_name_that_exceeds_the_maximum_allowed_length_for_windows_named_pipes_which_is_256_characters_total_including_the_prefix_and_we_need_to_make_sure_this_gets_truncated_properly_to_214_characters_before_appending_the_guid_suffix", "this_is_a_very_long_dogstatsd_pipe_name_that_exceeds_the_maximum_allowed_length_for_windows_named_pipes_which_is_256_characters_total_including_the_prefix_and_we_need_to_make_sure_this_gets_truncated_properly_to_")] // Long name should be truncated to 214 chars
    public void CalculateDogStatsDPipeName_GeneratesValidUniquePipeName(string compatLayerValue, string expectedBase)
    {
        // Arrange
        var state = new CallTargetState();

        // Act - First call generates the pipe name
        var result1 = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            instance: null,
            returnValue: compatLayerValue,
            exception: null,
            in state);

        // Second call should return the same cached value
        var result2 = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            instance: null,
            returnValue: compatLayerValue,
            exception: null,
            in state);

        // Assert
        result1.GetReturnValue().Should().NotBeNullOrEmpty();
        result2.GetReturnValue().Should().NotBeNullOrEmpty();

        // Should return the same cached value
        result1.GetReturnValue().Should().Be(result2.GetReturnValue());

        // Should start with expected base name
        result1.GetReturnValue().Should().StartWith(expectedBase);

        // Should have GUID suffix (underscore + 32 hex chars)
        result1.GetReturnValue().Should().MatchRegex($"^{System.Text.RegularExpressions.Regex.Escape(expectedBase)}_[0-9a-f]{{32}}$");

        // If input was longer than 214, verify truncation
        if (compatLayerValue?.Length > 214)
        {
            var pipeName = result1.GetReturnValue();
            var baseNamePart = pipeName.Substring(0, pipeName.LastIndexOf('_'));
            baseNamePart.Length.Should().Be(214);
        }
    }

    [Fact]
    public void CalculateDogStatsDPipeName_HandlesException_ReturnsFallback()
    {
        // Arrange
        var state = new CallTargetState();
        var expectedException = new InvalidOperationException("Test exception");
        var fallbackValue = "fallback_dogstatsd_pipe";

        // Act - Pass exception to OnMethodEnd
        var result = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            instance: null,
            returnValue: fallbackValue,
            exception: expectedException,
            in state);

        // Assert - Should return the fallback value when exception is present
        result.GetReturnValue().Should().Be(fallbackValue);
    }
}
#endif
