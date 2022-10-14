add_library(spdlog-headers INTERFACE)

target_include_directories(spdlog-headers INTERFACE
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/spdlog/include
)
