// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0. This product includes software
// developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present
// Datadog, Inc.

#pragma once

#include "map_utils.hpp"

#include <link.h>
#include <string>
#include <string_view>
#include <functional>
#include "../../../shared/src/native-src/dd_span.hpp"

namespace ddprof {

struct DynamicInfo;

const ElfW(Sym) *
    gnu_hash_lookup(const char *strtab, const ElfW(Sym) * symtab,
                    const uint32_t *hashtab, std::string_view name);
const ElfW(Sym) *
    elf_hash_lookup(const char *strtab, const ElfW(Sym) * symtab,
                    const uint32_t *hashtab, std::string_view name);

uint32_t elf_hash_symbol_count(const uint32_t *hashtab);
uint32_t gnu_hash_symbol_count(const uint32_t *hashtab);

struct LookupResult {
  ElfW(Sym) symbol;
  std::string object_name;
};

// Lookup first symbol matching `symbol_name` and different from
// `not_this_symbol`
LookupResult lookup_symbol(std::string_view symbol_name,
                           bool accept_null_sized_symbol,
                           uintptr_t not_this_symbol = 0);

void override_symbol(std::string_view symbol_name, uintptr_t new_symbol,
                     uintptr_t do_not_override_this_symbol = 0);

int count_loaded_libraries();

enum class LibraryCallbackStatus {
  Continue,
  Stop,
};

using LibraryCallback =
    std::function<LibraryCallbackStatus(const dl_phdr_info &, bool is_exe)>;

LibraryCallbackStatus iterate_over_loaded_libraries(LibraryCallback callback);

class SymbolOverrides {
public:
  // register a symbol override
  // symbol_name: name of the symbol to override
  // new_symbol: new symbol value
  // ref_symbol: output filled with the address of the symbol to override
  // do_not_override_this_symbol: if symbol value is equal to this value, do not
  //                              override it
  bool register_override(std::string_view symbol_name, uintptr_t new_symbol,
                         uintptr_t *ref_symbol,
                         uintptr_t do_not_override_this_symbol = 0);

  // override all registered symbols
  void apply_overrides();

  // apply overrides to newly loaded libraries
  void update_overrides();

  // restore all overriden symbols to their original value
  void restore_overrides();

private:
  struct SymbolOverrideInfo {
    uintptr_t *ref_symbol;
    uintptr_t new_symbol;
    uintptr_t do_not_override_this_symbol;
  };

  using SymbolNameToOverrideMap =
      HeterogeneousLookupStringMap<SymbolOverrideInfo>;
  using AddressToValueMap = std::unordered_map<uintptr_t, uintptr_t>;

  struct LibraryRevertInfo {
    explicit LibraryRevertInfo(std::string_view library_name)
        : library_name(library_name) {}
    std::string library_name;
    AddressToValueMap old_value_per_address;
    bool processed = false;
  };

  void restore_library_overrides(std::string_view library_name,
                                 uintptr_t base_address);

  void apply_overrides_to_library(const DynamicInfo &info,
                                  std::string_view library_name);

  template <typename Reloc>
  void process_relocations(const DynamicInfo &dyn_info, shared::span<Reloc> relocs,
                           LibraryRevertInfo &revert_info);

  std::unordered_map<uintptr_t, LibraryRevertInfo> _revert_info_per_library;
  SymbolNameToOverrideMap _overrides;
  int _nb_loaded_libs{-1};
};

} // namespace ddprof
