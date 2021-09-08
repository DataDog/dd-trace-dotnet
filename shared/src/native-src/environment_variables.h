#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_

#include "string.h"  // NOLINT

namespace shared {
	namespace environment {

		// Sets the path for the profiler's log file.
		// Environment variable DD_TRACE_LOG_DIRECTORY takes precedence over this setting, if set.
		const WSTRING log_path = WStr("DD_TRACE_LOG_PATH");

		// Sets the directory for the profiler's log file.
		// If set, this setting takes precedence over environment variable DD_TRACE_LOG_PATH.
		// If not set, default is
		// "%ProgramData%"\Datadog .NET Tracer\logs\" on Windows or
		// "/var/log/datadog/dotnet/" on Linux.
		const WSTRING log_directory = WStr("DD_TRACE_LOG_DIRECTORY");


		//
		// LOADER FEATURE FLAGS
		//

		// Disables rewriting the module initializer
		const WSTRING disable_rewrite_module_initializer = WStr("DD_LOADER_DISABLE_REWRITE_MODULE_INITIALIZER");

		// Disables rewriting the module entrypoint
		const WSTRING disable_rewrite_module_entrypoint = WStr("DD_LOADER_DISABLE_REWRITE_MODULE_ENTRYPOINT");

		// Disables rewriting the mscorlib
		const WSTRING disable_rewrite_mscorlib = WStr("DD_LOADER_DISABLE_REWRITE_MSCORLIB");

		// Disables NGEN images support
		const WSTRING disable_ngen = WStr("DD_LOADER_DISABLE_NGEN");

	}  // namespace environment
}  // namespace shared

#endif
