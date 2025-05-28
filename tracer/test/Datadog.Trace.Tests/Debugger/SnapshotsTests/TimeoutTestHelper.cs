// <copyright file="TimeoutTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Expressions;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests;

/// <summary>
/// Helper class to control timeout scenarios for testing serialization timeout behavior.
/// This uses CaptureLimitInfo to set custom timeout values instead of reflection.
/// </summary>
internal static class TimeoutTestHelper
{
    /// <summary>
    /// Create a CaptureLimitInfo with a very low timeout to trigger timeout scenarios
    /// </summary>
    public static CaptureLimitInfo CreateLowTimeoutLimitInfo(int timeoutMs = 1)
    {
        return new CaptureLimitInfo(
            maxReferenceDepth: null, // Use defaults
            maxCollectionSize: null, // Use defaults
            maxLength: null, // Use defaults
            maxFieldCount: null, // Use defaults
            timeoutInMilliSeconds: timeoutMs);
    }

    /// <summary>
    /// Create an object that will likely trigger timeout during serialization
    /// </summary>
    public static object CreateSlowSerializationObject()
    {
        return new SlowToStringObject();
    }

    /// <summary>
    /// Object with a ToString method that takes a long time to execute
    /// </summary>
    private class SlowToStringObject
    {
        public SlowToStringObject()
        {
            // Create a structure that will be slow to serialize
            for (int i = 0; i < 100; i++)
            {
                var child = new SlowToStringObject
                {
                    Name = $"Child{i}",
                    Children = new List<SlowToStringObject>()
                };

                // Add some nested children
                for (int j = 0; j < 10; j++)
                {
                    child.Children.Add(new SlowToStringObject
                    {
                        Name = $"Grandchild{i}_{j}"
                    });
                }

                Children.Add(child);
                ChildrenDict[$"key{i}"] = child;
            }
        }

        public string Name { get; set; } = "SlowObject";

        public List<SlowToStringObject> Children { get; set; } = new();

        public Dictionary<string, SlowToStringObject> ChildrenDict { get; set; } = new();

        public override string ToString()
        {
            // Simulate slow ToString operation
            System.Threading.Thread.Sleep(10); // Small delay that adds up
            return $"SlowObject({Name})";
        }
    }
}
