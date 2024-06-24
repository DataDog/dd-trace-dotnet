#include "common.h"
#include "unistd.h"
#include <dlfcn.h>
#include <errno.h>
#include <stdio.h>

int (*volatile dd_set_shared_memory)(volatile int*) = NULL;

__attribute__((visibility("hidden"))) inline int __dd_set_shared_memory(volatile int* mem)
{
    // make a copy to avoid race
    int (*volatile set_shared_memory)(volatile int*) = dd_set_shared_memory;

    if (set_shared_memory == NULL)
        return 0;
    return set_shared_memory(mem);
}

__attribute__((visibility("hidden"))) inline int is_interrupted_by_profiler(int rc, int error_code, int interrupted_by_profiler)
{
    return rc == -1L && error_code == EINTR && interrupted_by_profiler != 0;
}


char *dlerror(void) __attribute__((weak));
static void *s_libdl_handle = NULL;
void *dlsym(void *handle, const char *symbol) __attribute__((weak));

static __typeof(dlerror) *s_dlerror = &dlerror;
static __typeof(dlopen) *s_dlopen = &dlopen;
void *__libc_dlopen_mode(const char *filename, int flag) __attribute__((weak));
void *__libc_dlsym(void *handle, const char *symbol) __attribute__((weak));

static void *__dd_dlopen(const char *filename, int flags) {
  if (!s_dlopen) {
    // if libdl.so is not loaded, use __libc_dlopen_mode
    s_dlopen = __libc_dlopen_mode;
  }
  if (s_dlopen) {
    void *ret = s_dlopen(filename, flags);
    if (!ret && s_dlerror) {
      fprintf(stderr, "Failed to dlopen %s (%s)\n", filename, s_dlerror());
    }
    return ret;
  }
  // Should not happen
  return NULL;
}

static void ensure_libdl_is_loaded() {
  if (!dlsym && !s_libdl_handle) {
    s_libdl_handle = __dd_dlopen("libdl.so.2", RTLD_GLOBAL | RTLD_NOW);
  }
}

__attribute__((visibility("hidden")))
static void *__dd_dlsym(void *handle, const char *symbol) {
  static __typeof(dlsym) *dlsym_ptr = &dlsym;
  if (!dlsym_ptr) {
    // dlsym is not available: meaning we are on glibc and libdl.so was not
    // loaded at startup

    // First ensure that libdl.so is loaded
    if (!s_libdl_handle) {
      ensure_libdl_is_loaded();
    }

    if (s_libdl_handle) {
      // locate dlsym in libdl.so by using internal libc.so.6 function
      // __libc_dlsym.
      // Note that we need dlsym because __libc_dlsym does not provide
      // RTLD_DEFAULT/RTLD_NEXT functionality.
      dlsym_ptr = __libc_dlsym(s_libdl_handle, "dlsym");
    }

    // Should not happen
    if (!dlsym_ptr) {
      return NULL;
    }
  }

  return dlsym_ptr(handle, symbol);
}