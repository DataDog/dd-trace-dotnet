------------------------------ MODULE BatchingSink ------------------------------
EXTENDS Naturals, TLC

CONSTANT Scenario

VARIABLES phase, accepted, queued, delivered, dropped, flushRequested, ack, worker

vars == <<phase, accepted, queued, delivered, dropped, flushRequested, ack, worker>>

Init ==
    /\ phase = 0
    /\ accepted = 0
    /\ queued = 0
    /\ delivered = 0
    /\ dropped = 0
    /\ flushRequested = FALSE
    /\ ack = FALSE
    /\ worker = TRUE

Saturation ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<delivered, dropped, flushRequested, ack, worker>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ accepted' = 2 /\ queued' = 1
       /\ UNCHANGED <<delivered, dropped, flushRequested, ack, worker>>

FailedBatch ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<delivered, dropped, flushRequested, ack, worker>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ queued' = 0
       /\ UNCHANGED <<accepted, delivered, dropped, flushRequested, ack, worker>>

FalseFlushAck ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<delivered, dropped, flushRequested, ack, worker>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ flushRequested' = TRUE
       /\ UNCHANGED <<accepted, queued, delivered, dropped, ack, worker>>
    \/ /\ phase = 2
       /\ phase' = 3 /\ queued' = 0 /\ ack' = TRUE
       /\ UNCHANGED <<accepted, delivered, dropped, flushRequested, worker>>

FlushAfterDispose ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ worker' = FALSE
       /\ UNCHANGED <<accepted, queued, delivered, dropped, flushRequested, ack>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ flushRequested' = TRUE
       /\ UNCHANGED <<accepted, queued, delivered, dropped, ack, worker>>

Next ==
    \/ /\ Scenario = "Saturation" /\ Saturation
    \/ /\ Scenario = "FailedBatch" /\ FailedBatch
    \/ /\ Scenario = "FalseFlushAck" /\ FalseFlushAck
    \/ /\ Scenario = "FlushAfterDispose" /\ FlushAfterDispose

Ownership == accepted = queued + delivered + dropped
FlushBarrier == ack => accepted = delivered + dropped
FlushEventuallyAcknowledged == [](flushRequested => <>ack)

SafetyContract ==
    /\ Ownership
    /\ FlushBarrier

Spec == Init /\ [][Next]_vars

=============================================================================
