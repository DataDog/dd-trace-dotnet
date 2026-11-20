------------------------------ MODULE DataStreams ------------------------------
EXTENDS Naturals, TLC

CONSTANT Scenario

VARIABLES phase, accepted, queued, aggregated, delivered, dropped, flushReturned,
          closeReturned, sendRunning

vars == <<phase, accepted, queued, aggregated, delivered, dropped, flushReturned,
          closeReturned, sendRunning>>

Init ==
    /\ phase = 0
    /\ accepted = 0
    /\ queued = 0
    /\ aggregated = 0
    /\ delivered = 0
    /\ dropped = 0
    /\ flushReturned = FALSE
    /\ closeReturned = FALSE
    /\ sendRunning = FALSE

FailedSend ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<aggregated, delivered, dropped, flushReturned, closeReturned, sendRunning>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ queued' = 0 /\ aggregated' = 1
       /\ UNCHANGED <<accepted, delivered, dropped, flushReturned, closeReturned, sendRunning>>
    \/ /\ phase = 2
       /\ phase' = 3 /\ aggregated' = 0
       /\ UNCHANGED <<accepted, queued, delivered, dropped, flushReturned, closeReturned, sendRunning>>

FalseFlushAck ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ sendRunning' = TRUE
       /\ UNCHANGED <<accepted, queued, aggregated, delivered, dropped, flushReturned, closeReturned>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ accepted' = 1 /\ queued' = 1
       /\ UNCHANGED <<aggregated, delivered, dropped, flushReturned, closeReturned, sendRunning>>
    \/ /\ phase = 2
       /\ phase' = 3 /\ flushReturned' = TRUE
       /\ UNCHANGED <<accepted, queued, aggregated, delivered, dropped, closeReturned, sendRunning>>

CloseWhileSending ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ sendRunning' = TRUE
       /\ UNCHANGED <<queued, aggregated, delivered, dropped, flushReturned, closeReturned>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ closeReturned' = TRUE
       /\ UNCHANGED <<accepted, queued, aggregated, delivered, dropped, flushReturned, sendRunning>>

Next ==
    \/ /\ Scenario = "FailedSend" /\ FailedSend
    \/ /\ Scenario = "FalseFlushAck" /\ FalseFlushAck
    \/ /\ Scenario = "CloseWhileSending" /\ CloseWhileSending

Ownership == accepted = queued + aggregated + delivered + dropped
FlushBarrier == flushReturned => accepted = delivered + dropped
CloseBarrier == closeReturned => ~sendRunning

Contract == Ownership /\ FlushBarrier /\ CloseBarrier

Spec == Init /\ [][Next]_vars

=============================================================================
