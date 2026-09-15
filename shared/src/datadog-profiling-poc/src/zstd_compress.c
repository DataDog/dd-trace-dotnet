#include "zstd_compress.h"

#include <stdlib.h>
#include <zstd.h>

bool ddog__zstd_compress(const uint8_t* input, size_t input_len, ddog__buf* out)
{
    size_t bound = ZSTD_compressBound(input_len);
    uint8_t* dst = (uint8_t*)malloc(bound);
    if (dst == NULL)
    {
        return false;
    }

    // Level 1, matching libdatadog's own default (ddog_prof_Profile_COMPRESSION_LEVEL).
    size_t compressed_size = ZSTD_compress(dst, bound, input, input_len, 1);
    if (ZSTD_isError(compressed_size))
    {
        free(dst);
        return false;
    }

    bool ok = ddog__buf_append(out, dst, compressed_size);
    free(dst);
    return ok;
}
