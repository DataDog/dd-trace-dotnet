#include "logging.h"

std::shared_ptr<spdlog::logger> logger =
    spdlog::rotating_logger_mt("profiler", "logs/rotating.txt", 1048576 * 5, 3);
