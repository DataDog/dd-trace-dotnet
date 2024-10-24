// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "stdint.h"

// uniquely identify a network activity based on the GUID provided by the events payload
//     00005011-0000-0000-0000-00009e259d59
// -->      011-0000-0000-0000-00009e
class NetworkActivity
{
public:
    uint32_t Activity;
};
