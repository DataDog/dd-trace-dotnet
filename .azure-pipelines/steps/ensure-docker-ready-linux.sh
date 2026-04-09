#!/bin/sh
# Linux Docker readiness check — mirrors the Windows PowerShell logic in ensure-docker-ready.yml.
# Waits for the Docker daemon, attempts service restarts if needed, and fails fast
# to avoid wasting time on a broken agent.

set -u

DOCKER_READY_TIMEOUT_SECONDS="${DOCKER_READY_TIMEOUT_SECONDS:-300}"
DOCKER_READY_CHECK_INTERVAL_SECONDS="${DOCKER_READY_CHECK_INTERVAL_SECONDS:-10}"
DOCKER_MAX_RESTARTS="${DOCKER_MAX_RESTARTS:-3}"

log()
{
    echo "[ensure-docker-ready-linux] $*"
}

log_diagnostics()
{
    log "--- Diagnostics ---"
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

try_restart_docker()
{
    log "Attempting Docker service restart..."
    local output
    output=$(systemctl restart docker 2>&1)
    if [ $? -eq 0 ]; then
        log "systemctl restart docker completed"
        return 0
    else
        log "systemctl restart docker failed: ${output}"
        return 1
    fi
}

wait_for_docker()
{
    local elapsed=0
    local restart_count=0

    log "Waiting up to ${DOCKER_READY_TIMEOUT_SECONDS}s for Docker daemon (will attempt up to ${DOCKER_MAX_RESTARTS} service restarts)..."

    # Quick check — if Docker is already healthy, nothing to do
    local info_output
    if info_output=$(docker info 2>&1); then
        log "Docker daemon is ready"
        echo "${info_output}"
        return 0
    fi

    # If we can't restart Docker, there's no point looping
    if ! command -v systemctl >/dev/null 2>&1; then
        log "Docker is not responding and systemctl is not available — cannot recover"
        log_diagnostics
        return 1
    fi

    # Log initial service state
    local initial_status
    initial_status=$(systemctl is-active docker 2>&1 || true)
    log "Docker service initial state: ${initial_status}"

    local consecutive_failures=0
    local DOCKER_READY_FORCE_RESTART_AFTER=3

    while [ "${elapsed}" -lt "${DOCKER_READY_TIMEOUT_SECONDS}" ]; do
        if info_output=$(docker info 2>&1); then
            log "Docker daemon is ready (waited ${elapsed}s, ${restart_count} restart(s) performed)"
            echo "${info_output}"
            return 0
        fi

        consecutive_failures=$((consecutive_failures + 1))

        # Try restarting if the service is down, or if it reports active but is unresponsive
        local svc_status
        svc_status=$(systemctl is-active docker 2>&1 || true)
        local should_restart=false
        if [ "${svc_status}" != "active" ]; then
            should_restart=true
        elif [ "${consecutive_failures}" -ge "${DOCKER_READY_FORCE_RESTART_AFTER}" ]; then
            log "Docker service reports active but has been unresponsive for ${consecutive_failures} checks"
            should_restart=true
        fi

        if [ "${should_restart}" = true ] && [ "${restart_count}" -lt "${DOCKER_MAX_RESTARTS}" ]; then
            restart_count=$((restart_count + 1))
            log "Docker service is ${svc_status}. Attempting restart ${restart_count}/${DOCKER_MAX_RESTARTS}..."
            try_restart_docker
            sleep 2
            consecutive_failures=0
        elif [ "${should_restart}" = true ] && [ "${restart_count}" -ge "${DOCKER_MAX_RESTARTS}" ]; then
            log "Docker service is ${svc_status} but max restarts (${DOCKER_MAX_RESTARTS}) exhausted — giving up"
            log_diagnostics
            return 1
        fi

        log "Docker not ready yet (${elapsed}s elapsed), retrying in ${DOCKER_READY_CHECK_INTERVAL_SECONDS}s..."
        sleep "${DOCKER_READY_CHECK_INTERVAL_SECONDS}"
        elapsed=$((elapsed + DOCKER_READY_CHECK_INTERVAL_SECONDS))
    done

    log "Docker daemon did not become ready within ${DOCKER_READY_TIMEOUT_SECONDS}s after ${restart_count} restart(s)"
    echo "##vso[task.logissue type=error]Docker daemon did not become ready within ${DOCKER_READY_TIMEOUT_SECONDS}s after ${restart_count} restart(s)"
    log_diagnostics
    return 1
}

wait_for_docker
