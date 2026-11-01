--------------------------- MODULE RcmCapabilities ---------------------------
EXTENDS Naturals, TLC

VARIABLES phase, capabilities, firstRead, secondRead, requested

vars == <<phase, capabilities, firstRead, secondRead, requested>>

Init ==
    /\ phase = 0
    /\ capabilities = {}
    /\ firstRead = {}
    /\ secondRead = {}
    /\ requested = {"first", "second"}

Next ==
    \/ /\ phase = 0
       /\ phase' = 1 /\ firstRead' = capabilities
       /\ UNCHANGED <<capabilities, secondRead, requested>>
    \/ /\ phase = 1
       /\ phase' = 2 /\ secondRead' = capabilities
       /\ UNCHANGED <<capabilities, firstRead, requested>>
    \/ /\ phase = 2
       /\ phase' = 3 /\ capabilities' = firstRead \cup {"first"}
       /\ UNCHANGED <<firstRead, secondRead, requested>>
    \/ /\ phase = 3
       /\ phase' = 4 /\ capabilities' = secondRead \cup {"second"}
       /\ UNCHANGED <<firstRead, secondRead, requested>>

NoLostEnable == phase < 4 \/ requested \subseteq capabilities

Spec == Init /\ [][Next]_vars

=============================================================================
