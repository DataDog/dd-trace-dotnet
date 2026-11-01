---------------------------- MODULE TelemetryRetry ----------------------------
EXTENDS Naturals, TLC

CONSTANT Scenario

VARIABLES phase, accepted, queued, retryBuffered, delivered, dropped, worker

vars == <<phase, accepted, queued, retryBuffered, delivered, dropped, worker>>

Init ==
    /\ phase = 0
    /\ accepted = 0
    /\ queued = 0
    /\ retryBuffered = 0
    /\ delivered = 0
    /\ dropped = 0
    /\ worker = TRUE

FinalPushFailure ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<retryBuffered, delivered, dropped, worker>>
    \/ /\ phase = 1
       \* The final push fails and SaveDataIfRequired retains the payload.
       /\ phase' = 2 /\ queued' = 0 /\ retryBuffered' = 1
       /\ UNCHANGED <<accepted, delivered, dropped, worker>>
    \/ /\ phase = 2
       \* The controller exits without another push iteration.
       /\ phase' = 3 /\ worker' = FALSE
       /\ UNCHANGED <<accepted, queued, retryBuffered, delivered, dropped>>

RedactedLogFailure ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<retryBuffered, delivered, dropped, worker>>
    \/ /\ phase = 1
       \* GetLogs destructively drains the collector; the push result is ignored.
       /\ phase' = 2 /\ queued' = 0
       /\ UNCHANGED <<accepted, retryBuffered, delivered, dropped, worker>>

Next ==
    \/ /\ Scenario = "FinalPushFailure" /\ FinalPushFailure
    \/ /\ Scenario = "RedactedLogFailure" /\ RedactedLogFailure

Ownership == accepted = queued + retryBuffered + delivered + dropped
TerminalShutdown == ~worker => accepted = delivered + dropped

Contract == Ownership /\ TerminalShutdown

Spec == Init /\ [][Next]_vars

=============================================================================
