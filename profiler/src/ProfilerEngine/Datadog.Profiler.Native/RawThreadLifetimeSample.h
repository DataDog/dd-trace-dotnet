// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "InternedString.h"
#include "RawSample.h"

class SampleValueTypeProvider;

enum class ThreadEventKind
{
    Start,
    Stop
};


class RawThreadLifetimeSample : public RawSample
{
public:
    ThreadEventKind Kind;

public:
    // Inherited via RawSample
    void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffset) const override;

private:
    // each thread lifetime event will share the same root frame and the second one will show the collected generation
    static const inline InternedString EmptyModule = "CLR";
    static const inline InternedString StartFrame = "|lm: |ns: |ct: |cg: |fn:Thread Start |fg: |sg:";
    static const inline InternedString StopFrame = "|lm: |ns: |ct: |cg: |fn:Thread Stop |fg: |sg:";
};