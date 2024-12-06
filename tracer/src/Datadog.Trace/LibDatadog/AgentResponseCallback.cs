// <copyright file="AgentResponseCallback.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Callback to be invoked when the agent responds to a request.
/// </summary>
internal delegate void AgentResponseCallback(IntPtr response);
