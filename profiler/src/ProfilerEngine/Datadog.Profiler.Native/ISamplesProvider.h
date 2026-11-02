// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <cstddef>
#include <list>
#include <memory>

// forward declarations
class SamplesEnumerator;

class ISamplesProvider
{
public:
    virtual ~ISamplesProvider() = default;
    virtual std::unique_ptr<SamplesEnumerator> GetSamples() = 0;
    virtual const char* GetName() = 0;

    // Number of raw samples waiting to be collected by the SamplesCollector.
    // This is expected to stay close to zero: a value that keeps growing means samples are being
    // created but never drained, which is unbounded memory growth (see SCP-1413). Providers that do
    // not queue raw samples keep the default.
    virtual std::size_t GetPendingSamplesCount() const
    {
        return 0;
    }
};