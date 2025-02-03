#pragma once

#include <atomic>
#include <optional>
#include <vector>
#include <string>

#include <datadog/common.h>

namespace shared {

typedef struct ConfigEntry {
    std::string key;
    std::string value;
} ConfigEntry;

// FFI struct for managed .NET interop
// TODO: is this really needed?
typedef struct ConfigEntryFFI {
    const char* key;
    const char* value;
} ConfigEntryFFI;

class LibraryConfig
{
  private:
    static ddog_CharSlice to_char_slice(const std::string& str);

  public:
    static std::vector<ConfigEntry> get_configuration(bool debug_logs);
    static std::string config_path; // Overridden in tests
};

} // namespace shared
