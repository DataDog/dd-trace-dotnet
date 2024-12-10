PUBLIC dd_restart_wrapper
.386
.model flat, C
.SAFESEH dd_restart_wrapper

.code

; When a thread is interrupted, we replace the Rip by the address of this function
; We pass in rdi the address of a data structure which contains
;- the next instruction to run
;- 8-byte memory area to store a value
;- previous value of rdi
; Since the processor will jump directly to this function
; we need to simulate a call by push the next instruction on the stack
; then going for the prologue.
; So when `leave` is call, the epilogue is executed and when
; `ret` is called, the next instruction will be used.
; This function must guarantee that all the register will
; have the same values before the jump.
; This function does not modify any registers except rdi
; but it's restored before leaving
; Added an epilogue and prologue in case suspend the thread while executing this code
; and try to unwind the callstack :crossfinger:
dd_restart_wrapper PROC
    ; this function is not called.
    ; we simulate a call by pushing [edi] on the stack
    ; [edi] contains the return address
    push [edi]     ; [edi] points to the next EIP

    ; mark the execution as resumed
    mov DWORD PTR [edi+4], 1
    
    ; restore edi
    mov edi, dword ptr [edi + 8]

    ret
dd_restart_wrapper ENDP
END