// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadsCpuManager.h"

// -------------------------------------
// Currently no implementation on Linux
// -------------------------------------
// List all threads in the current process
// and dump their CPU consumption
void ThreadsCpuManager::LogCpuTimes()
{
}
