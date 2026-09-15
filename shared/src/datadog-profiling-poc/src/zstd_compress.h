// Thin wrapper around libzstd. Isolated behind this tiny header so the rest
// of the codebase never touches <zstd.h> directly.
#pragma once

#include "internal.h"

// Compresses input[0..input_len) and appends the compressed bytes to `out`.
// Returns false on failure (libzstd error, or allocation failure).
bool ddog__zstd_compress(const uint8_t* input, size_t input_len, ddog__buf* out);
