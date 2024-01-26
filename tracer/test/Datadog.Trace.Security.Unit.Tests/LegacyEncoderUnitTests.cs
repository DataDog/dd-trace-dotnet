// <copyright file="LegacyEncoderUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.WafEncoding;

namespace Datadog.Trace.Security.Unit.Tests;

public class LegacyEncoderUnitTests() : EncoderUnitTests(new EncoderLegacy(WafLibraryInvoker));
