include(ExternalProject)

SET(MANAGED_LOADER_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/tracer/src/bin/ProfilerResources/netcoreapp2.0)


# Set specific custom commands to embed the loader
if (ISMACOS)
    SET(PDB_COMMAND touch stub.c &&
            gcc -o stub.arm64.o -c stub.c -target arm64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION} &&
            ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.arm64.o -sectcreate binary pdb Datadog.Trace.ClrProfiler.Managed.Loader.pdb stub.arm64.o &&
            gcc -o stub.x86_64.o -c stub.c -target x86_64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION} &&
            ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.x86_64.o -sectcreate binary pdb Datadog.Trace.ClrProfiler.Managed.Loader.pdb stub.x86_64.o &&
            lipo Datadog.Trace.ClrProfiler.Managed.Loader.pdb.arm64.o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.x86_64.o -create -output Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o)
    SET(DLL_COMMAND touch stub.c &&
            gcc -o stub.arm64.o -c stub.c -target arm64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION} &&
            ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.arm64.o -sectcreate binary dll Datadog.Trace.ClrProfiler.Managed.Loader.dll stub.arm64.o &&
            gcc -o stub.x86_64.o -c stub.c -target x86_64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION} &&
            ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.x86_64.o -sectcreate binary dll Datadog.Trace.ClrProfiler.Managed.Loader.dll stub.x86_64.o &&
            lipo Datadog.Trace.ClrProfiler.Managed.Loader.dll.arm64.o Datadog.Trace.ClrProfiler.Managed.Loader.dll.x86_64.o -create -output Datadog.Trace.ClrProfiler.Managed.Loader.dll.o)
elseif(ISLINUX)
    SET(DLL_COMMAND ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o Datadog.Trace.ClrProfiler.Managed.Loader.dll)
    SET(PDB_COMMAND ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o Datadog.Trace.ClrProfiler.Managed.Loader.pdb)
endif()

ExternalProject_Add(managed-loader-dll
    INSTALL_COMMAND ""
    DOWNLOAD_COMMAND cp ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll <BINARY_DIR>/Datadog.Trace.ClrProfiler.Managed.Loader.dll
    BUILD_COMMAND ""
    CONFIGURE_COMMAND ""
    COMMAND ${DLL_COMMAND}
    BUILD_BYPRODUCTS ${CMAKE_CURRENT_BINARY_DIR}/managed-loader-dll-prefix/src/managed-loader-dll-build/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
)

ExternalProject_Add(managed-loader-pdb
    INSTALL_COMMAND ""
    DOWNLOAD_COMMAND cp ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb <BINARY_DIR>/Datadog.Trace.ClrProfiler.Managed.Loader.pdb
    BUILD_COMMAND ""
    CONFIGURE_COMMAND ""
    COMMAND ${PDB_COMMAND}
    BUILD_BYPRODUCTS ${CMAKE_CURRENT_BINARY_DIR}/managed-loader-pdb-prefix/src/managed-loader-pdb-build/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
)

SET(GENERATED_OBJ_FILES
    ${CMAKE_CURRENT_BINARY_DIR}/managed-loader-dll-prefix/src/managed-loader-dll-build/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
    ${CMAKE_CURRENT_BINARY_DIR}/managed-loader-pdb-prefix/src/managed-loader-pdb-build/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
)
SET_SOURCE_FILES_PROPERTIES(
        ${GENERATED_OBJ_FILES}
        PROPERTIES
        EXTERNAL_OBJECT true
        GENERATED true
)

add_library(managed-loader-objs OBJECT IMPORTED)

set_target_properties(managed-loader-objs PROPERTIES
    IMPORTED_OBJECTS "${GENERATED_OBJ_FILES}"
)

add_dependencies(managed-loader-objs managed-loader-dll managed-loader-pdb)