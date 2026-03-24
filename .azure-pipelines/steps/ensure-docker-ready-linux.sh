#!/usr/bin/env bash

set -u

MAX_RETRIES="${DOCKER_CGROUP_RETRY_MAX_RETRIES:-3}"
INITIAL_BACKOFF_SECONDS="${DOCKER_CGROUP_RETRY_INITIAL_BACKOFF_SECONDS:-5}"
DOCKER_READY_TIMEOUT_SECONDS="${DOCKER_READY_TIMEOUT_SECONDS:-300}"
DOCKER_READY_CHECK_INTERVAL_SECONDS="${DOCKER_READY_CHECK_INTERVAL_SECONDS:-10}"

log()
{
    echo "[ensure-docker-ready-linux] $*"
}

log_diagnostics()
{
    log "Collecting diagnostics..."
    local cgroup_version="unknown"
    if [ -f "/sys/fs/cgroup/cgroup.controllers" ]; then
        cgroup_version="v2"
    elif [ -d "/sys/fs/cgroup" ]; then
        cgroup_version="v1"
    fi

    log "cgroup version: ${cgroup_version}"
    log "kernel: $(uname -a)"

    if command -v systemctl >/dev/null 2>&1; then
        log "systemd state:"
        systemctl is-system-running || true
        log "docker service status:"
        systemctl status docker --no-pager || true
        log "dbus service status:"
        systemctl status dbus --no-pager || true
    fi

    if command -v journalctl >/dev/null 2>&1; then
        log "docker journal logs (last 50 lines):"
        journalctl -u docker --no-pager -n 50 || true
    fi

    log "docker version:"
    docker version || true
    log "docker info:"
    docker info || true
}

wait_for_docker()
{
    local elapsed=0
    log "Waiting up to ${DOCKER_READY_TIMEOUT_SECONDS}s for Docker daemon..."

    while [ "${elapsed}" -lt "${DOCKER_READY_TIMEOUT_SECONDS}" ]; do
        if docker info >/dev/null 2>&1; then
            log "Docker daemon is ready after ${elapsed}s"
            return 0
        fi

        log "Docker daemon not ready yet (${elapsed}s elapsed), retrying in ${DOCKER_READY_CHECK_INTERVAL_SECONDS}s..."
        sleep "${DOCKER_READY_CHECK_INTERVAL_SECONDS}"
        elapsed=$((elapsed + DOCKER_READY_CHECK_INTERVAL_SECONDS))
    done

    log "Docker daemon did not become ready within ${DOCKER_READY_TIMEOUT_SECONDS}s"
    log_diagnostics
    return 1
}

run_with_oci_retry()
{
    local retry_count=0
    local total_attempts=$((MAX_RETRIES + 1))
    local command=("$@")

    while true; do
        local attempt=$((retry_count + 1))
        log "Running Docker command (attempt ${attempt}/${total_attempts}): ${command[*]}"
        "${command[@]}"
        local exit_code=$?
        if [ "${exit_code}" -eq 0 ]; then
            log "Docker command succeeded"
            return 0
        fi

        if [ "${exit_code}" -ne 125 ]; then
            log "Docker command failed with non-retryable exit code ${exit_code}"
            log_diagnostics
            return "${exit_code}"
        fi

        if [ "${retry_count}" -ge "${MAX_RETRIES}" ]; then
            log "Docker command failed with exit code 125 and max retries (${MAX_RETRIES}) were exhausted"
            log_diagnostics
            return "${exit_code}"
        fi

        local backoff_seconds=$((INITIAL_BACKOFF_SECONDS * (2 ** retry_count)))
        retry_count=$((retry_count + 1))
        log "Docker command failed with exit code 125 (likely transient OCI/cgroup issue). Retrying in ${backoff_seconds}s (${retry_count}/${MAX_RETRIES})..."
        sleep "${backoff_seconds}"
    done
}

main()
{
    if [ "${1:-}" = "--health-check" ]; then
        shift
        wait_for_docker
        return $?
    fi

    wait_for_docker || return $?

    if [ "${1:-}" = "--" ]; then
        shift
    fi

    if [ "$#" -eq 0 ]; then
        return 0
    fi

    run_with_oci_retry "$@"
}

main "$@"