// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IConfiguration.h"

#include <string>
#include <vector>

class IMetadataProvider
{
public:
    using section_t = std::pair<std::string, std::vector<std::pair<std::string, std::string>>>;
    using metadata_t = std::vector<section_t>;

    virtual ~IMetadataProvider() = default;

    virtual void Initialize() = 0;
    virtual void Add(std::string const& section, std::string const& key, std::string const& value) = 0;
    virtual metadata_t const& Get() = 0;
};