#include "logging.h"

std::shared_ptr<spdlog::logger> logger = spdlog::rotating_logger_mt(
    "profiler", "C:\\Temp\\Profiler.log", 1024 * 1024 * 5, 3);
