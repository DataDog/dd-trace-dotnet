PUBLIC dd_restart_wrapper
PUBLIC dd_restart_wrapper_end

.data
dd_restart_wrapper_size QWORD 0

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
    ; we simulate a call by pushing [rdi] on the stack
    ; [rdi] contains the return address
    push [rdi]     ; [rdi] points to the next RIP

    ; prologue
    push rbp
    mov rbp, rsp
    and rsp, -16 ;; make sure the stack is 16-byte aligned

    ; mark the execution as resumed
    mov QWORD PTR [rdi+8], 1
    
    ; restore rdi
    mov rdi, qword ptr [rdi + 16]
    
    ; epilogue
    leave

    ret
dd_restart_wrapper ENDP

dd_restart_wrapper_end:
mov dd_restart_wrapper_size, OFFSET dd_restart_wrapper_end - OFFSET dd_restart_wrapper  ; Calculate size of the function

END