// Microbenchmark: the PoC C library vs libdatadog (Rust), same synthetic inputs,
// calling each library's C API directly. Build with -DBACKEND_POC or
// -DBACKEND_LDD (see run-benchmarks.sh / run-benchmarks.ps1). Prints one JSON
// object per cycle (or one for the export phase) on stdout.
#if !defined(_WIN32)
#define _GNU_SOURCE
#endif
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>
#include <tlhelp32.h>
#define strdup _strdup
#else
#include <dirent.h>
#include <malloc.h>
#include <sched.h>
#include <time.h>
#include <unistd.h>
#endif

#if defined(BACKEND_POC)
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"
#define BACKEND "poc"
#elif defined(BACKEND_LDD)
#include "datadog/common.h"
#include "datadog/profiling.h"
#define BACKEND "libdatadog"
#else
#error define BACKEND_POC or BACKEND_LDD
#endif

// ---------------- parameters ----------------
static long P_SAMPLES = 20000, P_STACKS = 1000, P_DEPTH = 32, P_LABELS = 3, P_VALUES = 4, P_TS = 1, P_CYCLES = 6;
static long P_ENDPOINTS = 0, P_UPSCALE = 0, P_SENDS = 0, P_FILE_KB = 4;
static long P_CPU = -1;
static const char* P_URL = "http://127.0.0.1:18127";

// ---------------- measurement (platform layer) ----------------
//
// cpu_ns:     process CPU time (Linux: CLOCK_PROCESS_CPUTIME_ID, ns resolution;
//             Windows: GetProcessTimes, ~15.6 ms granularity - prefer the
//             wall times there, they are measured on a pinned core)
// heap_bytes: bytes allocated on the C heap. Both libraries allocate through
//             it (libdatadog uses Rust's System allocator): glibc malloc on
//             Linux (mallinfo2, main arena), the process heap on Windows
//             (HeapWalk, UCRT malloc and Rust both use GetProcessHeap()).
// peak_extra: extra resident memory at the peak of serialize - Linux only
//             (VmHWM, reset through /proc/self/clear_refs); -1 on Windows.
#if defined(_WIN32)
static int64_t filetime_ns(FILETIME ft) { return (int64_t)((((uint64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime) * 100); }
static int64_t cpu_ns(void)
{
    FILETIME c, e, k, u;
    GetProcessTimes(GetCurrentProcess(), &c, &e, &k, &u);
    return filetime_ns(k) + filetime_ns(u);
}
static int64_t wall_ns(void)
{
    static LARGE_INTEGER freq;
    LARGE_INTEGER now;
    if (freq.QuadPart == 0) QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&now);
    return (int64_t)((double)now.QuadPart * 1e9 / (double)freq.QuadPart);
}
static const char* HEAP_METRIC = "heapwalk";
// Sums the busy blocks of every heap of the process: UCRT malloc and Rust's
// System allocator use GetProcessHeap(), but other CRTs (e.g. msvcrt.dll)
// allocate from a private heap.
static int64_t heap_bytes(void)
{
    HANDLE heaps[256];
    DWORD n = GetProcessHeaps(256, heaps);
    int64_t total = 0;
    long entries = 0;
    for (DWORD i = 0; i < n && i < 256; i++)
    {
        PROCESS_HEAP_ENTRY e;
        e.lpData = NULL;
        if (!HeapLock(heaps[i])) continue;
        while (HeapWalk(heaps[i], &e))
        {
            entries++;
            if (e.wFlags & PROCESS_HEAP_ENTRY_BUSY) total += (int64_t)e.cbData;
        }
        HeapUnlock(heaps[i]);
    }
    if (entries == 0)
    {
        // HeapWalk unavailable: fall back to the process private bytes
        PROCESS_MEMORY_COUNTERS_EX pmc;
        GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&pmc, sizeof(pmc));
        HEAP_METRIC = "private_bytes";
        return (int64_t)pmc.PrivateUsage;
    }
    return total;
}
static long rss_kb(void)
{
    PROCESS_MEMORY_COUNTERS pmc;
    GetProcessMemoryInfo(GetCurrentProcess(), &pmc, sizeof(pmc));
    return (long)(pmc.WorkingSetSize / 1024);
}
static void reset_peak(void) {}
static long peak_kb(void) { return -1; }
static int thread_count(void)
{
    DWORD pid = GetCurrentProcessId();
    int n = 0;
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    THREADENTRY32 te;
    te.dwSize = sizeof(te);
    if (snap != INVALID_HANDLE_VALUE && Thread32First(snap, &te))
        do { if (te.th32OwnerProcessID == pid) n++; } while (Thread32Next(snap, &te));
    if (snap != INVALID_HANDLE_VALUE) CloseHandle(snap);
    return n;
}
static void pin_to_cpu(long cpu)
{
    if (cpu >= 0) SetProcessAffinityMask(GetCurrentProcess(), (DWORD_PTR)1 << cpu);
}
#else
static int64_t cpu_ns(void) { struct timespec t; clock_gettime(CLOCK_PROCESS_CPUTIME_ID, &t); return t.tv_sec * 1000000000LL + t.tv_nsec; }
static int64_t wall_ns(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec * 1000000000LL + t.tv_nsec; }
static const char* HEAP_METRIC = "mallinfo2";
static int64_t heap_bytes(void) { struct mallinfo2 m = mallinfo2(); return (int64_t)(m.uordblks + m.hblkhd); }
static long status_kb(const char* key)
{
    FILE* f = fopen("/proc/self/status", "r"); char line[256]; long v = -1; size_t n = strlen(key);
    while (f && fgets(line, sizeof line, f)) if (strncmp(line, key, n) == 0) { v = atol(line + n); break; }
    if (f) fclose(f);
    return v;
}
static long rss_kb(void) { return status_kb("VmRSS:"); }
static void reset_peak(void) { FILE* f = fopen("/proc/self/clear_refs", "w"); if (f) { fputs("5", f); fclose(f); } }
static long peak_kb(void) { return status_kb("VmHWM:"); }
static int thread_count(void) { int n = 0; DIR* d = opendir("/proc/self/task"); struct dirent* e; while (d && (e = readdir(d))) if (e->d_name[0] != '.') n++; if (d) closedir(d); return n; }
static void pin_to_cpu(long cpu)
{
    if (cpu < 0) return;
    cpu_set_t set;
    CPU_ZERO(&set);
    CPU_SET((int)cpu, &set);
    sched_setaffinity(0, sizeof(set), &set);
}
#endif

static uint64_t rng = 0x9E3779B97F4A7C15ULL;
static uint64_t next(void) { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return rng; }

// ---------------- synthetic inputs (identical for both backends) ----------------
typedef struct { char* name; char* filename; char* module; int line; } fn_t;
static fn_t* fns; static long nfns;
static long* stack_frames; // P_STACKS * P_DEPTH indices into fns
static char** label_keys; static char*** label_vals; // P_LABELS keys, 32 values each
typedef struct { int stack; int label_val[16]; int64_t num; int64_t ts; int64_t values[16]; } sample_t;
static sample_t* samples;

static char* fmt(const char* f, long a) { char b[256]; snprintf(b, sizeof b, f, a, a); return strdup(b); }

static void build_inputs(void)
{
    nfns = P_STACKS * P_DEPTH / 4 + 1;
    fns = calloc(nfns, sizeof(fn_t));
    for (long i = 0; i < nfns; i++)
    {
        fns[i].name = fmt("Company.Product.Component.SubSystem%ld.Worker.ProcessItem_%ld(System.Int32, System.String)", i);
        fns[i].filename = fmt("/src/Component/SubSystem%ld/Worker%ld.cs", i);
        fns[i].module = fmt("Company.Product.Module%ld.dll", i % 64);
        fns[i].line = (int)(i % 500) + 1;
    }
    stack_frames = malloc(sizeof(long) * P_STACKS * P_DEPTH);
    for (long s = 0; s < P_STACKS; s++)
        for (long d = 0; d < P_DEPTH; d++)
            stack_frames[s * P_DEPTH + d] = (long)(next() % nfns);
    label_keys = calloc(P_LABELS, sizeof(char*)); label_vals = calloc(P_LABELS, sizeof(char**));
    for (long k = 0; k < P_LABELS; k++)
    {
        label_keys[k] = fmt("label key %ld", k);
        label_vals[k] = calloc(32, sizeof(char*));
        for (long v = 0; v < 32; v++) label_vals[k][v] = fmt("label value number %ld", v + k * 100);
    }
    samples = calloc(P_SAMPLES, sizeof(sample_t));
    for (long i = 0; i < P_SAMPLES; i++)
    {
        sample_t* s = &samples[i];
        s->stack = (int)(next() % P_STACKS);
        for (long k = 0; k < P_LABELS; k++) s->label_val[k] = (int)(next() % 32);
        s->num = P_ENDPOINTS ? (int64_t)(next() % P_ENDPOINTS) + 1 : (int64_t)(next() % 8) + 1; // "local root span id"
        s->ts = P_TS ? 1700000000000000000LL + i * 1000 : 0;
        for (long v = 0; v < P_VALUES; v++) s->values[v] = (int64_t)(next() % 1000) + 1;
    }
}

// ---------------- backend wrappers ----------------
#if defined(BACKEND_POC)
static ddog_charslice cs(const char* s) { ddog_charslice r = {s, strlen(s)}; return r; }
static const char* TYPE_NAMES[16] = {"wall-time", "cpu-time", "cpu-samples", "alloc-samples", "alloc-size", "lock-count", "lock-time", "exception-samples", "timeline", "inuse-objects", "inuse-space", "wall-samples", "request-time", "heap-live-samples", "heap-live-size", "sample"};
static ddog_prof_location* locs;
static ddog_prof_profile* profile;
static void bk_init(void)
{
    ddog_prof_value_type vt[16];
    for (long i = 0; i < P_VALUES; i++) { vt[i].type = cs(TYPE_NAMES[i]); vt[i].unit = cs("count"); }
    ddog_prof_period period = {vt[0], 1};
    if (ddog_prof_profile_new(vt, P_VALUES, &period, &profile) != DDOG_OK) { fprintf(stderr, "new: %s\n", ddog_last_error_message()); exit(1); }
    locs = calloc(P_STACKS * P_DEPTH, sizeof(ddog_prof_location));
    for (long i = 0; i < P_STACKS * P_DEPTH; i++)
    {
        fn_t* f = &fns[stack_frames[i]];
        locs[i].function.name = cs(f->name); locs[i].function.filename = cs(f->filename);
        locs[i].mapping_filename = cs(f->module); locs[i].line = f->line;
    }
}
static void bk_add(const sample_t* s)
{
    ddog_prof_label labels[17]; memset(labels, 0, sizeof labels);
    for (long k = 0; k < P_LABELS; k++) { labels[k].key = cs(label_keys[k]); labels[k].str = cs(label_vals[k][s->label_val[k]]); }
    labels[P_LABELS].key = cs("local root span id"); labels[P_LABELS].num = s->num;
    ddog_prof_sample smp = {&locs[s->stack * P_DEPTH], (size_t)P_DEPTH, s->values, (size_t)P_VALUES, labels, (size_t)P_LABELS + 1};
    if (ddog_prof_profile_add(profile, &smp, s->ts) != DDOG_OK) { fprintf(stderr, "add: %s\n", ddog_last_error_message()); exit(1); }
}
static void bk_endpoints(void)
{
    char b[64];
    for (long i = 1; i <= P_ENDPOINTS; i++)
    {
        snprintf(b, sizeof b, "GET /api/resource/%ld", i % 50);
        ddog_prof_profile_set_endpoint(profile, (uint64_t)i, cs(b));
        ddog_prof_profile_add_endpoint_count(profile, cs(b), 1);
    }
}
static void bk_upscale(void)
{
    size_t off[1] = {0};
    for (long v = 0; v < 32 && P_LABELS > 0; v++)
        ddog_prof_profile_add_upscaling_rule_proportional(profile, off, 1, cs(label_keys[0]), cs(label_vals[0][v]), 10, 25);
}
static ddog_prof_encoded_profile* bk_serialize(size_t* size)
{
    ddog_prof_encoded_profile* e = NULL;
    if (ddog_prof_profile_serialize(profile, NULL, NULL, &e) != DDOG_OK) { fprintf(stderr, "ser: %s\n", ddog_last_error_message()); exit(1); }
    const uint8_t* p; ddog_prof_encoded_profile_bytes(e, &p, size);
    return e;
}
static void bk_encoded_drop(ddog_prof_encoded_profile* e) { ddog_prof_encoded_profile_drop(e); }
static ddog_prof_exporter* exporter;
static void bk_exporter_new(void)
{
    ddog_prof_endpoint ep; ddog_prof_endpoint_agent(cs(P_URL), 3000, false, &ep);
    ddog_vec_tag tags; ddog_vec_tag_new(&tags);
    ddog_vec_tag_push(&tags, cs("service"), cs("bench")); ddog_vec_tag_push(&tags, cs("env"), cs("bench"));
    if (ddog_prof_exporter_new(cs("bench"), cs("1.0.0"), cs("native"), &tags, &ep, &exporter) != DDOG_OK) { fprintf(stderr, "exp: %s\n", ddog_last_error_message()); exit(1); }
    ddog_vec_tag_drop(&tags);
}
static void bk_send(ddog_prof_encoded_profile* e, const uint8_t* file, size_t file_len)
{
    ddog_prof_exporter_file f[1] = {{cs("metrics.json"), {file, file_len}}};
    ddog_charslice internal = cs("{\"k\":\"v\"}"), info = cs("{\"profiler\":{\"version\":\"1\"}}");
    uint16_t status = 0;
    if (ddog_prof_exporter_send_blocking(exporter, e, f, 1, NULL, NULL, &internal, &info, &status) != DDOG_OK || status >= 300) { fprintf(stderr, "send: %s %u\n", ddog_last_error_message(), status); exit(1); }
    ddog_prof_encoded_profile_drop(e); // the PoC does not take ownership
}
static void bk_exporter_drop(void) { ddog_prof_exporter_drop(exporter); }
static void bk_drop(void) { ddog_prof_profile_drop(profile); }

#else // libdatadog
static ddog_CharSlice cs(const char* s) { ddog_CharSlice r = {s, strlen(s)}; return r; }
static const ddog_prof_SampleType TYPES[16] = {DDOG_PROF_SAMPLE_TYPE_WALL_TIME, DDOG_PROF_SAMPLE_TYPE_CPU_TIME, DDOG_PROF_SAMPLE_TYPE_CPU_SAMPLES, DDOG_PROF_SAMPLE_TYPE_ALLOC_SAMPLES, DDOG_PROF_SAMPLE_TYPE_ALLOC_SIZE, DDOG_PROF_SAMPLE_TYPE_LOCK_COUNT, DDOG_PROF_SAMPLE_TYPE_LOCK_TIME, DDOG_PROF_SAMPLE_TYPE_EXCEPTION_SAMPLES, DDOG_PROF_SAMPLE_TYPE_TIMELINE, DDOG_PROF_SAMPLE_TYPE_INUSE_OBJECTS, DDOG_PROF_SAMPLE_TYPE_INUSE_SPACE, DDOG_PROF_SAMPLE_TYPE_WALL_SAMPLES, DDOG_PROF_SAMPLE_TYPE_REQUEST_TIME, DDOG_PROF_SAMPLE_TYPE_HEAP_LIVE_SAMPLES, DDOG_PROF_SAMPLE_TYPE_HEAP_LIVE_SIZE, DDOG_PROF_SAMPLE_TYPE_SAMPLE};
static ddog_prof_Location* locs;
static ddog_prof_Profile profile;
static void die(const char* what, ddog_Error* err) { ddog_CharSlice m = ddog_Error_message(err); fprintf(stderr, "%s: %.*s\n", what, (int)m.len, m.ptr); exit(1); }
static void bk_init(void)
{
    ddog_prof_Slice_SampleType st = {TYPES, (size_t)P_VALUES};
    ddog_prof_Period period = {TYPES[0], 1};
    ddog_prof_Profile_NewResult r = ddog_prof_Profile_new(st, &period);
    if (r.tag != DDOG_PROF_PROFILE_NEW_RESULT_OK) die("new", &r.err);
    profile = r.ok;
    locs = calloc(P_STACKS * P_DEPTH, sizeof(ddog_prof_Location));
    for (long i = 0; i < P_STACKS * P_DEPTH; i++)
    {
        fn_t* f = &fns[stack_frames[i]];
        locs[i].function.name = cs(f->name); locs[i].function.filename = cs(f->filename);
        locs[i].mapping.filename = cs(f->module); locs[i].line = f->line;
    }
}
static void bk_add(const sample_t* s)
{
    ddog_prof_Label labels[17]; memset(labels, 0, sizeof labels);
    for (long k = 0; k < P_LABELS; k++) { labels[k].key = cs(label_keys[k]); labels[k].str = cs(label_vals[k][s->label_val[k]]); }
    labels[P_LABELS].key = cs("local root span id"); labels[P_LABELS].num = s->num;
    ddog_prof_Sample smp = {{&locs[s->stack * P_DEPTH], (uintptr_t)P_DEPTH}, {s->values, (uintptr_t)P_VALUES}, {labels, (uintptr_t)P_LABELS + 1}};
    ddog_prof_Profile_Result r = ddog_prof_Profile_add(&profile, smp, s->ts);
    if (r.tag != DDOG_PROF_PROFILE_RESULT_OK) die("add", &r.err);
}
static void bk_endpoints(void)
{
    char b[64];
    for (long i = 1; i <= P_ENDPOINTS; i++)
    {
        snprintf(b, sizeof b, "GET /api/resource/%ld", i % 50);
        ddog_prof_Profile_Result r = ddog_prof_Profile_set_endpoint(&profile, (uint64_t)i, cs(b));
        if (r.tag != DDOG_PROF_PROFILE_RESULT_OK) die("set_endpoint", &r.err);
        r = ddog_prof_Profile_add_endpoint_count(&profile, cs(b), 1);
        if (r.tag != DDOG_PROF_PROFILE_RESULT_OK) die("endpoint_count", &r.err);
    }
}
static void bk_upscale(void)
{
    uintptr_t off[1] = {0};
    for (long v = 0; v < 32 && P_LABELS > 0; v++)
    {
        ddog_prof_Profile_Result r = ddog_prof_Profile_add_upscaling_rule_proportional(&profile, (ddog_prof_Slice_Usize){off, 1}, cs(label_keys[0]), cs(label_vals[0][v]), 10, 25);
        if (r.tag != DDOG_PROF_PROFILE_RESULT_OK) die("upscale", &r.err);
    }
}
static ddog_prof_EncodedProfile enc_storage[64]; static int enc_next;
static ddog_prof_EncodedProfile* bk_serialize(size_t* size)
{
    ddog_prof_Profile_SerializeResult r = ddog_prof_Profile_serialize(&profile, NULL, NULL);
    if (r.tag != DDOG_PROF_PROFILE_SERIALIZE_RESULT_OK) die("serialize", &r.err);
    ddog_prof_EncodedProfile* e = &enc_storage[enc_next++ % 64]; *e = r.ok;
    ddog_prof_Result_ByteSlice b = ddog_prof_EncodedProfile_bytes(e);
    *size = b.ok.len;
    return e;
}
static void bk_encoded_drop(ddog_prof_EncodedProfile* e) { ddog_prof_EncodedProfile_drop(e); }
static ddog_prof_ProfileExporter exporter;
static void bk_exporter_new(void)
{
    ddog_Vec_Tag tags = ddog_Vec_Tag_new();
    ddog_Vec_Tag_PushResult p1 = ddog_Vec_Tag_push(&tags, cs("service"), cs("bench"));
    ddog_Vec_Tag_PushResult p2 = ddog_Vec_Tag_push(&tags, cs("env"), cs("bench"));
    if (p1.tag != DDOG_VEC_TAG_PUSH_RESULT_OK || p2.tag != DDOG_VEC_TAG_PUSH_RESULT_OK) { fprintf(stderr, "tag push failed\n"); exit(1); }
    ddog_prof_ProfileExporter_Result r = ddog_prof_Exporter_new(cs("bench"), cs("1.0.0"), cs("native"), &tags, ddog_prof_Endpoint_agent(cs(P_URL), 3000, false));
    if (r.tag != DDOG_PROF_PROFILE_EXPORTER_RESULT_OK_HANDLE_PROFILE_EXPORTER) die("exporter", &r.err);
    exporter = r.ok;
    ddog_Vec_Tag_drop(tags);
}
static void bk_send(ddog_prof_EncodedProfile* e, const uint8_t* file, size_t file_len)
{
    ddog_prof_Exporter_File f[1] = {{cs("metrics.json"), {file, file_len}}};
    ddog_CharSlice internal = cs("{\"k\":\"v\"}"), info = cs("{\"profiler\":{\"version\":\"1\"}}");
    ddog_prof_Result_HttpStatus r = ddog_prof_Exporter_send_blocking(&exporter, e, (ddog_prof_Exporter_Slice_File){f, 1}, NULL, NULL, &internal, &info, NULL);
    if (r.tag != DDOG_PROF_RESULT_HTTP_STATUS_OK_HTTP_STATUS || r.ok.code >= 300) { if (r.tag != DDOG_PROF_RESULT_HTTP_STATUS_OK_HTTP_STATUS) die("send", &r.err); fprintf(stderr, "status %u\n", r.ok.code); exit(1); }
    // send_blocking takes ownership of the encoded profile
}
static void bk_exporter_drop(void) { ddog_prof_Exporter_drop(&exporter); }
static void bk_drop(void) { ddog_prof_Profile_drop(&profile); }
#endif

// ---------------- main ----------------
static long arg(int argc, char** argv, const char* name, long def)
{
    for (int i = 1; i + 1 < argc; i++) if (strcmp(argv[i], name) == 0) return atol(argv[i + 1]);
    return def;
}

int main(int argc, char** argv)
{
    P_SAMPLES = arg(argc, argv, "--samples", P_SAMPLES); P_STACKS = arg(argc, argv, "--stacks", P_STACKS);
    P_DEPTH = arg(argc, argv, "--depth", P_DEPTH); P_LABELS = arg(argc, argv, "--labels", P_LABELS);
    P_VALUES = arg(argc, argv, "--values", P_VALUES); P_TS = arg(argc, argv, "--ts", P_TS);
    P_CYCLES = arg(argc, argv, "--cycles", P_CYCLES); P_ENDPOINTS = arg(argc, argv, "--endpoints", P_ENDPOINTS);
    P_UPSCALE = arg(argc, argv, "--upscale", P_UPSCALE); P_SENDS = arg(argc, argv, "--sends", P_SENDS);
    P_FILE_KB = arg(argc, argv, "--file-kb", P_FILE_KB); P_CPU = arg(argc, argv, "--cpu", P_CPU);
    for (int i = 1; i + 1 < argc; i++) if (strcmp(argv[i], "--url") == 0) P_URL = argv[i + 1];
    pin_to_cpu(P_CPU);

    build_inputs();
    long rss_base = rss_kb();
    int64_t heap0 = heap_bytes();
    bk_init();

    if (P_SENDS > 0)
    {
        // exporter: creation cost, then N sends of a small profile + one attachment
        uint8_t* file = malloc(P_FILE_KB * 1024);
        for (long i = 0; i < P_FILE_KB * 1024; i++) file[i] = (uint8_t)"[[\"metric_name\",12345],"[i % 24];
        for (long i = 0; i < P_SAMPLES; i++) bk_add(&samples[i]);
        size_t size;
        long rss0 = rss_kb(); int th0 = thread_count();
        int64_t c0 = cpu_ns(), w0 = wall_ns();
        bk_exporter_new();
        int64_t c1 = cpu_ns(), w1 = wall_ns();
        long rss_exp = rss_kb();
        int64_t send_cpu = 0, send_wall = 0;
        for (long s = 0; s < P_SENDS; s++)
        {
            if (s > 0) for (long i = 0; i < 1000 && i < P_SAMPLES; i++) bk_add(&samples[i]);
            void* e = bk_serialize(&size);
            int64_t a = cpu_ns(), b = wall_ns();
            bk_send(e, file, P_FILE_KB * 1024);
            send_cpu += cpu_ns() - a; send_wall += wall_ns() - b;
        }
        long rss_after = rss_kb(); int th1 = thread_count();
        printf("{\"backend\":\"%s\",\"phase\":\"export\",\"exporter_new_cpu_us\":%.1f,\"exporter_new_wall_us\":%.1f,"
               "\"rss_exporter_new_kb\":%ld,\"threads_before\":%d,\"threads_after\":%d,\"send_cpu_us\":%.1f,\"send_wall_us\":%.1f,"
               "\"rss_after_sends_kb\":%ld,\"encoded_bytes\":%zu,\"sends\":%ld}\n",
               BACKEND, (c1 - c0) / 1e3, (w1 - w0) / 1e3, rss_exp - rss0, th0, th1, send_cpu / 1e3 / P_SENDS,
               send_wall / 1e3 / P_SENDS, rss_after - rss0, size, P_SENDS);
        bk_exporter_drop();
        bk_drop();
        return 0;
    }

    for (long c = 0; c < P_CYCLES; c++)
    {
        int64_t h0 = heap_bytes();
        int64_t c0 = cpu_ns(), w0 = wall_ns();
        for (long i = 0; i < P_SAMPLES; i++) bk_add(&samples[i]);
        int64_t c1 = cpu_ns(), w1 = wall_ns();
        int64_t c_ep = 0;
        if (P_ENDPOINTS) { int64_t a = cpu_ns(); bk_endpoints(); c_ep = cpu_ns() - a; }
        if (P_UPSCALE) bk_upscale();
        int64_t h1 = heap_bytes();
        long rss1 = rss_kb();
        reset_peak();
        size_t size;
        int64_t c2 = cpu_ns(), w2 = wall_ns();
        void* e = bk_serialize(&size);
        int64_t c3 = cpu_ns(), w3 = wall_ns();
        long hwm = peak_kb();
        int64_t h2 = heap_bytes();
        bk_encoded_drop(e);
        int64_t h3 = heap_bytes();
        printf("{\"backend\":\"%s\",\"cycle\":%ld,\"add_ns_per_sample\":%.1f,\"add_wall_ns_per_sample\":%.1f,"
               "\"endpoints_cpu_us\":%.1f,\"heap_after_add_kb\":%lld,\"serialize_cpu_ms\":%.3f,\"serialize_wall_ms\":%.3f,"
               "\"serialize_peak_rss_extra_kb\":%ld,\"heap_with_encoded_kb\":%lld,\"heap_after_drop_kb\":%lld,"
               "\"encoded_bytes\":%zu,\"lib_baseline_rss_kb\":%ld,\"serialize_wall_us\":%.1f,\"heap_metric\":\"%s\"}\n",
               BACKEND, c, (double)(c1 - c0) / P_SAMPLES, (double)(w1 - w0) / P_SAMPLES, c_ep / 1e3,
               (long long)((h1 - h0) / 1024), (c3 - c2) / 1e6, (w3 - w2) / 1e6, hwm < 0 ? -1 : hwm - rss1,
               (long long)((h2 - h0) / 1024), (long long)((h3 - h0) / 1024), size, rss_base, (w3 - w2) / 1e3, HEAP_METRIC);
        fflush(stdout);
    }
    (void)heap0;
    bk_drop();
    return 0;
}
