# Formal-methods diagnostics

Start with [`REVIEW-GUIDE.md`](REVIEW-GUIDE.md) for the methodology,
priority-ranked findings, replay commands, limitations, and reviewer questions.

These bounded TLA+ models describe ownership and barrier protocols observed in the
current C# implementation. They are diagnostics: every checked configuration is
expected to produce the documented counterexample. A model unexpectedly passing
is therefore a test failure.

Run them with:

```bash
./tracer/test/formal-methods/run-models.sh /path/to/tla2tools.jar
```

The real-implementation replays use the `FormalMethods` and
`FormalMethodsPhase2` xUnit traits:

```bash
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj \
  -f net10.0 -m:1 -nodeReuse:false \
  --filter 'Category=FormalMethods|Category=FormalMethodsPhase2'
```

The models deliberately do not claim production frequency. They establish the
shortest protocol schedule; the xUnit tests establish that the corresponding C#
behavior is reachable under controlled fault conditions.
