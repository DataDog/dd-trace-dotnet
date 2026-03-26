#!/usr/bin/env bash
# Linux Docker readiness check — mirrors the Windows PowerShell logic in ensure-docker-ready.yml.
# Waits for the Docker daemon, attempts service restarts if needed, and fails fast
# so the job can be rescheduled on a different agent.

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
    if command -v systemctl >/dev/null 2>&1; then
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
    else
        log "systemctl not available, cannot restart Docker service"
        return 1
    fi
}

wait_for_docker()
{
    local elapsed=0
    local restart_count=0

    log "Waiting up to ${DOCKER_READY_TIMEOUT_SECONDS}s for Docker daemon (will attempt up to ${DOCKER_MAX_RESTARTS} service restarts)..."

    # Log initial service state
    if command -v systemctl >/dev/null 2>&1; then
        local initial_status
        initial_status=$(systemctl is-active docker 2>&1 || true)
        log "Docker service initial state: ${initial_status}"
    fi

    while [ "${elapsed}" -lt "${DOCKER_READY_TIMEOUT_SECONDS}" ]; do
        if docker info >/dev/null 2>&1; then
            log "Docker daemon is ready (waited ${elapsed}s, ${restart_count} restart(s) performed)"
            docker info
            return 0
        fi

        # If Docker is not responding, try restarting the service
        if command -v systemctl >/dev/null 2>&1; then
            local svc_status
            svc_status=$(systemctl is-active docker 2>&1 || true)
            if [ "${svc_status}" != "active" ] && [ "${restart_count}" -lt "${DOCKER_MAX_RESTARTS}" ]; then
                restart_count=$((restart_count + 1))
                log "Docker service is ${svc_status}. Attempting restart ${restart_count}/${DOCKER_MAX_RESTARTS}..."
                try_restart_docker
            elif [ "${svc_status}" != "active" ] && [ "${restart_count}" -ge "${DOCKER_MAX_RESTARTS}" ]; then
                log "Docker service is ${svc_status} but max restarts (${DOCKER_MAX_RESTARTS}) exhausted"
            fi
        fi

        log "Docker not ready yet (${elapsed}s elapsed), retrying in ${DOCKER_READY_CHECK_INTERVAL_SECONDS}s..."
        sleep "${DOCKER_READY_CHECK_INTERVAL_SECONDS}"
        elapsed=$((elapsed + DOCKER_READY_CHECK_INTERVAL_SECONDS))
    done

    log "Docker daemon did not become ready within ${DOCKER_READY_TIMEOUT_SECONDS}s after ${restart_count} restart(s)"
    log_diagnostics
    return 1
}

wait_for_docker
