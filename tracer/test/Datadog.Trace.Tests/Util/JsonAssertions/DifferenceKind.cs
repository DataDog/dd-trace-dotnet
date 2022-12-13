﻿// <copyright file="DifferenceKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/// Based on https://github.com/fluentassertions/fluentassertions.json

namespace Datadog.Trace.Tests.Util.JsonAssertions;

internal enum DifferenceKind
{
    ActualIsNull,
    ExpectedIsNull,
    OtherType,
    OtherName,
    OtherValue,
    DifferentLength,
    ActualMissesProperty,
    ExpectedMissesProperty,
    ActualMissesElement,
    WrongOrder
}
