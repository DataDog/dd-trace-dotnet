# APMS-19196 — InvalidProgram / EH clause sort repro

Minimal ASP.NET Core app: one async middleware (`Middlewares/ReproMiddleware.cs`) — `try` / `await` / `catch (UnauthorizedAccessException)` — plus routes `/`, `/throw`, `/throw-unauthorized`.

Docker image: **.NET 9 linux/amd64**, tracer **v3.41.0** from GitHub releases (pre–`SortEHClauses` fix), profiler + **Exception Replay** (`DD_EXCEPTION_REPLAY_ENABLED=true`). Mock agent in `docker-compose.yml`.

## Run

```bash
cd repro/APMS-19196
./test.sh                 # Exception Replay + hammer (optional instrument-all fallback)
./test.sh instrument-all  # debugger instrument-all only
docker compose down
```

`BUILD_NO_CACHE=1` forces image rebuild. `SKIP_INSTRUMENT_ALL_FALLBACK=1` skips the second compose profile.

## Environment most likely to see **in-process** `InvalidProgramException`

- **Native Linux x86_64** (VM, bare metal, CI runner). **WSL2 on x64 Windows** runs **Linux amd64** natively on Intel/AMD — usable; **ARM Windows** / Mac Docker **linux/amd64** often uses emulation and may **not** fault.
- Tracer **before** the EH sort fix (e.g. **3.41.0** tarball as in `Dockerfile`).
- **.NET 9**, ER on, profiler on (see `Dockerfile`).

Deterministic **ordering** check (no CLR): compile and run `eh_sort_repro.cpp` locally. Native tests: `tracer/test/Datadog.Tracer.Native.Tests/il_rewriter_eh_sort_test.cpp` (`DebuggerAsyncMiddlewareScenario`).

Jira: APMS-19196.
