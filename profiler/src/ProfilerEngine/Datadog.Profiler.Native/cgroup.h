// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

class CGroup
{
    // the cgroup version number or 0 to indicate cgroups are not found or not enabled
    static int s_cgroup_version;

    static char *s_cpu_cgroup_path;
public:
    static void Initialize();

    static void Cleanup();

    static bool GetCpuLimit(double* val);

private:
    static int FindCGroupVersion();
    static bool IsCGroup1CpuSubsystem(const char* strTok);

    static char* FindCGroupPath(bool (*is_subsystem)(const char*));

    static void FindHierarchyMount(bool (*is_subsystem)(const char*), char** pmountpath, char** pmountroot);

    static char* FindCGroupPathForSubsystem(bool (*is_subsystem)(const char*));

    static bool GetCGroup1CpuLimit(double* val);

    static bool GetCGroup2CpuLimit(double* val);

    static void ComputeCpuLimit(long long period, long long quota, double* val);

    static long long ReadCpuCGroupValue(const char* subsystemFilename);

    static bool ReadLongLongValueFromFile(const char* filename, long long* val);
};
