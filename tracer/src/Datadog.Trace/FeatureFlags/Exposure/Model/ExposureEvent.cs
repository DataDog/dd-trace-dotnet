// <copyright file="ExposureEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Exposure.Model;

// The serializer includes nulls globally, but the intake reads an absent serial id and an explicit
// null differently, so this one property has to omit itself instead.
internal readonly record struct ExposureEvent(
    long Timestamp,
    Allocation Allocation,
    Flag Flag,
    Variant Variant,
    Subject Subject,
    [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] long? SerialId = null);
