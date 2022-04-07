// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef _WINDOWS

#include "SystemTime.h"
#include "timezoneapi.h"

const uint64_t msInSecond = 1000;
const uint64_t msInMinute = 60 * 1000;
const uint64_t msInHour = 60 * 60 * 1000;
const uint64_t msInDay = 24 * 60 * 60 * 1000;

uint64_t GetTotalMilliseconds(SYSTEMTIME time)
{
    uint64_t total = time.wMilliseconds;
    if (time.wSecond != 0)
    {
        total += (uint64_t)time.wSecond * msInSecond;
    }
    if (time.wMinute != 0)
    {
        total += (uint64_t)time.wMinute * msInMinute;
    }
    if (time.wHour != 0)
    {
        total += (uint64_t)time.wHour * msInHour;
    }
    if (time.wDay != 0)
    {
        total += (uint64_t)(time.wDay - 1) * msInDay; // january 1st 1601
    }

    // don't deal with month duration...

    return total;
}

uint64_t GetTotalMilliseconds(FILETIME fileTime)
{
    SYSTEMTIME systemTime;
    ::FileTimeToSystemTime(&fileTime, &systemTime);
    return GetTotalMilliseconds(systemTime);
}

#endif
