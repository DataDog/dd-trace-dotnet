// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "environment.h"

#include <string>

#include "../../../shared/src/native-src/logger.h"
#include "../../../shared/src/native-src/logmanager.h"
#include "../../../shared/src/native-src/string.h"

namespace ds = datadog::shared;

class Log final
{
public:
    struct NativeLoaderLoggerPolicy
    {
        inline static const std::string file_name = "dotnet-native-loader";
#ifdef _WIN32
        inline static const shared::WSTRING folder_path = WStr(R"(Datadog .NET Tracer\logs)");
#endif
        inline static const std::string pattern = "[%Y-%m-%d %H:%M:%S.%e | %l | PId: %P | TId: %t] %v";

        static bool enable_buffering() {
#ifdef _WIN32
            // For Windows SSI, we are injected into all .NET Core processes, so we can't enable flushing of logs
            // until we know that the process is going to be instrumented, otherwise we will create way
            // too many log files. Note that we don't currently distinguish between .NET FX and .NET Core, because
            // we need to make the decision _before_ we have that information.
            const auto isSingleStepVariable = shared::GetEnvironmentValue(environment::single_step_instrumentation_enabled);
            if (isSingleStepVariable.empty())
            {
                // not in Windows SSI, so we don't need buffering
                return false;
            }

            const auto bufferEnvVar = shared::GetEnvironmentValue(environment::log_buffering_enabled);

            // enable buffering _unless_ the variable is present and set to false
            // if the variable is not set, or it's set to false
            bool enable_buffering;
            return bufferEnvVar.empty()
                || !shared::TryParseBooleanEnvironmentValue(bufferEnvVar, enable_buffering)
                || enable_buffering == true;
#else
            // On linux/mac, we are only injected where we know we are needed, so we don't buffer
            return false;
#endif
        }

        struct logging_environment
        {
            inline static const shared::WSTRING log_path = environment::log_path;
            inline static const shared::WSTRING log_directory = environment::log_directory;
        };
    };

    inline static ds::Logger* const Instance = ds::LogManager::Get<Log::NativeLoaderLoggerPolicy>();

    static void EnableAutoFlush()
    {
        Instance->FlushAndDisableBuffering();
    }

    static bool IsDebugEnabled()
    {
        return Instance->IsDebugEnabled();
    }

    static void EnableDebug(bool enable)
    {
        Instance->EnableDebug(enable);
    }

    template <typename... Args>
    static inline void Debug(const Args&... args)
    {
        Instance->Debug<Args...>(args...);
    }

    template <typename... Args>
    static void Info(const Args&... args)
    {
        Instance->Info<Args...>(args...);
    }

    template <typename... Args>
    static void Warn(const Args&... args)
    {
        Instance->Warn<Args...>(args...);
    }

    template <typename... Args>
    static void Error(const Args&... args)
    {
        Instance->Error<Args...>(args...);
    }
};