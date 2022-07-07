// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>

// forward declarations
class Sample;

class ISamplesProvider
{
public:
    virtual ~ISamplesProvider() = default;
    virtual std::list<Sample> GetSamples() = 0;
    virtual void ProcessRawSamples() = 0;
};