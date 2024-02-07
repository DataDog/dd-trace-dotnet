# ******************************************************
# Project definition
# ******************************************************
project("async-profiler" VERSION 2.41.0)

option(RUN_ASAN "Build with Clang Undefined-Behavior Sanitizer" OFF)
option(RUN_UBSAN "Build with Clang Undefined-Behavior Sanitizer" OFF)

message(STATUS "Run Clang Address Sanitizer: " ${RUN_ASAN})
message(STATUS "Run Clang Undefined-Behavior Sanitizer: " ${RUN_UBSAN})

# ******************************************************
# Compiler options
# ******************************************************
set(CMAKE_CXX_STANDARD 20)

# Sets compiler options
add_compile_options(-std=c++20 -fPIC -fms-extensions -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

if (IS_ALPINE)
    add_compile_options(-DDD_ALPINE)
endif()

if (RUN_ASAN)
    add_compile_options(-g -fsanitize=address -fno-omit-frame-pointer -DDD_SANITIZERS)
endif()

if (RUN_UBSAN)
    add_compile_options(-fsanitize=undefined -g -fno-omit-frame-pointer -fno-sanitize-recover=all -DDD_SANITIZERS)
endif()

if(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
endif()

if (BIT64)
    add_compile_options(-DBIT64)
    add_compile_options(-DHOST_64BIT)
endif()

if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DBX86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()


# ******************************************************
# Environment detection
# ******************************************************

SET(PROFILER_BASENAME async-profiler-lib)
SET(PROFILER_STATIC_LIB_NAME ${PROFILER_BASENAME}.static)
SET(PROFILER_SHARED_LIB_NAME ${PROFILER_BASENAME})

SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR})


# ******************************************************
# Define static target
# ******************************************************

add_library(${PROFILER_STATIC_LIB_NAME} STATIC
    symbols_linux.cpp
    dwarf.cpp
    codeCache.cpp
    mutex.cpp
    os.cpp
)

if (ISAMD64)
    target_sources(${PROFILER_STATIC_LIB_NAME} PRIVATE
        stackFrame_x64.cpp
    )
elseif (ISX86)
    target_sources(${PROFILER_STATIC_LIB_NAME} PRIVATE
        stackFrame_i386.cpp
    )
elseif (ISARM64)
    target_sources(${PROFILER_STATIC_LIB_NAME} PRIVATE
        stackFrame_aarch64.cpp
    )
elseif (ISARM)
    target_sources(${PROFILER_STATIC_LIB_NAME} PRIVATE
        stackFrame_arm.cpp
    )
endif()

set_target_properties(${PROFILER_STATIC_LIB_NAME} PROPERTIES PREFIX "")

# Define linker libraries

if (RUN_UBSAN)
    target_link_libraries(${PROFILER_STATIC_LIB_NAME} -fsanitize=undefined)
endif()

if (RUN_ASAN)
    target_link_libraries(${PROFILER_STATIC_LIB_NAME} -fsanitize=address)
endif()

if (NOT RUN_ASAN AND NOT RUN_UBSAN)
    target_link_libraries(${PROFILER_STATIC_LIB_NAME} -Wl,--no-undefined)
endif()

target_compile_definitions(${PROFILER_STATIC_LIB_NAME} PUBLIC "-D_GLIBCXX_USE_CXX11_ABI=0")

