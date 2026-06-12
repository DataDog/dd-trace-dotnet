#define _GNU_SOURCE
#include <dlfcn.h>
#include <errno.h>
#include <fcntl.h>
#include <stdarg.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

#include "common.h"

/*
 * This file wraps filesystem operations to restart them in case the profiler
 * interrupted them via SIGUSR1.
 *
 * On CIFS/SMB mounts with Kerberos authentication, the kernel CIFS driver may
 * return EINTR to userspace when a signal interrupts a filesystem operation
 * during Kerberos re-authentication. Despite SA_RESTART being set on the
 * profiler's signal handler, the CIFS driver internally converts ERESTARTSYS
 * to EINTR in its authentication path, bypassing the kernel's automatic
 * restart mechanism.
 *
 * The wrapping mechanism is identical to socket_operations.c:
 * - Register shared memory so the profiler knows if the thread can be
 *   interrupted (CanBeInterrupted() returns false while inside a wrapper)
 * - If interrupted anyway, retry the syscall when EINTR is caused by the
 *   profiler signal
 *
 * Note: close() is intentionally NOT wrapped with retry. On Linux, close()
 * always releases the fd even when returning EINTR -- retrying would risk
 * closing a reused fd. close() also does not trigger Kerberos re-auth.
 */

#define WRAPPED_FUNCTION(return_type, name, parameters)                           \
    static return_type (*__dd_real_##name)(END(PARAMS_LOOP_0 parameters)) = NULL; \
                                                                                  \
    return_type name(END(PARAMS_LOOP_0 parameters))                               \
    {                                                                             \
        if (__dd_real_##name == NULL)                                             \
        {                                                                         \
            __dd_real_##name = __dd_dlsym(RTLD_NEXT, #name);                      \
        }                                                                         \
        volatile int interrupted_by_profiler = 0;                                 \
        __dd_set_shared_memory(&interrupted_by_profiler);                         \
        return_type rc;                                                           \
        do                                                                        \
        {                                                                         \
            interrupted_by_profiler = 0;                                          \
            rc = __dd_real_##name(END(VAR_LOOP_0 parameters));                    \
        } while (is_interrupted_by_profiler(rc, errno, interrupted_by_profiler)); \
        __dd_set_shared_memory(NULL);                                             \
        return rc;                                                                \
    }                                                                             \
    static void load_symbols_##name() __attribute__((constructor));               \
    void load_symbols_##name()                                                    \
    {                                                                             \
        __dd_real_##name = __dd_dlsym(RTLD_NEXT, #name);                          \
    }

/* Non-variadic wrappers */

WRAPPED_FUNCTION(ssize_t, read, (int, fd)(void*, buf)(size_t, count))
WRAPPED_FUNCTION(ssize_t, write, (int, fd)(const void*, buf)(size_t, count))
WRAPPED_FUNCTION(ssize_t, pread, (int, fd)(void*, buf)(size_t, count)(off_t, offset))
WRAPPED_FUNCTION(ssize_t, pwrite, (int, fd)(const void*, buf)(size_t, count)(off_t, offset))

WRAPPED_FUNCTION(int, stat, (const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION(int, lstat, (const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION(int, fstatat, (int, dirfd)(const char*, pathname)(struct stat*, buf)(int, flags))

/*
 * On glibc < 2.33 (e.g. CentOS 7), stat/lstat/fstatat are inline functions
 * in the header that call __xstat/__lxstat/__fxstatat with a version argument.
 * Applications compiled against older glibc will call __xstat, not stat.
 * We wrap both unconditionally since this is a universal binary used on both
 * glibc and musl. On musl, these symbols are never called (apps use stat directly).
 */
WRAPPED_FUNCTION(int, __xstat, (int, ver)(const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION(int, __lxstat, (int, ver)(const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION(int, __fxstatat, (int, ver)(int, dirfd)(const char*, pathname)(struct stat*, buf)(int, flags))

/*
 * open/openat are variadic (optional mode_t argument when O_CREAT or O_TMPFILE
 * is set). The WRAPPED_FUNCTION macro cannot handle variadic functions, so we
 * implement the same pattern manually.
 */

static int (*__dd_real_open)(const char*, int, mode_t) = NULL;

static void load_symbols_open() __attribute__((constructor));
void load_symbols_open()
{
    __dd_real_open = __dd_dlsym(RTLD_NEXT, "open");
}

int open(const char* pathname, int flags, ...)
{
    if (__dd_real_open == NULL)
    {
        __dd_real_open = __dd_dlsym(RTLD_NEXT, "open");
    }

    mode_t mode = 0;
    if (flags & (O_CREAT
#ifdef O_TMPFILE
                 | O_TMPFILE
#endif
                 ))
    {
        va_list args;
        va_start(args, flags);
        mode = va_arg(args, mode_t);
        va_end(args);
    }

    volatile int interrupted_by_profiler = 0;
    __dd_set_shared_memory(&interrupted_by_profiler);
    int rc;
    do
    {
        interrupted_by_profiler = 0;
        rc = __dd_real_open(pathname, flags, mode);
    } while (is_interrupted_by_profiler(rc, errno, interrupted_by_profiler));
    __dd_set_shared_memory(NULL);
    return rc;
}

static int (*__dd_real_openat)(int, const char*, int, mode_t) = NULL;

static void load_symbols_openat() __attribute__((constructor));
void load_symbols_openat()
{
    __dd_real_openat = __dd_dlsym(RTLD_NEXT, "openat");
}

int openat(int dirfd, const char* pathname, int flags, ...)
{
    if (__dd_real_openat == NULL)
    {
        __dd_real_openat = __dd_dlsym(RTLD_NEXT, "openat");
    }

    mode_t mode = 0;
    if (flags & (O_CREAT
#ifdef O_TMPFILE
                 | O_TMPFILE
#endif
                 ))
    {
        va_list args;
        va_start(args, flags);
        mode = va_arg(args, mode_t);
        va_end(args);
    }

    volatile int interrupted_by_profiler = 0;
    __dd_set_shared_memory(&interrupted_by_profiler);
    int rc;
    do
    {
        interrupted_by_profiler = 0;
        rc = __dd_real_openat(dirfd, pathname, flags, mode);
    } while (is_interrupted_by_profiler(rc, errno, interrupted_by_profiler));
    __dd_set_shared_memory(NULL);
    return rc;
}
