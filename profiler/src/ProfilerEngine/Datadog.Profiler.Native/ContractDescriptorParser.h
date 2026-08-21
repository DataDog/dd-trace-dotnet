// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CdacDescriptorTypes.h"

namespace cdac
{
// Parses the physical JSON data descriptor (compact format; the regular format never appears in the
// in-memory blob) as described in data_descriptor.md. Indirect values ([index]) are resolved against
// the descriptor's own pointer_data array here, since each descriptor owns its own indirection table.
class ContractDescriptorParser
{
public:
    static ParsedDescriptor Parse(const RawContractDescriptor& raw);
};
} // namespace cdac
