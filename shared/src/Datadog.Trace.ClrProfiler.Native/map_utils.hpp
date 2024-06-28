// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0. This product includes software
// developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present
// Datadog, Inc.

#pragma once

#include <string>
#include <string_view>
#include <unordered_map>

namespace ddprof {

struct StringHash {
  using Hasher = std::hash<std::string_view>;
  using is_transparent = void;

  std::size_t operator()(const char *str) const { return Hasher{}(str); }
  std::size_t operator()(std::string_view str) const { return Hasher{}(str); }
  std::size_t operator()(std::string const &str) const { return Hasher{}(str); }
};

template <typename T>
using HeterogeneousLookupStringMap =
    std::unordered_map<std::string, T, StringHash, std::equal_to<>>;

} // namespace ddprof
