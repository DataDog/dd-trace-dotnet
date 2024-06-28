// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0. This product includes software
// developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present
// Datadog, Inc.

#include "elfutils.hpp"

#include "log.h"

#include <algorithm>
#include <climits>
#include <cstdio>
#include <cstring>
#include <functional>
#include <optional>
#include <sys/mman.h>
#include <unistd.h>

#include "../../../shared/src/native-src/dd_span.hpp"

#ifndef __ELF_NATIVE_CLASS
#define __ELF_NATIVE_CLASS 64
#endif

namespace ddprof
{

struct DynamicInfo
{
    std::string_view strtab;
    shared::span<const ElfW(Sym)> symtab;
    shared::span<const ElfW(Rel)> rels;
    shared::span<const ElfW(Rela)> relas;
    shared::span<const ElfW(Rela)> jmprels;
    const uint32_t* elf_hash;
    const uint32_t* gnu_hash;
    ElfW(Addr) base_address;
};

namespace
{

    uint64_t load64(uintptr_t addr)
    {
        uint64_t result;
        const auto* p = reinterpret_cast<const char*>(addr);
        memcpy(&result, p, sizeof(result));
        return result;
    }

    void write64(uintptr_t addr, uint64_t value)
    {
        auto* p = reinterpret_cast<char*>(addr);
        memcpy(p, &value, sizeof(value));
    }

    // NOLINTBEGIN(readability-magic-numbers)
    uint32_t gnu_hash(std::string_view name)
    {
        uint32_t h = 5381;

        for (auto c : name)
        {
            h = (h << 5) + h + c;
        }

        return h;
    }

    uint32_t elf_hash(std::string_view name)
    {
        uint32_t h = 0;
        for (auto c : name)
        {
            h = (h << 4) + c;
            uint32_t const g = h & 0xf0000000;
            h ^= g >> 24;
        }
        return h & 0x0fffffff;
    }
    // NOLINTEND(readability-magic-numbers)

    bool check(const ElfW(Sym) & sym, const char* symname, std::string_view name)
    {
        auto stt = ELF64_ST_TYPE(sym.st_info);

        if (sym.st_value == 0 && sym.st_shndx != SHN_ABS && stt != STT_TLS)
        {
            return false;
        }

        if (((1 << stt) & ((1 << STT_NOTYPE) | (1 << STT_OBJECT) | (1 << STT_FUNC) | (1 << STT_COMMON) |
                           (1 << STT_TLS) | (1 << STT_GNU_IFUNC))) == 0)
        {
            return false;
        }

        return name == symname;
    }

    DynamicInfo retrieve_dynamic_info(const ElfW(Dyn) * dyn_begin,
                                      // cppcheck-suppress unknownMacro
                                      ElfW(Addr) base_address)
    {

        // Addresses are sometimes relative sometimes absolute
        // * on musl, addresses are relative
        // * on glibc, addresses are absolutes
        // (https://elixir.bootlin.com/glibc/glibc-2.36/source/elf/get-dynamic-info.h#L84)
        auto correct_address = [base_address](ElfW(Addr) ptr) { return ptr > base_address ? ptr : base_address + ptr; };

        const char* strtab = nullptr;
        size_t strtab_size = 0;
        const ElfW(Sym)* symtab = nullptr;
        const ElfW(Rel)* rels = nullptr;
        size_t rels_size = 0;
        const ElfW(Rela)* relas = nullptr;
        size_t relas_size = 0;
        const ElfW(Rela)* jmprels = nullptr;
        size_t jmprels_size = 0;
        const uint32_t* elf_hash = nullptr;
        const uint32_t* gnu_hash = nullptr;
        ElfW(Sword) pltrel_type = 0;

        for (const auto* it = dyn_begin; it->d_tag != DT_NULL; ++it)
        {
            switch (it->d_tag)
            {
                case DT_STRTAB:
                    strtab = reinterpret_cast<const char*>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_STRSZ:
                    strtab_size = it->d_un.d_val;
                    break;
                case DT_SYMTAB:
                    symtab = reinterpret_cast<const ElfW(Sym)*>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_HASH:
                    // \fixme{nsavoire} Avoid processing DT_HASH since it sometimes points in
                    // kernel address range on Centos 7...

                    // elf_hash =
                    //     reinterpret_cast<const uint32_t
                    //     *>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_GNU_HASH:
                    gnu_hash = reinterpret_cast<const uint32_t*>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_REL:
                    rels = reinterpret_cast<const ElfW(Rel)*>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_RELA:
                    relas = reinterpret_cast<const ElfW(Rela)*>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_JMPREL:
                    jmprels = reinterpret_cast<const ElfW(Rela)*>(correct_address(it->d_un.d_ptr));
                    break;
                case DT_RELSZ:
                    rels_size = it->d_un.d_val;
                    break;
                case DT_RELASZ:
                    relas_size = it->d_un.d_val;
                    break;
                case DT_PLTRELSZ:
                    jmprels_size = it->d_un.d_val;
                    break;
                case DT_PLTREL:
                    pltrel_type = it->d_un.d_val;
                    break;
                default:
                    break;
            }
        }

        if (pltrel_type != DT_RELA)
        {
            jmprels = nullptr;
            jmprels_size = 0;
        }

        uint32_t sym_count =
            gnu_hash ? gnu_hash_symbol_count(gnu_hash) : (elf_hash ? elf_hash_symbol_count(elf_hash) : 0);

        return {.strtab = {strtab, strtab_size},
                .symtab = {symtab, sym_count},
                .rels = {rels, rels_size / sizeof(ElfW(Rel))},
                .relas = {relas, relas_size / sizeof(ElfW(Rela))},
                .jmprels = {jmprels, jmprels_size / sizeof(ElfW(Rela))},
                .elf_hash = elf_hash,
                .gnu_hash = gnu_hash,
                .base_address = base_address};
    }

    std::optional<DynamicInfo> retrieve_dynamic_info(const dl_phdr_info& info, bool exclude_self = false)
    {
        const ElfW(Phdr)* phdr_dynamic = nullptr;

        for (auto phdr = info.dlpi_phdr, end = phdr + info.dlpi_phnum; phdr != end; ++phdr)
        {
            if (phdr->p_type == PT_DYNAMIC)
            {
                phdr_dynamic = phdr;
            }
            if (exclude_self && phdr->p_type == PT_LOAD && phdr->p_flags & PF_X)
            {
                ElfW(Addr) local_symbol_addr = reinterpret_cast<ElfW(Addr)>(
                    static_cast<std::optional<DynamicInfo> (*)(const dl_phdr_info&, bool)>(&retrieve_dynamic_info));
                if (phdr->p_vaddr + info.dlpi_addr <= local_symbol_addr &&
                    local_symbol_addr < phdr->p_vaddr + info.dlpi_addr + phdr->p_memsz)
                {
                    return std::nullopt;
                }
            }
        }

        if (!phdr_dynamic)
        {
            return std::nullopt;
        }

        const ElfW(Dyn)* dyn_begin = reinterpret_cast<const ElfW(Dyn)*>(info.dlpi_addr + phdr_dynamic->p_vaddr);

        DynamicInfo dyn_info = retrieve_dynamic_info(dyn_begin, info.dlpi_addr);

        if (dyn_info.strtab.empty() || dyn_info.symtab.empty() || !(dyn_info.elf_hash || dyn_info.gnu_hash))
        {
            return std::nullopt;
        }

        return dyn_info;
    }

    class SymbolLookup
    {
    public:
        explicit SymbolLookup(std::string_view symname, bool accept_null_sized_symbol, uint64_t not_sym = 0) :
            _symname(symname), _not_sym(not_sym), _sym{}, _accept_null_sized_symbol(accept_null_sized_symbol)
        {
        }

        bool operator()(std::string_view object_name, const DynamicInfo& dyn_info)
        {
            const ElfW(Sym)* s = nullptr;
            if (dyn_info.gnu_hash)
            {
                s = gnu_hash_lookup(dyn_info.strtab.data(), dyn_info.symtab.data(), dyn_info.gnu_hash, _symname);
            }
            else if (dyn_info.elf_hash)
            {
                s = elf_hash_lookup(dyn_info.strtab.data(), dyn_info.symtab.data(), dyn_info.elf_hash, _symname);
            }
            if (s && (_accept_null_sized_symbol || s->st_size > 0) && (s->st_value + dyn_info.base_address != _not_sym))
            {
                _sym = *s;
                _sym.st_value = s->st_value + dyn_info.base_address;
                _object_name = object_name;
                return true;
            }
            return false;
        }

        LookupResult result()
        {
            return {_sym, _object_name};
        }

    private:
        std::string_view _symname;
        uint64_t _not_sym;
        ElfW(Sym) _sym;
        std::string _object_name;
        bool _accept_null_sized_symbol;
    };

    void override_entry(ElfW(Addr) entry_addr, uint64_t new_value)
    {
        static long const page_size = sysconf(_SC_PAGESIZE);
        auto* aligned_addr = reinterpret_cast<void*>(entry_addr & ~(page_size - 1));
        if (mprotect(aligned_addr, page_size, PROT_READ | PROT_WRITE) == 0)
        {
            write64(entry_addr, new_value);
        }
    }

} // namespace

// https://flapenguin.me/elf-dt-hash
const ElfW(Sym) *
    elf_hash_lookup(const char* strtab, const ElfW(Sym) * symtab, const uint32_t* hashtab, std::string_view symname)
{
    const uint32_t hash = elf_hash(symname);

    const uint32_t nbuckets = *(hashtab++);
    ++hashtab;
    const uint32_t* buckets = hashtab;
    hashtab += nbuckets;
    const uint32_t* chain = hashtab;

    for (auto symidx = buckets[hash % nbuckets]; symidx != STN_UNDEF; symidx = chain[symidx])
    {
        const auto& sym = symtab[symidx];
        if (check(sym, strtab + sym.st_name, symname))
        {
            return &sym;
        }
    }
    return nullptr;
}

// https://flapenguin.me/elf-dt-gnu-hash
const ElfW(Sym) *
    gnu_hash_lookup(const char* strtab, const ElfW(Sym) * symtab, const uint32_t* hashtab, std::string_view symname)
{
    const uint32_t nbuckets = *(hashtab++);
    const uint32_t symbias = *(hashtab++);
    const uint32_t bloom_size = *(hashtab++);
    const uint32_t bloom_shift = *(hashtab++);
    const ElfW(Addr)* bloom = reinterpret_cast<const ElfW(Addr)*>(hashtab);
    hashtab += __ELF_NATIVE_CLASS / (CHAR_BIT * sizeof(uint32_t)) * bloom_size;
    const uint32_t* buckets = hashtab;
    hashtab += nbuckets;
    const uint32_t* chain_zero = hashtab - symbias;

    if (nbuckets == 0)
    {
        return nullptr;
    }

    const uint32_t hash = gnu_hash(symname);

    ElfW(Addr) bitmask_word = bloom[(hash / __ELF_NATIVE_CLASS) & (bloom_size - 1)];
    uint32_t const hashbit1 = hash & (__ELF_NATIVE_CLASS - 1);
    uint32_t const hashbit2 = (hash >> bloom_shift) & (__ELF_NATIVE_CLASS - 1);

    if (!((bitmask_word >> hashbit1) & (bitmask_word >> hashbit2) & 1))
    {
        return nullptr;
    }

    uint32_t symidx = buckets[hash % nbuckets];
    if (symidx == 0)
    {
        return nullptr;
    }

    while (true)
    {
        const uint32_t h = chain_zero[symidx];
        if (((h ^ hash) >> 1) == 0)
        {
            const auto& sym = symtab[symidx];
            if (check(sym, strtab + sym.st_name, symname))
            {
                return &sym;
            }
        }

        if (h & 1)
        {
            break;
        }

        ++symidx;
    }

    return nullptr;
}

uint32_t elf_hash_symbol_count(const uint32_t* hashtab)
{
    return hashtab[1];
}

uint32_t gnu_hash_symbol_count(const uint32_t* hashtab)
{
    const uint32_t nbuckets = *(hashtab++);
    const uint32_t symbias = *(hashtab++);
    const uint32_t bloom_size = *(hashtab++);
    ++hashtab;
    hashtab += __ELF_NATIVE_CLASS / (sizeof(uint32_t) * CHAR_BIT) * bloom_size;
    const uint32_t* buckets = hashtab;
    hashtab += nbuckets;
    const uint32_t* chain_zero = hashtab - symbias;

    if (nbuckets == 0)
    {
        return 0;
    }
    uint32_t idx = *std::max_element(buckets, buckets + nbuckets);
    while (!(chain_zero[idx] & 1))
    {
        ++idx;
    }
    return idx + 1;
}

class SymbolOverride
{
public:
    explicit SymbolOverride(std::string_view symname, uint64_t new_symbol, uint64_t do_not_override_this_symbol) :
        _symname(symname), _new_symbol(new_symbol), _do_not_override_this_symbol(do_not_override_this_symbol)
    {
    }

    template <typename Reloc>
    void process_relocation(Reloc& reloc, const DynamicInfo& dyn_info) const
    {
        auto index = ELF64_R_SYM(reloc.r_info);
        // \fixme{nsavoire} size of symtab seems incorrect on CentOS 7
        auto symname = dyn_info.strtab.data() + dyn_info.symtab.data()[index].st_name;
        auto addr = reloc.r_offset + dyn_info.base_address;
        if (symname == _symname && addr != _do_not_override_this_symbol)
        {
            override_entry(addr, _new_symbol);
        }
    }

    bool operator()(std::string_view object_name, const DynamicInfo& dyn_info)
    {
        if (object_name.find("linux-vdso") != std::string_view::npos ||
            object_name.find("/ld-linux") != std::string_view::npos)
        {
            return false;
        }

        std::for_each(dyn_info.rels.begin(), dyn_info.rels.end(),
                      [&](auto& rel) { process_relocation(rel, dyn_info); });
        std::for_each(dyn_info.relas.begin(), dyn_info.relas.end(),
                      [&](auto& rel) { process_relocation(rel, dyn_info); });
        std::for_each(dyn_info.jmprels.begin(), dyn_info.jmprels.end(),
                      [&](auto& rel) { process_relocation(rel, dyn_info); });
        return false;
    }

private:
    std::string_view _symname;
    uint64_t _new_symbol = 0;
    uint64_t _do_not_override_this_symbol = 0;
};

LookupResult lookup_symbol(std::string_view symbol_name, bool accept_null_sized_symbol, uintptr_t not_this_symbol)
{
    SymbolLookup lookup{symbol_name, accept_null_sized_symbol, not_this_symbol};
    iterate_over_loaded_libraries([&](const dl_phdr_info& info, bool /*is_exe*/) {
        auto dyn_info = retrieve_dynamic_info(info);
        if (dyn_info)
        {
            return lookup(info.dlpi_name, *dyn_info) ? LibraryCallbackStatus::Stop : LibraryCallbackStatus::Continue;
        }
        return LibraryCallbackStatus::Continue;
    });
    return lookup.result();
}

void override_symbol(std::string_view symbol_name, uintptr_t new_symbol, uintptr_t do_not_override_this_symbol)
{
    SymbolOverride symbol_override{symbol_name, new_symbol, do_not_override_this_symbol};
    iterate_over_loaded_libraries([&](const dl_phdr_info& info, bool is_exe) {
        auto dyn_info = retrieve_dynamic_info(info, !is_exe);
        if (dyn_info)
        {
            symbol_override(info.dlpi_name, *dyn_info);
        }
        return LibraryCallbackStatus::Continue;
    });
}

int count_loaded_libraries()
{
    int count = 0;
    iterate_over_loaded_libraries([&](const dl_phdr_info& info, bool) {
        count = info.dlpi_adds;
        return LibraryCallbackStatus::Stop;
    });
    return count;
}

LibraryCallbackStatus iterate_over_loaded_libraries(LibraryCallback callback)
{
    struct CallbackInfo
    {
        LibraryCallback callback;
        bool is_first{true};
    };
    CallbackInfo callback_info{callback};
    return static_cast<LibraryCallbackStatus>(dl_iterate_phdr(
        [](dl_phdr_info* info, size_t /*size*/, void* data) {
            auto* local_callback = static_cast<CallbackInfo*>(data);
            const bool is_exe = local_callback->is_first;
            local_callback->is_first = false;
            return static_cast<int>((local_callback->callback)(*info, is_exe));
        },
        &callback_info));
}

bool SymbolOverrides::register_override(std::string_view symbol_name, uintptr_t new_symbol, uintptr_t* ref_symbol,
                                        uintptr_t do_not_override_this_symbol)
{
    return _overrides.try_emplace(std::string{symbol_name}, ref_symbol, new_symbol, do_not_override_this_symbol).second;
}

void SymbolOverrides::apply_overrides()
{
    // Static / global variables bound to an extern function symbol as:
    // `static decltype(&::malloc) malloc_ref = &::malloc;`
    // appear as relocations to the extern function in the generated shared
    // library.
    // Their value is initialized by the dynamic linker when library is
    // loaded.
    // This means that we will override these relocations with our hooks if we are
    // not careful.
    // Moreover ld < 3.38 will initialize these relocations with the
    // local <symbol>@plt address and during relocation processing they will be
    // replaced with the global <symbol>@plt (eg. from the executable).
    // That means that if we use these as the reference function to call in our
    // hooks, we will end up calling <symbol>@plt from exe and incidentally our
    // hook since we will have overriden <symbol>@plt from exe with our hook,
    // causing infinite recursion.
    // Note that ld >= 38 seems to initialize these relocation with null, and
    // during relocation processing they are replaced with the global <symbol>
    // address, not the global <symbol>@plt address.

    // To workaround this, we don't initialize `<symbol>Hook::ref` and do an
    // explicit lookup to determine <symbol> address. We exclude zero-sized
    // symbols to avoid finding the address of <symbol>@plt instead of <symbol>.
    // That's also why we don't use dlsym, because it might return <symbol>@plt
    // address.
    // Note that we might be able to use dlsym(RTLD_NEXT, <symbol>) to the same
    // effect.

    for (auto& [symbol_name, override] : _overrides)
    {
        auto result = ddprof::lookup_symbol(symbol_name, false);

        if (result.symbol.st_value > 0 && result.symbol.st_size > 0)
        {
            *override.ref_symbol = result.symbol.st_value;
            Log::Debug("Found symbol ", symbol_name, " in ", result.object_name, ": 0x", std::hex,
                       result.symbol.st_value, " size=", std::dec, result.symbol.st_size);
        }
        else
        {
            Log::Debug("Unable to find symbol ", symbol_name);
        }
    }

    update_overrides();
}

void SymbolOverrides::restore_overrides()
{
    iterate_over_loaded_libraries([&](const dl_phdr_info& info, bool /*is_exe*/) {
        restore_library_overrides(info.dlpi_name, info.dlpi_addr);
        return LibraryCallbackStatus::Continue;
    });
    _revert_info_per_library.clear();
}

void SymbolOverrides::restore_library_overrides(std::string_view library_name, uintptr_t base_address)
{
    auto it = _revert_info_per_library.find(base_address);
    if (it == _revert_info_per_library.end() || it->second.library_name != library_name)
    {
        return;
    }

    auto& revert_info = it->second;
    for (auto& [address, old_value] : revert_info.old_value_per_address)
    {
        Log::Debug("Restoring symbol @0x", std::hex, address, " in library ", revert_info.library_name, " : old:0x",
                   load64(address), " new:0x", old_value);
        write64(address, old_value);
    }
}

void SymbolOverrides::update_overrides()
{
    auto nb_loaded_libraries = count_loaded_libraries();
    if (nb_loaded_libraries == _nb_loaded_libs)
    {
        return;
    }

    _nb_loaded_libs = nb_loaded_libraries;

    for (auto& [base_address, revert_info] : _revert_info_per_library)
    {
        revert_info.processed = false;
    }

    iterate_over_loaded_libraries([this](const dl_phdr_info& info, bool is_exe) {
        // Avoid overriding symbols in lib profiling, except if it is statically
        // linked into the executable.
        // We cannot rely on `dl_phdr_info.dlpi_name` being an empty string for the
        // exe, because musl passes the absolute path of the executable instead.
        auto maybe_dyn_info = retrieve_dynamic_info(info, !is_exe);
        if (maybe_dyn_info)
        {
            apply_overrides_to_library(*maybe_dyn_info, info.dlpi_name);
        }
        return LibraryCallbackStatus::Continue;
    });

    auto it = _revert_info_per_library.begin();
    while (it != _revert_info_per_library.end())
    {
        if (!it->second.processed)
        {
            // library not present, remove it
            it = _revert_info_per_library.erase(it);
        }
        else
        {
            ++it;
        }
    }
}

void SymbolOverrides::apply_overrides_to_library(const DynamicInfo& dyn_info, std::string_view library_name)
{

    if (library_name.find("linux-vdso") != std::string_view::npos ||
        library_name.find("/ld-linux") != std::string_view::npos)
    {
        return;
    }

    auto [it, inserted] = _revert_info_per_library.try_emplace(dyn_info.base_address, library_name);

    LibraryRevertInfo& revert_info = it->second;
    revert_info.processed = true;

    // not a new library, skip
    if (!inserted)
    {
        return;
    }

    process_relocations(dyn_info, dyn_info.rels, revert_info);
    process_relocations(dyn_info, dyn_info.relas, revert_info);
    process_relocations(dyn_info, dyn_info.jmprels, revert_info);
}

template <typename Reloc>
void SymbolOverrides::process_relocations(const DynamicInfo& dyn_info, shared::span<Reloc> relocs,
                                          LibraryRevertInfo& revert_info)
{
    auto end = _overrides.end();

    for (auto& reloc : relocs)
    {
        auto index = ELF64_R_SYM(reloc.r_info);
        auto symname = dyn_info.strtab.data() + dyn_info.symtab.data()[index].st_name;

        auto it = _overrides.find(symname);
        if (it != end)
        {
            auto& override = it->second;
            auto addr = reloc.r_offset + dyn_info.base_address;
            // Override only if :
            // * the symbol was not previously overridden (might happen because
            //   different symbol names might point to the same address)
            // * the symbol address is not equal to the do_not_override_this_symbol
            //   value (ie. we explicitly don't want to touch this address)
            // * the ref symbol is not null (ie. we were able to lookup the ref
            //   symbol)
            if (addr != override.do_not_override_this_symbol && !revert_info.old_value_per_address.contains(addr) &&
                *override.ref_symbol != 0)
            {
                revert_info.old_value_per_address[addr] = load64(addr);
                Log::Debug("Overriding symbol ", symname, "@0x", std::hex, addr, " in library ",
                           revert_info.library_name, ": old:0x", load64(addr), " new:0x", override.new_symbol);
                override_entry(addr, override.new_symbol);
            }
        }
    }
}

} // namespace ddprof
