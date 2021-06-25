#pragma once

#include "string.h"
#include <iostream>
#include <mutex>

static std::mutex log_mutex_;

template <typename Arg>
std::string LogToString(Arg const& arg)
{
    return ToString(arg);
}

template <typename... Args>
std::string LogToString(Args const&... args)
{
    std::ostringstream oss;
    int a[] = {0, ((void) (oss << LogToString(args)), 0)...};
    return oss.str();
}

template <typename... Args>
void Debug(const Args... args)
{
    std::lock_guard<std::mutex> guard(log_mutex_);
    std::cout << "[DBG] : " << LogToString(args...) << std::endl;
}

template <typename... Args>
void Info(const Args... args)
{
    std::lock_guard<std::mutex> guard(log_mutex_);
    std::cout << "[WRN] : " << LogToString(args...) << std::endl;
}

template <typename... Args>
void Warn(const Args... args)
{
    std::lock_guard<std::mutex> guard(log_mutex_);
    std::cout << "[INF] : " << LogToString(args...) << std::endl;
}