// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>

//
// etw_timestamp represent the events timestamp in System Time for GMT
// etw_time is a ratio used to convert between time types
//

// systemTimestamp == etw_timestamp
// nanoseconds has      ratio<1, 1000000000> (nano)
// since is in 100ns units
// => etw_timestamp has ratio<1, 10000000> (etw_time)
using etw_time = std::ratio<1, 10000000>;

// the events timestamp is in System Time for GMT
using etw_timestamp = std::chrono::duration<long long, etw_time>;