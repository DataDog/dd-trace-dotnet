------------------------------ MODULE RcmDelivery ------------------------------
EXTENDS Naturals, TLC

CONSTANT Scenario

VARIABLES phase, cached, failedDelivered, lateDelivered, unrelatedDelivered

vars == <<phase, cached, failedDelivered, lateDelivered, unrelatedDelivered>>

Init ==
    /\ phase = 0
    /\ cached = FALSE
    /\ failedDelivered = FALSE
    /\ lateDelivered = FALSE
    /\ unrelatedDelivered = FALSE

ApplyFailure ==
    \/ /\ phase = 0
       /\ phase' = 1
       /\ cached' = TRUE
       /\ UNCHANGED <<failedDelivered, lateDelivered, unrelatedDelivered>>
    \/ /\ phase = 1
       /\ phase' = 2
       /\ UNCHANGED <<cached, failedDelivered, lateDelivered, unrelatedDelivered>>

RemoveFailure ==
    \/ /\ phase = 0
       /\ phase' = 1
       /\ cached' = TRUE
       /\ UNCHANGED <<failedDelivered, lateDelivered, unrelatedDelivered>>
    \/ /\ phase = 1
       /\ phase' = 2
       /\ cached' = FALSE
       /\ UNCHANGED <<failedDelivered, lateDelivered, unrelatedDelivered>>
    \/ /\ phase = 2
       /\ phase' = 3
       /\ UNCHANGED <<cached, failedDelivered, lateDelivered, unrelatedDelivered>>

UnrelatedRemoval ==
    \/ /\ phase = 0
       /\ phase' = 1
       /\ cached' = TRUE
       /\ UNCHANGED <<failedDelivered, lateDelivered, unrelatedDelivered>>
    \/ /\ phase = 1
       /\ phase' = 2
       /\ cached' = FALSE
       /\ unrelatedDelivered' = TRUE
       /\ UNCHANGED <<failedDelivered, lateDelivered>>

LateSubscriber ==
    \/ /\ phase = 0
       /\ phase' = 1
       /\ cached' = TRUE
       /\ UNCHANGED <<failedDelivered, lateDelivered, unrelatedDelivered>>
    \/ /\ phase = 1
       /\ phase' = 2
       /\ UNCHANGED <<cached, failedDelivered, lateDelivered, unrelatedDelivered>>
    \/ /\ phase = 2
       /\ phase' = 3
       /\ UNCHANGED <<cached, failedDelivered, lateDelivered, unrelatedDelivered>>

Next ==
    \/ /\ Scenario = "ApplyFailure" /\ ApplyFailure
    \/ /\ Scenario = "RemoveFailure" /\ RemoveFailure
    \/ /\ Scenario = "UnrelatedRemoval" /\ UnrelatedRemoval
    \/ /\ Scenario = "LateSubscriber" /\ LateSubscriber

Contract ==
    /\ (Scenario = "ApplyFailure" => ~(phase = 2 /\ ~failedDelivered))
    /\ (Scenario = "RemoveFailure" => ~(phase = 3 /\ ~failedDelivered))
    /\ (Scenario = "UnrelatedRemoval" => ~unrelatedDelivered)
    /\ (Scenario = "LateSubscriber" => ~(phase = 3 /\ ~lateDelivered))

Spec == Init /\ [][Next]_vars

=============================================================================
