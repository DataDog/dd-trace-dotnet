# Orchestration Instrumentation

Orchestration integrations provide observability into workflow engines and task orchestration systems like Temporal, Step Functions, Airflow, Prefect, and Dagster.

## What to Trace

### Workflow Execution (Critical)
- Workflow/DAG start: `startExecution()`, `run_workflow()`
- Workflow completion or failure

### Task/Activity Execution (Critical)
- Individual task execution within a workflow
- Activity invocations in Temporal
- Step execution in Step Functions

### Workflow Triggers
- Manual triggers
- Scheduled triggers
- Event-based triggers

### State Transitions
- Task state changes (pending → running → completed)
- Workflow state changes
- Retry attempts

## What to Skip

### Workflow Definition
- DAG definition
- Task registration
- Workflow configuration

### Scheduler Operations
- Internal scheduling decisions
- Queue management
- Worker polling

### Administrative Operations
- Workflow listing
- History queries
- Cleanup operations

## Context Propagation

**Workflow → Task**: Inject trace context when dispatching tasks/activities so each task span is linked to the workflow span.

**Task → Subtask**: Continue propagation for nested task invocations.

**Cross-Service**: When workflows invoke external services, propagate context to maintain end-to-end traces.

## Parent-Child Relationships

Typical span hierarchy:
```
Workflow Execution (parent)
├── Task 1 Execution
├── Task 2 Execution
│   └── External Service Call
└── Task 3 Execution
```

## Retry Handling

- Each retry attempt should be visible
- Link retries to original task
- Capture retry count and reason
