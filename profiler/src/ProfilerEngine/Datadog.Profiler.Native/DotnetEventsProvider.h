// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

enum class DotnetEventsProvider
{
    Unknown = 0,
    Clr = 1,
    Http = 2,
    Sockets = 3,
    NameResolution = 4,
    NetSecurity = 5,
};