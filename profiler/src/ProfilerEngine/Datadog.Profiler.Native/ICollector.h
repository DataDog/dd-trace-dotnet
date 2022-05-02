// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once


template <class TRawSample>
class ICollector
{
public:
    virtual ~ICollector() = default;

public:
    virtual void Add(TRawSample&& rawSample) = 0;
};
