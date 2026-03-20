include(ExternalProject)

SET(RE2_VERSION "2018-10-01")

set (DOWNLOAD_COMMAND ${CMAKE_COMMAND} -DPROJECT_NAME=re2 -DPROJECT_REPOSITORY=https://github.com/google/re2.git -DPROJECT_BRANCH=${RE2_VERSION} -P ${CMAKE_SOURCE_DIR}/build/cmake/git-clone-quiet-once.cmake)

if (ISMACOS)
    SET (OSXRE2BUILDCOMMAND
            echo "Building Re2 Arm64" &&
            rm -f -r ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj &&
            ${CMAKE_COMMAND} -E env MAKEFLAGS=-s LDFLAGS=-arch\ arm64 ARFLAGS=-r\ -s\ -c CXXFLAGS=-O3\ -g\ -fPIC\ -target\ arm64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION}\ -Wno-unused-but-set-variable\ -D_GLIBCXX_USE_CXX11_ABI=0 $(MAKE) -j &&
            mv ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj/libre2.a ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/libre2.arm64.a &&
            echo "Building Re2 X86_64" &&
            rm -f -r ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj &&
            ${CMAKE_COMMAND} -E env MAKEFLAGS=-s LDFLAGS=-arch\ x86_64 ARFLAGS=-r\ -s\ -c CXXFLAGS=-O3\ -g\ -fPIC\ -target\ x86_64-apple-darwin${CMAKE_HOST_SYSTEM_VERSION}\ -Wno-unused-but-set-variable\ -D_GLIBCXX_USE_CXX11_ABI=0 $(MAKE) -j &&
            mv ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj/libre2.a ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/libre2.x86_64.a &&
            echo "Creating Re2 universal binary" &&
            lipo ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/libre2.arm64.a ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/libre2.x86_64.a -create -output ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj/libre2.a
    )
    ExternalProject_Add(re2
        DOWNLOAD_COMMAND ${DOWNLOAD_COMMAND}
        TIMEOUT 5
        INSTALL_COMMAND ""
        CONFIGURE_COMMAND ""
        UPDATE_COMMAND ""
        BUILD_IN_SOURCE TRUE
        BUILD_COMMAND ${OSXRE2BUILDCOMMAND}
        BUILD_BYPRODUCTS ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj/libre2.a
    )

    set_property(TARGET re2 PROPERTY JOB_SERVER_AWARE TRUE)

elseif(ISLINUX)
    ExternalProject_Add(re2
        DOWNLOAD_COMMAND ${DOWNLOAD_COMMAND}
        TIMEOUT 5
        INSTALL_COMMAND ""
        CONFIGURE_COMMAND ""
        UPDATE_COMMAND ""
        BUILD_IN_SOURCE TRUE
        BUILD_COMMAND ${CMAKE_COMMAND} -E env ARFLAGS=-r\ -s\ -c CXXFLAGS=-O3\ -g\ -fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0 $(MAKE) -j
        BUILD_BYPRODUCTS ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj/libre2.a
    )
endif()

ExternalProject_Get_property(re2 SOURCE_DIR)

get_property(FOLDERS_TO_DELETE DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES)
set_property(DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES "${FOLDERS_TO_DELETE};${SOURCE_DIR}")

add_library(re2-lib STATIC IMPORTED)

set_target_properties(re2-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/
    IMPORTED_LOCATION ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/obj/libre2.a
)

add_dependencies(re2-lib re2)
