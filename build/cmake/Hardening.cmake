# Hardening: make sure no target ever requests an executable stack.
# Without this, glibc >= 2.41 (Debian 13 "trixie", Fedora 40, etc.)
# rejects the shared library with:
#   "cannot enable executable stack as shared object requires"
#
# Include this module once (from the repo root) and every subdirectory
# inherits the flags automatically.

if (ISLINUX)
    add_compile_options("$<$<COMPILE_LANGUAGE:ASM>:-Wa,--noexecstack>")
    add_link_options(-Wl,-z,noexecstack)
endif()
