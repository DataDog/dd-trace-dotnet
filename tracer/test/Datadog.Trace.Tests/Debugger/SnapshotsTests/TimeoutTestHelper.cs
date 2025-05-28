// <copyright file="TimeoutTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests;

/// <summary>
/// Helper class to control timeout scenarios for testing serialization timeout behavior.
/// This uses reflection to temporarily modify the internal timeout settings.
/// </summary>
internal static class TimeoutTestHelper
{
    private static readonly FieldInfo MaxSerializationTimeField;
    private static int? _originalTimeout;

    static TimeoutTestHelper()
    {
        // Get the private static field that controls serialization timeout
        MaxSerializationTimeField = typeof(DebuggerSnapshotSerializer)
            .GetField("_maximumSerializationTime", BindingFlags.NonPublic | BindingFlags.Static);
    }

    /// <summary>
    /// Temporarily set a very low timeout to trigger timeout scenarios
    /// </summary>
    public static IDisposable SetLowTimeout(int timeoutMs = 1)
    {
        if (MaxSerializationTimeField == null)
        {
            throw new InvalidOperationException("Could not find _maximumSerializationTime field. The implementation may have changed.");
        }

        // Store original value
        _originalTimeout = (int)MaxSerializationTimeField.GetValue(null);

        // Set very low timeout
        MaxSerializationTimeField.SetValue(null, timeoutMs);

        return new TimeoutRestorer();
    }

    /// <summary>
    /// Create an object that will likely trigger timeout during serialization
    /// </summary>
    public static object CreateSlowSerializationObject()
    {
        return new SlowToStringObject();
    }

    private static void RestoreOriginalTimeout()
    {
        if (MaxSerializationTimeField != null && _originalTimeout.HasValue)
        {
            MaxSerializationTimeField.SetValue(null, _originalTimeout.Value);
            _originalTimeout = null;
        }
    }

    private class TimeoutRestorer : IDisposable
    {
        private bool _disposed = false;

        public void Dispose()
        {
            if (!_disposed)
            {
                RestoreOriginalTimeout();
                _disposed = true;
            }
        }
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
