// <copyright file="CaptureExpression.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal record CaptureExpression
    {
        /// <summary>
        /// Gets or sets the name of the capture expression section that will be generated into the snapshot.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the debugger expression describing what should be evaluated and captured.
        /// </summary>
        public required SnapshotSegment Expr { get; set; }

        /// <summary>
        /// Gets or sets the optional capture limits specific to this capture expression.
        /// When not specified, the probe-level capture limits (if any) are used.
        /// </summary>
        public Capture? Capture { get; set; }
    }
}
