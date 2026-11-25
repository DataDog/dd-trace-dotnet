-------------------------- MODULE TraceTransportRetry --------------------------
EXTENDS Naturals, TLC

VARIABLES phase, accepted, attempts, delivered, dropped, transientFailure

vars == <<phase, accepted, attempts, delivered, dropped, transientFailure>>

Init ==
    /\ phase = 0
    /\ accepted = 0
    /\ attempts = 0
    /\ delivered = 0
    /\ dropped = 0
    /\ transientFailure = FALSE

Next ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1
       /\ UNCHANGED <<attempts, delivered, dropped, transientFailure>>
    \/ /\ phase = 1
       \* The intake receives the full body, then resets before acknowledging it.
       /\ phase' = 2 /\ attempts' = 1 /\ transientFailure' = TRUE
       /\ UNCHANGED <<accepted, delivered, dropped>>
    \/ /\ phase = 2
       \* The callback exception leaves success = Failed_DontRetry.
       /\ phase' = 3 /\ dropped' = 1
       /\ UNCHANGED <<accepted, attempts, delivered, transientFailure>>

TransientFailureIsRetried == phase < 3 \/ ~transientFailure \/ attempts > 1
Ownership == accepted = delivered + dropped

Contract == TransientFailureIsRetried /\ Ownership

Spec == Init /\ [][Next]_vars

=============================================================================
