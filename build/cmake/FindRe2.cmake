include(ExternalProject)

SET(RE2_VERSION "2018-10-01")
# The build folder depends on the version, so a version bump fetches and builds re2 again
SET(RE2_PREFIX ${CMAKE_CURRENT_BINARY_DIR}/re2-${RE2_VERSION})
SET(RE2_SOURCE_DIR ${RE2_PREFIX}/src/re2)

set (DOWNLOAD_COMMAND ${CMAKE_COMMAND} -DPROJECT_NAME=re2 -DPROJECT_REPOSITORY=https://github.com/google/re2.git -DPROJECT_BRANCH=${RE2_VERSION} -P ${CMAKE_SOURCE_DIR}/build/cmake/git-clone-quiet-once.cmake)

if (ISMACOS)
    SET (OSXRE2BUILDCOMMAND
            echo "Building Re2 Arm64" &&
            rm -f -r ${RE2_SOURCE_DIR}/obj &&
            ${CMAKE_COMMAND} -E env MAKEFLAGS=-s LDFLAGS=-arch\ arm64 ARFLAGS=-r\ -s\ -c CXXFLAGS=-O3\ -g\ -fPIC\ -target\ arm64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION}\ -Wno-unused-but-set-variable\ -D_GLIBCXX_USE_CXX11_ABI=0 $(MAKE) -j obj/libre2.a &&
            mv ${RE2_SOURCE_DIR}/obj/libre2.a ${RE2_SOURCE_DIR}/libre2.arm64.a &&
            echo "Building Re2 X86_64" &&
            rm -f -r ${RE2_SOURCE_DIR}/obj &&
            ${CMAKE_COMMAND} -E env MAKEFLAGS=-s LDFLAGS=-arch\ x86_64 ARFLAGS=-r\ -s\ -c CXXFLAGS=-O3\ -g\ -fPIC\ -target\ x86_64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION}\ -Wno-unused-but-set-variable\ -D_GLIBCXX_USE_CXX11_ABI=0 $(MAKE) -j obj/libre2.a &&
            mv ${RE2_SOURCE_DIR}/obj/libre2.a ${RE2_SOURCE_DIR}/libre2.x86_64.a &&
            echo "Creating Re2 universal binary" &&
            lipo ${RE2_SOURCE_DIR}/libre2.arm64.a ${RE2_SOURCE_DIR}/libre2.x86_64.a -create -output ${RE2_SOURCE_DIR}/obj/libre2.a
    )
    ExternalProject_Add(re2
        PREFIX ${RE2_PREFIX}
        DOWNLOAD_COMMAND ${DOWNLOAD_COMMAND}
        TIMEOUT 5
        INSTALL_COMMAND ""
        CONFIGURE_COMMAND ""
        UPDATE_COMMAND ""
        BUILD_IN_SOURCE TRUE
        BUILD_COMMAND ${OSXRE2BUILDCOMMAND}
        BUILD_BYPRODUCTS ${RE2_SOURCE_DIR}/obj/libre2.a
    )

    set_property(TARGET re2 PROPERTY JOB_SERVER_AWARE TRUE)

elseif(ISLINUX)
    ExternalProject_Add(re2
        PREFIX ${RE2_PREFIX}
        DOWNLOAD_COMMAND ${DOWNLOAD_COMMAND}
        TIMEOUT 5
        INSTALL_COMMAND ""
        CONFIGURE_COMMAND ""
        UPDATE_COMMAND ""
        BUILD_IN_SOURCE TRUE
        BUILD_COMMAND ${CMAKE_COMMAND} -E env ARFLAGS=-r\ -s\ -c CXXFLAGS=-O3\ -g\ -fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0 $(MAKE) -j
        BUILD_BYPRODUCTS ${RE2_SOURCE_DIR}/obj/libre2.a
    )
endif()

ExternalProject_Get_property(re2 SOURCE_DIR)

get_property(FOLDERS_TO_DELETE DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES)
set_property(DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES "${FOLDERS_TO_DELETE};${SOURCE_DIR};${RE2_PREFIX}/src/re2_gitclone;${RE2_PREFIX}/src/re2-stamp")

add_library(re2-lib STATIC IMPORTED)

set_target_properties(re2-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${RE2_SOURCE_DIR}/
    IMPORTED_LOCATION ${RE2_SOURCE_DIR}/obj/libre2.a
)

add_dependencies(re2-lib re2)
