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
 *
 * The same reasoning applies to the fortified (_FORTIFY_SOURCE) entry points
 * (__read_chk, __open_2, ...) defined at the bottom of this file.
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
 * fstat operates on an already-open descriptor. The common open-then-fstat flow
 * (open a CIFS file, then size/validate it) can still hit a Kerberos re-auth and
 * receive EINTR, so it must be wrapped too -- path-based stat alone is not enough.
 */
WRAPPED_FUNCTION(int, fstat, (int, fd)(struct stat*, buf))

/*
 * On glibc < 2.33 (e.g. CentOS 7), stat/lstat/fstat/fstatat are inline functions
 * in the header that call __xstat/__lxstat/__fxstat/__fxstatat with a version
 * argument. Applications compiled against older glibc will call __xstat, not stat.
 * We wrap both unconditionally since this is a universal binary used on both
 * glibc and musl. On musl, these symbols are never called (apps use stat directly).
 */
WRAPPED_FUNCTION(int, __xstat, (int, ver)(const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION(int, __lxstat, (int, ver)(const char*, pathname)(struct stat*, buf))
WRAPPED_FUNCTION(int, __fxstat, (int, ver)(int, fd)(struct stat*, buf))
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
WRAPPED_FUNCTION_RENAMED(int, "__fxstat64", __dd_fxstat64, (int, ver)(int, fd)(struct stat*, buf))
WRAPPED_FUNCTION_RENAMED(int, "__fxstatat64", __dd_fxstatat64, (int, ver)(int, dirfd)(const char*, pathname)(struct stat*, buf)(int, flags))

/*
 * open/openat are variadic (optional mode_t argument when O_CREAT or O_TMPFILE
 * is set). The WRAPPED_FUNCTION macro cannot handle variadic functions, so we
 * implement the same pattern manually.
 */

/*
 * Mirrors glibc's __OPEN_NEEDS_MODE: the optional mode argument is present only
 * for O_CREAT or a *full* O_TMPFILE open.
 *
 * O_TMPFILE is defined as (__O_TMPFILE | O_DIRECTORY), so we must match the
 * ENTIRE bit pattern with "(flags & O_TMPFILE) == O_TMPFILE". Testing
 * "flags & O_TMPFILE" would also be true for a plain directory open such as
 * open(path, O_RDONLY | O_DIRECTORY), which passes no mode argument -- reading
 * it via va_arg would be undefined behavior.
 */
static int __dd_open_needs_mode(int flags)
{
    if ((flags & O_CREAT) != 0)
    {
        return 1;
    }
#ifdef O_TMPFILE
    if ((flags & O_TMPFILE) == O_TMPFILE)
    {
        return 1;
    }
#endif
    return 0;
}

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
    if (__dd_open_needs_mode(flags))
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
    if (__dd_open_needs_mode(flags))
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
    if (__dd_open_needs_mode(flags))
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
    if (__dd_open_needs_mode(flags))
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

/*
 * Fortified (_FORTIFY_SOURCE) entry points.
 *
 * When an application's own code is compiled with -D_FORTIFY_SOURCE, glibc's
 * headers redirect read/pread/open/openat to the fortified symbols below
 * (__read_chk, __open_2, ...) instead of the plain ones. Without wrapping these
 * too, a fortified caller doing SMB/CIFS I/O would bypass our interception and
 * could still surface profiler-induced EINTR.
 *
 * All of these are non-variadic, so they go through WRAPPED_FUNCTION_RENAMED.
 * Notes:
 *   - The *_chk read variants carry a trailing buflen used by glibc to detect
 *     buffer overflows. We forward to the REAL __read_chk/__pread_chk (passing
 *     buflen through) so that bounds check is preserved -- we must NOT redirect
 *     these to plain read/pread.
 *   - __open_2 / __openat_2 are the non-variadic fortified forms used when no
 *     mode argument is passed; glibc itself aborts if a mode would be required,
 *     so we simply forward (no va_arg handling needed).
 *   - These symbols only exist on glibc; on musl they are never referenced, but
 *     we export them unconditionally (the universal binary is built on musl) via
 *     the __asm__ rename so glibc consumers are covered. pread64 uses off_t,
 *     which is ABI-identical to off64_t on our 64-bit targets.
 */
WRAPPED_FUNCTION_RENAMED(ssize_t, "__read_chk", __dd_read_chk, (int, fd)(void*, buf)(size_t, nbytes)(size_t, buflen))
WRAPPED_FUNCTION_RENAMED(ssize_t, "__pread_chk", __dd_pread_chk, (int, fd)(void*, buf)(size_t, nbytes)(off_t, offset)(size_t, buflen))
WRAPPED_FUNCTION_RENAMED(ssize_t, "__pread64_chk", __dd_pread64_chk, (int, fd)(void*, buf)(size_t, nbytes)(off_t, offset)(size_t, buflen))

WRAPPED_FUNCTION_RENAMED(int, "__open_2", __dd_open_2, (const char*, pathname)(int, flags))
WRAPPED_FUNCTION_RENAMED(int, "__open64_2", __dd_open64_2, (const char*, pathname)(int, flags))
WRAPPED_FUNCTION_RENAMED(int, "__openat_2", __dd_openat_2, (int, dirfd)(const char*, pathname)(int, flags))
WRAPPED_FUNCTION_RENAMED(int, "__openat64_2", __dd_openat64_2, (int, dirfd)(const char*, pathname)(int, flags))
