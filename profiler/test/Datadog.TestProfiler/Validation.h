// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#ifdef _WIN32
    #define PROFILER_EXPORT __declspec(dllexport)
#else
    #define PROFILER_EXPORT __attribute__((visibility("default")))
#endif

struct ValidationOptions {
    bool failFast;
    const char* reportPath;  // UTF-8, full path
};

struct ValidationResult {
    int totalIPs;
    int totalFunctions;
    int totalCodeRanges;
    int failureCount;
    int skippedIPs;
    int invalidIPsTested;
    int invalidIPFailures;
    char firstFailureMethod[512];
};

extern "C" {
    PROFILER_EXPORT int PrepareForValidation();
    PROFILER_EXPORT int ValidateManagedCodeCache(
        ValidationOptions* options,
        ValidationResult* result);
}
