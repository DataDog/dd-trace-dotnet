#!/usr/bin/env bash

git clone -n --depth=1 https://git.alpinelinux.org/aports &&
    pushd aports &&
    git sparse-checkout set --no-cone main/llvm16 main/clang16 &&
    git checkout &&
    popd &&
    # copy patches
    cp aports/main/llvm16/*.patch llvm-project &&
    cp aports/main/clang16/*.patch llvm-project/clang &&
    # apply patches
    for j in llvm-project llvm-project/clang; do
    pushd $j;
    for i in `ls .`; do
		case ${i%::*} in
           # This patch enables Fortify source feature by default (https://www.gnu.org/software/libc/manual/html_node/Source-Fortification.html)
           # Before clang-16, this patch was not applied, and fortify was activated only if the compilation
           # option _FORTIFY_SOURCE was set to a value greater than 0.
           # Starting from clang-16, they activated it by default but our code breaks because stdio fortified functions call `__builtin_va_arg_pack()`
           # which is not implemented by LLVM/Clang folks. https://bugs.llvm.org/show_bug.cgi?id=7219 and https://bugs.llvm.org/show_bug.cgi?id=7219
           # By not applying this patch, we make sure that our rebuilt clang-16 behaves the same as older clang versions.
            *002-fortify-enable.patch)
                echo "Skipped - ${i%::*}"
                ;;
			*.patch)
				echo "${i%::*}"
				patch -p1 -i "${i%::*}" || return 1
				;;
		esac
	done
    popd
    done &&
    # cleanup
    rm -rf aports