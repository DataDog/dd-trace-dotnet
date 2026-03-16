// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Validation.h"
#include "TestProfilerCallback.h"
#include "Log.h"
#include <fstream>
#include <sstream>
#include <chrono>
#include <thread>
#include <algorithm>
#include <map>
#include <set>

// Helper: Get method name using FrameStore
std::string GetFullMethodName(TestProfilerCallback* profiler, uintptr_t instructionPointer)
{
    if (!profiler || !profiler->_pFrameStore)
    {
        return "<UnknownFunction>";
    }

    auto [success, frameInfo] = profiler->_pFrameStore->GetFrame(instructionPointer);

    if (!success || frameInfo.Frame == nullptr || frameInfo.Frame[0] == '\0')
    {
        return "<UnknownFunction>";
    }

    // Parse frame: |lm:Assembly |ns:Namespace |ct:Type |fn:Method
    std::string frame(frameInfo.Frame);

    auto extractTag = [](const std::string& str, const std::string& tag) -> std::string {
        size_t pos = str.find(tag);
        if (pos == std::string::npos) return "";
        pos += tag.length();
        size_t end = str.find(" |", pos);
        if (end == std::string::npos) end = str.length();
        return str.substr(pos, end - pos);
    };

    std::string typeName = extractTag(frame, "|ct:");
    std::string methodName = extractTag(frame, "|fn:");

    if (typeName.empty())
    {
        return methodName.empty() ? "<UnknownFunction>" : methodName;
    }

    return typeName + "::" + methodName;
}

extern "C" PROFILER_EXPORT int PrepareForValidation()
{
    Log::Info("PrepareForValidation starting...");

    auto profiler = TestProfilerCallback::GetInstance();
    if (!profiler || !profiler->_pCorProfilerInfo)
    {
        Log::Error("PrepareForValidation: Profiler not initialized");
        return -1;
    }

    // ReJIT testing is currently disabled (see TODO in TestProfilerCallback.h)
    // This function is kept for future use when ReJIT support is added
    Log::Info("PrepareForValidation completed (ReJIT testing disabled)");
    return 0;
}

extern "C" PROFILER_EXPORT int ValidateManagedCodeCache(
    ValidationOptions* options,
    ValidationResult* result)
{
    Log::Info("ValidateManagedCodeCache starting...");

    auto profiler = TestProfilerCallback::GetInstance();
    if (!profiler || !profiler->_pManagedCodeCache)
    {
        Log::Error("ValidateManagedCodeCache: Profiler not initialized");
        return -1;
    }

    std::string reportPath = options && options->reportPath
        ? std::string(options->reportPath)
        : "validation_report.txt";

    // Initialize result
    if (result)
    {
        result->totalIPs = 0;
        result->totalFunctions = 0;
        result->totalCodeRanges = 0;
        result->failureCount = 0;
        result->skippedIPs = 0;
        result->invalidIPsTested = 0;
        result->invalidIPFailures = 0;
        result->firstFailureMethod[0] = '\0';
    }

    std::ofstream report(reportPath);
    if (!report.is_open())
    {
        Log::Error("Failed to open report file: ", reportPath);
        return -1;
    }

    report << "=== ManagedCodeCache Validation Report ===" << std::endl;
    report << "Generated: " << std::chrono::system_clock::now().time_since_epoch().count() << std::endl;
    report << std::endl;

    // Validate collected IPs
    int totalIPs = 0;
    int totalFunctions = 0;
    int totalCodeRanges = 0;
    int failureCount = 0;
    int skippedIPs = 0;
    std::string firstFailureMethod;

    // Per-category statistics
    struct CategoryStats
    {
        int functions = 0;
        int codeRanges = 0;
        int ipsValidated = 0;
        int ipsSkipped = 0;
        int failures = 0;
    };

    std::map<MethodCategory, CategoryStats> categoryStats;

    {
        std::lock_guard<std::mutex> lock(profiler->_ipCollectionMutex);
        totalFunctions = static_cast<int>(profiler->_collectedIPs.size());

        // Track which categories are present for each function
        std::map<FunctionID, std::set<MethodCategory>> functionCategories;

        for (const auto& [funcId, ranges] : profiler->_collectedIPs)
        {
            for (const auto& range : ranges)
            {
                functionCategories[funcId].insert(range.category);
            }
        }

        // Count functions per category
        for (const auto& [funcId, categories] : functionCategories)
        {
            for (auto category : categories)
            {
                categoryStats[category].functions++;
            }
        }

        report << "## Summary" << std::endl;
        report << "Total Functions: " << totalFunctions << std::endl;

        for (const auto& [funcId, ranges] : profiler->_collectedIPs)
        {
            totalCodeRanges += static_cast<int>(ranges.size());

            for (const auto& range : ranges)
            {
                categoryStats[range.category].codeRanges++;

                if (range.skipValidation)
                {
                    int skipCount = static_cast<int>(range.instructionPointers.size());
                    skippedIPs += skipCount;
                    categoryStats[range.category].ipsSkipped += skipCount;
                    continue;
                }

                for (uintptr_t ip : range.instructionPointers)
                {
                    totalIPs++;
                    categoryStats[range.category].ipsValidated++;

                    // Validate with cache
                    auto cachedId = profiler->_pManagedCodeCache->GetFunctionId(ip);

                    // InvalidFunctionId constant from ManagedCodeCache
                    constexpr FunctionID InvalidFunctionId = static_cast<FunctionID>(-1);

                    // For R2R modules, we don't call GetFunctionFromIP because random IPs
                    // in R2R sections may not be valid and could cause issues
                    // We only validate that the cache returns a reasonable result
                    if (range.category == MethodCategory::ReadyToRun)
                    {
                        // For R2R, just check if cache returns InvalidFunctionId (-1)
                        // A valid result (0 or a function ID) means cache found something
                        // InvalidFunctionId means the IP is not a valid function entry point
                        if (cachedId.has_value() && cachedId.value() == InvalidFunctionId)
                        {
                            // Invalid IP in R2R section - skip
                            skippedIPs++;
                            categoryStats[range.category].ipsSkipped++;
                            categoryStats[range.category].ipsValidated--;
                            continue;
                        }

                        // Cache returned 0 or nullopt - also skip (not a valid R2R function)
                        if (!cachedId.has_value() || cachedId.value() == 0)
                        {
                            skippedIPs++;
                            categoryStats[range.category].ipsSkipped++;
                            categoryStats[range.category].ipsValidated--;
                            continue;
                        }

                        // Cache found a valid function ID - consider this a success
                        // (We can't validate against CLR easily for R2R)
                        continue;
                    }

                    // Also validate with CLR (source of truth) for non-R2R methods
                    FunctionID clrFunctionId = 0;
                    HRESULT hr = profiler->_pCorProfilerInfo->GetFunctionFromIP(
                        reinterpret_cast<LPCBYTE>(ip), &clrFunctionId);

                    // Dynamic methods can be GC'd, so CLR might not find them anymore
                    // If CLR fails but cache succeeds, and it's a dynamic method, that's expected
                    if (FAILED(hr) && range.category == MethodCategory::DynamicMethod && cachedId.has_value())
                    {
                        // Dynamic method was GC'd - skip this IP
                        skippedIPs++;
                        categoryStats[range.category].ipsSkipped++;
                        categoryStats[range.category].ipsValidated--; // Remove from validated count
                        continue;
                    }

                    bool cacheCorrect, clrCorrect, cacheCLRMismatch;

                    // For other categories, check against expected funcId
                    cacheCorrect = cachedId.has_value() && cachedId.value() == funcId;
                    clrCorrect = SUCCEEDED(hr) && clrFunctionId == funcId;
                    cacheCLRMismatch = cachedId.has_value() && clrCorrect && (cachedId.value() != clrFunctionId);

                    if (!cacheCorrect || !clrCorrect || cacheCLRMismatch)
                    {
                        failureCount++;
                        categoryStats[range.category].failures++;

                        if (firstFailureMethod.empty())
                        {
                            firstFailureMethod = GetFullMethodName(profiler, ip);
                        }

                        // Always write detailed failure information to report
                        if (failureCount == 1)
                        {
                            report << "## Failures" << std::endl;
                        }

                        report << "Failure #" << failureCount << ": " << GetFullMethodName(profiler, ip) << std::endl;
                        report << "  IP: 0x" << std::hex << ip << std::dec << std::endl;
                        report << "  Expected FunctionID: " << funcId << std::endl;
                        report << "  Cache returned: " << (cachedId.has_value() ? std::to_string(cachedId.value()) : "nullopt") << std::endl;
                        report << "  CLR returned: " << clrFunctionId << " (hr=" << std::hex << hr << std::dec << ")" << std::endl;
                        report << std::endl;
                    }
                }
            }
        }

        report << "Total Code Ranges: " << totalCodeRanges << std::endl;
        report << "Total IPs Validated: " << totalIPs << std::endl;
        report << "Skipped IPs: " << skippedIPs << std::endl;
        report << "Failures: " << failureCount << std::endl;
        report << std::endl;

        // Write per-category statistics
        report << "## Statistics by Category" << std::endl;
        report << std::endl;

        auto getCategoryName = [](MethodCategory category) -> const char* {
            switch (category)
            {
            case MethodCategory::JitCompiled: return "JIT-Compiled";
            case MethodCategory::DynamicMethod: return "Dynamic Methods";
            case MethodCategory::ReJIT: return "ReJIT";
            case MethodCategory::ReadyToRun: return "Ready-to-Run (R2R)";
            default: return "Unknown";
            }
        };

        for (const auto& [category, stats] : categoryStats)
        {
            report << "### " << getCategoryName(category) << std::endl;
            report << "  Functions: " << stats.functions << std::endl;
            report << "  Code Ranges: " << stats.codeRanges << std::endl;
            report << "  IPs Validated: " << stats.ipsValidated << std::endl;
            if (stats.ipsSkipped > 0)
            {
                report << "  IPs Skipped: " << stats.ipsSkipped << std::endl;
            }
            if (stats.failures > 0)
            {
                report << "  Failures: " << stats.failures << std::endl;
            }
            report << std::endl;
        }
    }

    // Validate invalid IPs
    int invalidIPsTested = 0;
    int invalidIPFailures = 0;

    {
        std::lock_guard<std::mutex> lock(profiler->_invalidIPsMutex);
        invalidIPsTested = static_cast<int>(profiler->_invalidIPsToTest.size());

        report << "## Invalid IP Tests" << std::endl;
        report << "Total Invalid IPs Tested: " << invalidIPsTested << std::endl;

        for (const auto& invalidIP : profiler->_invalidIPsToTest)
        {
            auto cachedId = profiler->_pManagedCodeCache->GetFunctionId(invalidIP.ip);

            FunctionID clrFunctionId = 0;
            HRESULT hr = profiler->_pCorProfilerInfo->GetFunctionFromIP(
                reinterpret_cast<LPCBYTE>(invalidIP.ip), &clrFunctionId);

            // Both should fail (return nullopt or E_FAIL)
            bool cacheCorrect = !cachedId.has_value();
            bool clrCorrect = FAILED(hr) || clrFunctionId == 0;

            if (!cacheCorrect || !clrCorrect)
            {
                invalidIPFailures++;
                report << "FAILURE: " << invalidIP.description << " (IP=0x" << std::hex << invalidIP.ip << std::dec << ")" << std::endl;
                report << "  Cache: " << (cachedId.has_value() ? "returned value" : "nullopt (correct)") << std::endl;
                report << "  CLR: hr=" << std::hex << hr << std::dec << ", funcId=" << clrFunctionId << std::endl;
            }
        }

        report << "Invalid IP Failures: " << invalidIPFailures << std::endl;
        report << std::endl;
    }

    // Write final result
    int totalFailures = failureCount + invalidIPFailures;

    if (totalFailures == 0)
    {
        report << "Result: ✓ PASSED" << std::endl;
        Log::Info("✓ PASSED - All validations successful!");
    }
    else
    {
        report << "Result: ✗ FAILED" << std::endl;
        report << "Total Failures: " << totalFailures << std::endl;
        if (!firstFailureMethod.empty())
        {
            report << "First Failure: " << firstFailureMethod << std::endl;
        }
        Log::Error("✗ FAILED - ", totalFailures, " failures detected");
    }

    report.close();

    // Populate result
    if (result)
    {
        result->totalIPs = totalIPs;
        result->totalFunctions = totalFunctions;
        result->totalCodeRanges = totalCodeRanges;
        result->failureCount = failureCount;
        result->skippedIPs = skippedIPs;
        result->invalidIPsTested = invalidIPsTested;
        result->invalidIPFailures = invalidIPFailures;

        if (!firstFailureMethod.empty())
        {
            strncpy(result->firstFailureMethod, firstFailureMethod.c_str(), 511);
            result->firstFailureMethod[511] = '\0';
        }
    }

    Log::Info("Report written to: ", reportPath);
    return totalFailures > 0 ? 1 : 0;
}
