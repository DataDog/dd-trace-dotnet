// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>

#include "DeploymentMode.h"

class IExporter;

// TODO: move these enums to a separate file
// This is a bits flag that represents the heuristics that were not triggered
// For example:
//   if
//      - the process ends before the short lived threshold is reached AND
//      - a span was created
//   --> ShortLived
//
//   if
//      - the process ends after the short lived threshold AND
//      - no span was created
//   --> NoSpan
//
//   if
//      - the process ends before the short lived threshold is reached AND
//      - no span was created
//   --> NoSpan | ShortLived
//
//   if
//      - the process ends after the short lived threshold AND
//      - a span was created
//   --> AllTriggered
//
enum SkipProfileHeuristicType
{
    AllTriggered = 0,
    ShortLived   = 0x1,
    NoSpan       = 0x2,
    // TODO: add new heuristics here
};


// TODO: this class was used when we planned to send metrics when the process starts and ends
//       It is also used in tests.
//       See how to "merge" with TelemetryMetricsWorker
class IProfilerTelemetry
{
public:
    virtual ~IProfilerTelemetry() = default;

    // send metrics
    virtual void ProcessStart(DeploymentMode deployment) = 0;
    virtual void ProcessEnd(uint64_t duration, uint64_t sentProfiles, SkipProfileHeuristicType heuristics) = 0;
    virtual void SetExporter(IExporter* pExporter) = 0;

};
