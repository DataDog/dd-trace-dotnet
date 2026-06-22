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
 *
 * Large-file (LFS) variants: when callers are compiled with
 * _FILE_OFFSET_BITS=64 (the default for the .NET runtime's native libraries),
 * glibc emits references to the *64 entry points (open64, pread64, __xstat64,
 * ...) instead of the un-suffixed symbols. Because an LD_PRELOAD interposer
 * only intercepts the exact symbol a caller references, we MUST also export the
 * *64 aliases -- otherwise the very SMB/CIFS paths used by the runtime bypass
 * this library entirely.
 *
 * IMPORTANT: this library is built as a single "universal" binary (on Alpine /
 * musl) that is loaded on both glibc and musl hosts. We therefore cannot guard
 * the *64 wrappers with #ifdef __GLIBC__ -- that macro is undefined in the musl
 * build and the *64 symbols would be compiled out, leaving glibc consumers
 * unprotected. Instead we export them unconditionally:
 *   - The exported symbol name is forced with an __asm__ label. The name lives
 *     in a string literal, so it is immune to musl's "#define open64 open"
 *     header macros, and the C identifier we use (e.g. __dd_open64) never
 *     collides with anything.
 *   - We reuse the base types (off_t, struct stat*). On our only targets
 *     (Linux x64 + arm64) off_t is 64-bit and struct stat == struct stat64,
 *     so they are ABI-identical to off64_t / struct stat64; we only forward the
 *     argument through, never dereference it. This avoids depending on
 *     struct stat64 / off64_t, which recent musl no longer provides.
 *   - dlsym(RTLD_NEXT, "open64") resolves the real *64 symbol on glibc. On a
 *     musl host these wrappers are simply never called (musl callers reference
 *     the base symbol), so the unused dlsym result is harmless.
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

/*
 * Same as WRAPPED_FUNCTION but the exported symbol name (asmname, a string
 * literal) is decoupled from the C identifier (cname). Used for the *64 LFS
 * aliases so the exported name bypasses musl header macros -- see the file
 * header comment.
 */
#define WRAPPED_FUNCTION_RENAMED(return_type, asmname, cname, parameters)           \
    static return_type (*__dd_real_##cname)(END(PARAMS_LOOP_0 parameters)) = NULL;  \
                                                                                   \
    return_type cname(END(PARAMS_LOOP_0 parameters)) __asm__(asmname);             \
    return_type cname(END(PARAMS_LOOP_0 parameters))                               \
    {                                                                              \
        if (__dd_real_##cname == NULL)                                             \
        {                                                                          \
            __dd_real_##cname = __dd_dlsym(RTLD_NEXT, asmname);                     \
        }                                                                          \
        volatile int interrupted_by_profiler = 0;                                  \
        __dd_set_shared_memory(&interrupted_by_profiler);                          \
        return_type rc;                                                            \
        do                                                                         \
        {                                                                          \
            interrupted_by_profiler = 0;                                           \
            rc = __dd_real_##cname(END(VAR_LOOP_0 parameters));                    \
        } while (is_interrupted_by_profiler(rc, errno, interrupted_by_profiler));  \
        __dd_set_shared_memory(NULL);                                              \
        return rc;                                                                 \
    }                                                                              \
    static void load_symbols_##cname() __attribute__((constructor));               \
    void load_symbols_##cname()                                                    \
    {                                                                              \
        __dd_real_##cname = __dd_dlsym(RTLD_NEXT, asmname);                         \
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
 * Large-file (LFS) variants -- see the file header comment. These are the
 * symbols the .NET runtime native libraries (libcoreclr.so, libSystem.Native.so)
 * actually reference on glibc when compiled with _FILE_OFFSET_BITS=64. We reuse
 * off_t / struct stat* (ABI-identical to off64_t / struct stat64 on our 64-bit
 * targets) and force the exported name via __asm__ so the universal binary works
 * on both glibc and musl.
 */
WRAPPED_FUNCTION_RENAMED(ssize_t, "pread64", __dd_pread64, (int, fd)(void*, buf)(size_t, count)(off_t, offset))
WRAPPED_FUNCTION_RENAMED(ssize_t, "pwrite64", __dd_pwrite64, (int, fd)(const void*, buf)(size_t, count)(off_t, offset))

WRAPPED_FUNCTION_RENAMED(int, "__xstat64", __dd_xstat64, (int, ver)(const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION_RENAMED(int, "__lxstat64", __dd_lxstat64, (int, ver)(const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION_RENAMED(int, "__fxstatat64", __dd_fxstatat64, (int, ver)(int, dirfd)(const char*, pathname)(struct stat*, buf)(int, flags))

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

/*
 * Large-file variants of open/openat. Same manual variadic handling as above;
 * only the resolved symbol name differs. The C identifiers (__dd_open64 /
 * __dd_openat64) are exported under the names "open64" / "openat64" via an
 * __asm__ label so this compiles and exports correctly in the universal (musl)
 * build -- see the file header comment.
 */

static int (*__dd_real_open64)(const char*, int, mode_t) = NULL;

static void load_symbols_open64() __attribute__((constructor));
void load_symbols_open64()
{
    __dd_real_open64 = __dd_dlsym(RTLD_NEXT, "open64");
}

int __dd_open64(const char* pathname, int flags, ...) __asm__("open64");
int __dd_open64(const char* pathname, int flags, ...)
{
    if (__dd_real_open64 == NULL)
    {
        __dd_real_open64 = __dd_dlsym(RTLD_NEXT, "open64");
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
        rc = __dd_real_open64(pathname, flags, mode);
    } while (is_interrupted_by_profiler(rc, errno, interrupted_by_profiler));
    __dd_set_shared_memory(NULL);
    return rc;
}

static int (*__dd_real_openat64)(int, const char*, int, mode_t) = NULL;

static void load_symbols_openat64() __attribute__((constructor));
void load_symbols_openat64()
{
    __dd_real_openat64 = __dd_dlsym(RTLD_NEXT, "openat64");
}

int __dd_openat64(int dirfd, const char* pathname, int flags, ...) __asm__("openat64");
int __dd_openat64(int dirfd, const char* pathname, int flags, ...)
{
    if (__dd_real_openat64 == NULL)
    {
        __dd_real_openat64 = __dd_dlsym(RTLD_NEXT, "openat64");
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
        rc = __dd_real_openat64(dirfd, pathname, flags, mode);
    } while (is_interrupted_by_profiler(rc, errno, interrupted_by_profiler));
    __dd_set_shared_memory(NULL);
    return rc;
}
