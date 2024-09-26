#pragma once

#include <shared/src/native-src/dd_span.hpp>
#include <shared/src/native-src/dd_filesystem.hpp>

#include <optional>

using BuildIdSpan = shared::span<const std::byte>;
using BuildId = std::vector<std::byte>;

class BuildIdExtractor {
public:

    BuildIdExtractor() = delete;

    static std::optional<BuildId> Get(fs::path const& file); 
};
