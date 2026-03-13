# Task: Grant dd-octo-sts access to benchmarking-platform-tools for dd-trace-dotnet

## Goal

Open a PR on the `dd-trace-dotnet` repository to grant dd-octo-sts access to `DataDog/benchmarking-platform-tools` GitHub repository. This will allow the dd-trace-dotnet GitLab CI to clone benchmarking-platform-tools (which contains bp-runner) using a GitHub token issued by dd-octo-sts.

## Background

- dd-octo-sts is Datadog's service for issuing short-lived GitHub tokens from GitLab CI
- The dd-trace-dotnet microbenchmarks CI needs to install bp-runner from the private `DataDog/benchmarking-platform-tools` repo
- Currently, we work around this by building the AMI from the benchmarking-platform repo (which already has access)
- With this PR, dd-trace-dotnet CI jobs can directly access benchmarking-platform-tools

## What to do

1. Find the dd-octo-sts configuration file in the dd-trace-dotnet repo
   - It's typically at `.github/dd-octo-sts.yml` or similar
   - Look for existing dd-octo-sts config patterns in the repo

2. Add a new policy or update existing config to grant access to `DataDog/benchmarking-platform-tools`
   - The policy should allow `read-contents` access (for cloning the repo)
   - Follow the existing patterns in the file

3. If no dd-octo-sts config exists, look at other Datadog repos for examples:
   - Check `DataDog/dd-source` or `DataDog/datadog-agent` for reference
   - The config typically specifies which GitLab projects can request tokens for which GitHub repos

4. Create a PR with:
   - Title: `ci: grant dd-octo-sts access to benchmarking-platform-tools`
   - Description explaining that this enables the microbenchmarks CI to install bp-runner directly

## Expected outcome

After this PR is merged, dd-trace-dotnet GitLab CI jobs will be able to use dd-octo-sts to get a GitHub token with read access to `DataDog/benchmarking-platform-tools`, enabling direct installation of bp-runner without going through the benchmarking-platform repo.
