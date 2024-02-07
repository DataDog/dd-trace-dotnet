// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0. This product includes software
// developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present
// Datadog, Inc.

#pragma once

#include <stdint.h>

/// Maximum number of different watcher types
#define MAX_TYPE_WATCHER 10

// Maximum depth for a single stack
#define DD_MAX_STACK_DEPTH 512

constexpr int k_size_api_key = 32;

// Linux Inode type
typedef uint64_t inode_t;

typedef int32_t SymbolIdx_t;
typedef int32_t MapInfoIdx_t;
// Elf address (same as the address used with addr2line)
typedef uint64_t ElfAddress_t;
// Offset types : add or substract to address types
typedef ElfAddress_t Offset_t;
// Absolute address (needs to be adjusted with the start address of binary)
typedef ElfAddress_t ProcessAddress_t;
// Address within a binary : adjust doing (process_addr - module_start)
typedef ElfAddress_t FileAddress_t;
// Elf word
typedef uint64_t ElfWord_t;
