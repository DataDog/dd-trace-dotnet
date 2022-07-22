// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "ISamplesProvider.h"
#include "Sample.h"

#include <list>

class ISamplesCollector
{
public:
    virtual ~ISamplesCollector() = default;

    virtual std::list<Sample> GetSamples() = 0;
    virtual void Register(ISamplesProvider* sampleProvider) = 0;
};
