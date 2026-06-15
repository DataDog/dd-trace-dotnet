# LLM Validation gate (GitLab CI)

The gate **job and runner now live in the platform repo**
([llm-validation-platform](https://gitlab.ddbuild.io/DataDog/llm-validation-platform), `ci/`). This repo
carries only:

- **The benchmark suite:** `.llm-validation/` (`config.yaml`, `suites/`) — the cases + thresholds specific
  to dd-trace-dotnet.
- **The include** (in the top-level `.gitlab-ci.yml`):
  ```yaml
  include:
    - project: 'DataDog/llm-validation-platform'
      ref: nacho/initialImplementation   # -> main once the platform is merged there
      file: '/ci/llm-validation.gitlab-ci.yml'
  ```
  Until the platform code is on `main`, the `"llm validation"` job in `.gitlab-ci.yml` also pins
  `LLMVAL_PLATFORM_REF` to the same ref so the runner/CLI are cloned from that branch.

The job runs automatically on non-`master` pipelines, self-skips when `AGENTS.md` is unchanged, and **blocks
the merge on a FAIL**. Behaviour is tuned via `LLMVAL_*` variables — see the platform repo's
**`ci/README.md`** for the full adoption + configuration guide.

`gateway-check.{yml,sh}` here is a separate one-off manual entitlement probe (AI Gateway access was confirmed
2026-06); kept for re-checks.
