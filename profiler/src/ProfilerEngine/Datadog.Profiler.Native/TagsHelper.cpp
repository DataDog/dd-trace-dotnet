// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TagsHelper.h"


tag TagsHelper::ParseTag(std::string_view s)
{
    auto colonIdx = s.find_first_of(':');

    if (colonIdx == std::string::npos)
        return tag(s, "");

    return tag(s.substr(0, colonIdx), s.substr(colonIdx + 1));
}

tags TagsHelper::Parse(std::string && s)
{
    tags result;

    auto commaIdx = s.find_first_of(',');
    size_t startIdx =  0;
    while (commaIdx != std::string::npos)
    {
        if (commaIdx - startIdx != 0)
        {
            result.push_back(ParseTag(std::string_view(s.data() + startIdx, commaIdx - startIdx)));
        }
        startIdx = commaIdx + 1;
        commaIdx = s.find_first_of(',', commaIdx + 1);
    }

    if (startIdx < s.size())
        result.push_back(ParseTag(std::string_view(s.data() + startIdx, s.size() - startIdx)));

    return result;
}