# Updating the GitLab CI Windows Docker Image

The Windows GitLab CI build container is defined by `gitlab.windows.dockerfile` and the install scripts in this directory. The image is referenced by the `build:` job in `.gitlab-ci.yml` via a content-addressed tag derived from the hash of every file in this directory.

## Preferred: build via GitLab CI

This is a single-PR flow. You do not need a local Windows machine with Docker Desktop, and you do not need to touch `DataDog/images`.

### 1. Edit the image

Edit `gitlab.windows.dockerfile`, any of the `install_*.ps1` scripts, `entrypoint.bat`, `requirements.txt`, `constraints.txt`, etc. Every file in this directory contributes to the image hash, so any change produces a new tag.

### 2. Open a PR

Push the branch. The `build:` job will run and fail fast at its preflight step with a message like:

```
ERROR: Windows build image not found at registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-docker-build:<hash>.
The Dockerfile or install scripts under tracer/build/_build/docker/gitlab/ have changed.
Manually trigger the 'build-windows-ci-image' job in this pipeline...
```

This is expected. The tag does not exist yet because nobody has built it.

### 3. Manually trigger `build-windows-ci-image`

Find the `build-windows-ci-image` job in the pipeline UI and click run. It will:

1. Compute the hash via `compute-image-hash.ps1`.
2. Short-circuit if the tag already exists in `registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-docker-build`.
3. Otherwise pull `:latest` to warm the Docker layer cache, build the image, and push both the content-addressed tag and `:latest`.

A cold-cache build (e.g., rebasing the base image) takes up to 2 hours. A warm-cache build with only late-stage install changes should be a small fraction of that.

### 4. Re-run the `build:` job

Once `build-windows-ci-image` is green, re-run the `build:` job (or any other Windows job that consumes the image). It recomputes the same hash, finds the image in the registry, and proceeds normally.

### Notes

- The `:latest` tag is **only** used to seed the Docker build cache on the next rebuild. Nothing in the pipeline consumes `:latest` as an input at runtime; the consumer always pins to the content hash.
- Changes to `compute-image-hash.ps1` itself also invalidate the hash, because the script hashes its own directory.

### Example PR

- [dd-trace-dotnet#7492](https://github.com/DataDog/dd-trace-dotnet/pull/7492) (earlier, local-build flow — for structural reference)

---

## Fallback: manual local build (legacy, retained for rollback)

This path is retained while the GitLab-native flow stabilises. Prefer the flow above. Remove this section once the new flow has been stable on master for ~2 weeks.

### Prerequisites

- Windows PC/Virtual Machine
- Docker Desktop with Windows containers enabled
- Push access to `datadog` DockerHub organization

### Steps

1. **Update the Dockerfile** — edit `gitlab.windows.dockerfile` with required changes (version environment variables, SHA256/SHA512 hashes, download URLs).

2. **Determine the tag.** Format: `dotnet<VERSION>-<STAGE>` (e.g., `dotnet10-rc2`). Major .NET version updates use the .NET version (e.g., `dotnet10-rc1`, `dotnet10-rc2`). Minor tooling updates append `.1`, `.2`, etc. (e.g., `dotnet10-rc2.1`).

3. **Build and push to DockerHub:**

   ```powershell
   $tag="<TAG>"
   echo "building datadog/dd-trace-dotnet-docker-build:$tag"
   cd tracer\build\_build\docker\gitlab
   docker build -f gitlab.windows.dockerfile --tag datadog/dd-trace-dotnet-docker-build:$tag .
   docker push datadog/dd-trace-dotnet-docker-build:$tag
   ```

4. **Test the image in GitLab CI.** Temporarily pin `.gitlab-ci.yml`'s `build:` job to `datadog/dd-trace-dotnet-docker-build:<TAG>` (overriding the computed content hash). Push and verify the CI build passes.

5. **Get the image digest:**

   ```powershell
   docker pull datadog/dd-trace-dotnet-docker-build:$tag
   docker inspect --format='{{index .RepoDigests 0}}' datadog/dd-trace-dotnet-docker-build:$tag
   ```

   Or use [crane](https://github.com/google/go-containerregistry/blob/main/cmd/crane/doc/crane.md):

   ```
   $ crane digest datadog/dd-trace-dotnet-docker-build:dotnet10-rc1
   sha256:180cb096b25d9c53e24b23d0324cd403cc7fe4e99c88ec2c20e851dc37d359ef
   ```

6. **Create mirror PR in `DataDog/images`.** Add entries to `mirror.yaml` and `mirror.lock.yaml`:

   **`mirror.yaml`:**
   ```yaml
   - source: "docker.io/datadog/dd-trace-dotnet-docker-build:<TAG>"
     dest:
       repo: "datadog/dd-trace-dotnet-docker-build"
       tag: "<TAG>"
     replication_target: "build"
     platforms:
       - "windows/amd64"
   ```

   **`mirror.lock.yaml`:**
   ```yaml
   - source: docker.io/datadog/dd-trace-dotnet-docker-build:<TAG>
     digest: sha256:<DIGEST_FROM_STEP_5>
   ```

   Merge. Mirror sync completes within a few minutes.

7. **Update `.gitlab-ci.yml`** to pin the `build:` job to `registry.ddbuild.io/images/mirror/datadog/dd-trace-dotnet-docker-build:<TAG>`, overriding the computed content hash. Merge.

### Example PRs (legacy flow)

- Image update: [dd-trace-dotnet#7492](https://github.com/DataDog/dd-trace-dotnet/pull/7492)
- Mirror: [images#7647](https://github.com/DataDog/images/pull/7647)
