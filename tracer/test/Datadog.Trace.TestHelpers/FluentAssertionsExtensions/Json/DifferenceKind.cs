// <copyright file="DifferenceKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

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
