#pragma once

#include <corhlpr.h>

extern std::wofstream g_wLogFile;
extern WCHAR g_wszLogFilePath[MAX_PATH];

#define HEX(HR) L"0x" << std::hex << std::uppercase << HR << std::dec
#define LOG_APPEND(EXPR) do { g_wLogFile.open(g_wszLogFilePath, std::ios::app); g_wLogFile << EXPR << L"\n"; g_wLogFile.close(); } while(0)
#define LOG_IFFAILED(HR, EXPR) do { if (FAILED(HR)) { LOG_APPEND(EXPR << L", hr = " << HEX(HR)); } } while(0)
#define LOG_IFFAILEDRET(HR, EXPR) do { if (FAILED(HR)) { LOG_APPEND(EXPR << L", hr = " << HEX(HR)); return E_FAIL; } } while(0)
#define RETURN_IF_FAILED(EXPR) do { hr = (EXPR); if(FAILED(hr)) { return (hr); } } while (0)
