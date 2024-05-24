#pragma once

constexpr auto PROFILER_VERSION = "2.55.0";

#ifdef _WIN32
constexpr auto TRACER_ASSEMBLY = L"Datadog.Trace, Version=2.55.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
#else
constexpr auto TRACER_ASSEMBLY = u"Datadog.Trace, Version=2.55.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
#endif