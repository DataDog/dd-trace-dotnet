#ifndef DD_CLR_PROFILER_MACROS_H_
#define DD_CLR_PROFILER_MACROS_H_

#include <corhlpr.h>
#include <fstream>

const std::string kLogFilePath = "C:\\temp\\Profiler.log";

#define HEX(HR) L"0x" << std::hex << std::uppercase << HR << std::dec
#define LOG_APPEND(EXPR)                        \
  do {                                          \
    std::wofstream log_file;                    \
    log_file.open(kLogFilePath, std::ios::app); \
    log_file << EXPR << L"\n";                  \
    log_file.close();                           \
  } while (0)
#define LOG_IFFAILED(HR, EXPR)                   \
  do {                                           \
    if (FAILED(HR)) {                            \
      LOG_APPEND(EXPR << L", hr = " << HEX(HR)); \
    }                                            \
  } while (0)
#define LOG_IFFAILEDRET(HR, EXPR)                \
  do {                                           \
    if (FAILED(HR)) {                            \
      LOG_APPEND(EXPR << L", hr = " << HEX(HR)); \
      return E_FAIL;                             \
    }                                            \
  } while (0)
#define RETURN_IF_FAILED(EXPR) \
  do {                         \
    hr = (EXPR);               \
    if (FAILED(hr)) {          \
      return (hr);             \
    }                          \
  } while (0)
#define RETURN_OK_IF_FAILED(EXPR) \
  do {                            \
    hr = (EXPR);                  \
    if (FAILED(hr)) {             \
      return S_OK;                \
    }                             \
  } while (0)

#endif  // DD_CLR_PROFILER_MACROS_H_