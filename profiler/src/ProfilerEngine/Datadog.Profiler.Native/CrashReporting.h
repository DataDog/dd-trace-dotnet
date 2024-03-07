#pragma once

class CrashReporting
{
public:
    static void ReportCrash(char** frames, int count, char* threadId);
};
