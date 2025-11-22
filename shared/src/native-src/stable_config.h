#ifndef DD_CLR_PROFILER_STABLE_CONFIG_H_
#define DD_CLR_PROFILER_STABLE_CONFIG_H_

namespace shared
{
    namespace StableConfig
    {
        enum ProfilingEnabled
        {
            ProfilingDisabled = 0,
            ProfilingEnabledTrue = 1,
            ProfilingAuto = 2
        };

        struct SharedConfig
        {
            ProfilingEnabled profilingEnabled;
            bool tracingEnabled;
            bool iastEnabled;
            bool raspEnabled;
            bool dynamicInstrumentationEnabled;

            const char* runtimeId;
            const char* environment;
            const char* serviceName;
            const char* version;
            const char* processTags;
        };
    }
}

#endif
