// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <string>
#include <tuple>
#include <vector>

typedef std::pair<std::string, std::string> tag;
typedef std::vector<tag> tags;

class TagsHelper
{
public:
    TagsHelper() = delete;
    ~TagsHelper() = delete;

    static tags Parse(std::string&& s);

private:
    static tag ParseTag(std::string_view s);
};
