// <copyright file="DatadogTestLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using SpektTestLogger = Spekt.TestLogger.TestLogger;

namespace Datadog.Trace.TestLogger;

/// <summary>
/// Datadog Test Logger
/// </summary>
[FriendlyName("datadog")]
[ExtensionUri("logger://Microsoft/TestPlatform/DatadogTestLogger/v1")]
public class DatadogTestLogger : SpektTestLogger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatadogTestLogger"/> class.
    /// </summary>
    public DatadogTestLogger()
        : base(new DatadogTestResultSerializer())
    {
    }

    /// <inheritdoc/>
    protected override string DefaultTestResultFile => "Datadog.TestResult.txt";
}
