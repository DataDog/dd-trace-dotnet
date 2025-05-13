#define _GNU_SOURCE
#include <dlfcn.h>
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <sys/select.h>
#include <sys/socket.h>
#include <time.h>

#include "common.h"

/*
 * TL;DR This file wraps socket operations to restart them in case the profiler
 * interrupted them.
 * According to man 7 signal, there are system calls won't be restarted by the kernel.
 * In our case, those system calls are not restarted if a timeout was set. So we need
 * handle it.
 * The way we handle it is twofold:
 * - Establishing a communication pipe between the profiler and the wrapping library.
 *   The call to __dd_set_shared_memory set a shared memory area where the profiler
 *   knows if the thread can be interrupted or not, and tell the wrapping library if
 *   the profiler signal interrupted the thread.
 * - Retry the system calls: in case of an interruption by a signal (RC == -1 and
 *   errno == EINTR), if the profiler interrupted the thread (interrupted_by_profiler != 0)
 *   we restart the system calls
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

WRAPPED_FUNCTION(int, select, (int, nfds)(fd_set*, readfds)(fd_set*, writefds)(fd_set*, exceptfds)(struct timeval*, timeout))
WRAPPED_FUNCTION(int, poll, (struct pollfd*, fds)(nfds_t, nfds)(int, timeout))
WRAPPED_FUNCTION(int, accept, (int, sockfd)(struct sockaddr*, addr)(socklen_t*, addrlen))
WRAPPED_FUNCTION(int, accept4, (int, sockfd)(struct sockaddr*, addr)(socklen_t*, addrlen)(int, flags))
WRAPPED_FUNCTION(ssize_t, recv, (int, sockfd)(void*, buf)(size_t, len)(int, flags))
WRAPPED_FUNCTION(ssize_t, recvfrom, (int, sockfd)(void*, buf)(size_t, len)(int, flags)(struct sockaddr*, src_addr)(socklen_t*, addrlen))
WRAPPED_FUNCTION(ssize_t, recvmsg, (int, sockfd)(struct msghdr*, msg)(int, flags))
#ifdef DD_ALPINE
WRAPPED_FUNCTION(int, recvmmsg, (int, sockfd)(struct mmsghdr*, msgvec)(unsigned int, vlen)(unsigned int, flags)(struct timespec*, timeout))
#else
WRAPPED_FUNCTION(int, recvmmsg, (int, sockfd)(struct mmsghdr*, msgvec)(unsigned int, vlen)(int, flags)(DD_CONST struct timespec*, timeout))
#endif
WRAPPED_FUNCTION(int, connect, (int, sockfd)(const struct sockaddr*, addr)(socklen_t, addrlen))
WRAPPED_FUNCTION(ssize_t, send, (int, sockfd)(const void*, buf)(size_t, len)(int, flags))
WRAPPED_FUNCTION(ssize_t, sendto, (int, sockfd)(const void*, buf)(size_t, len)(int, flags)(const struct sockaddr*, dest_addr)(socklen_t, addrlen))
WRAPPED_FUNCTION(ssize_t, sendmsg, (int, sockfd)(const struct msghdr*, msg)(int, flags))
