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

void ddog__free_compressed_files(ddog__buf* bufs, size_t files_len)
{
    if (bufs == NULL)
    {
        return;
    }
    for (size_t i = 0; i < files_len; i++)
    {
        ddog__buf_free(&bufs[i]);
    }
    free(bufs);
}

ddog_error_code ddog__compress_files(const ddog_prof_exporter_file* files, size_t files_len, ddog__buf** out_bufs)
{
    *out_bufs = NULL;
    if (files_len == 0)
    {
        return DDOG_OK;
    }
    ddog__buf* bufs = (ddog__buf*)calloc(files_len, sizeof(ddog__buf));
    if (bufs == NULL)
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to allocate compressed file buffers");
    }
    for (size_t i = 0; i < files_len; i++)
    {
        ddog__buf_init(&bufs[i]);
        if (!ddog__zstd_compress(files[i].file.ptr, files[i].file.len, &bufs[i]))
        {
            ddog__free_compressed_files(bufs, files_len);
            return ddog__fail(DDOG_ERR_IO, "failed to zstd-compress file '%.*s'", (int)files[i].name.len, files[i].name.ptr);
        }
        if (bufs[i].len > DDOG_MAX_COMPRESSED_FILE_SIZE)
        {
            ddog__free_compressed_files(bufs, files_len);
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "compressed file '%.*s' is larger than %u bytes",
                              (int)files[i].name.len, files[i].name.ptr, DDOG_MAX_COMPRESSED_FILE_SIZE);
        }
    }
    *out_bufs = bufs;
    return DDOG_OK;
}
