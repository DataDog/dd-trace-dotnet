// Thin wrapper around libzstd. Isolated behind this tiny header so the rest
// of the codebase never touches <zstd.h> directly.
#pragma once

#include "datadog_poc/profiling.h"
#include "internal.h"

// Compresses input[0..input_len) and appends the compressed bytes to `out`.
// Returns false on failure (libzstd error, or allocation failure).
bool ddog__zstd_compress(const uint8_t* input, size_t input_len, ddog__buf* out);

// libdatadog caps each compressed additional file at 10 MiB
// (profile_exporter.rs build_multipart_form).
#define DDOG_MAX_COMPRESSED_FILE_SIZE (10u * 1024u * 1024u)

// Compresses each file into its own buffer (*out_bufs, files_len entries,
// to be released with ddog__free_compressed_files). Fails, with the last
// error set, if a compressed file exceeds DDOG_MAX_COMPRESSED_FILE_SIZE.
ddog_error_code ddog__compress_files(const ddog_prof_exporter_file* files, size_t files_len, ddog__buf** out_bufs);
void ddog__free_compressed_files(ddog__buf* bufs, size_t files_len);
