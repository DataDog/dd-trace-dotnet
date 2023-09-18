#include "common.h"
#include "unistd.h"
#include <errno.h>

int (*volatile dd_set_shared_memory)(int*) = NULL;

__attribute__((visibility("hidden"))) inline int __dd_set_shared_memory(int* mem)
{
    if (dd_set_shared_memory == NULL)
        return 0;
    return dd_set_shared_memory(mem);
}

__attribute__((visibility("hidden"))) inline int is_interrupted_by_profiler(int rc, int error_code, int interrupted_by_profiler)
{
    return rc == -1L && error_code == EINTR && interrupted_by_profiler != 0;
}
