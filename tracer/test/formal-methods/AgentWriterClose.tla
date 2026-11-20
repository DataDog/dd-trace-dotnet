--------------------------- MODULE AgentWriterClose ---------------------------
EXTENDS Naturals, TLC

VARIABLES phase, accepted, inFlight, delivered, dropped, firstCloseReturned,
          secondCloseReturned, workerRunning

vars == <<phase, accepted, inFlight, delivered, dropped, firstCloseReturned,
          secondCloseReturned, workerRunning>>

Init ==
    /\ phase = 0
    /\ accepted = 0
    /\ inFlight = 0
    /\ delivered = 0
    /\ dropped = 0
    /\ firstCloseReturned = FALSE
    /\ secondCloseReturned = FALSE
    /\ workerRunning = TRUE

Next ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ accepted' = 1 /\ inFlight' = 1
       /\ UNCHANGED <<delivered, dropped, firstCloseReturned,
                       secondCloseReturned, workerRunning>>
    \/ /\ phase = 1
       \* The first caller wins _processExit.TrySetResult and waits for cleanup.
       /\ phase' = 2
       /\ UNCHANGED <<accepted, inFlight, delivered, dropped, firstCloseReturned,
                       secondCloseReturned, workerRunning>>
    \/ /\ phase = 2
       \* The second caller loses TrySetResult and returns immediately.
       /\ phase' = 3 /\ secondCloseReturned' = TRUE
       /\ UNCHANGED <<accepted, inFlight, delivered, dropped, firstCloseReturned,
                       workerRunning>>

Ownership == accepted = inFlight + delivered + dropped
CloseBarrier ==
    (firstCloseReturned \/ secondCloseReturned) =>
        (~workerRunning /\ inFlight = 0 /\ accepted = delivered + dropped)

Contract == Ownership /\ CloseBarrier

Spec == Init /\ [][Next]_vars

=============================================================================
