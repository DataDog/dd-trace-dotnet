.intel_syntax noprefix
.text
.global dd_restart_wrapper
.global dd_restart_wrapper_size

# Function description (comments preserved from the original)
# When a thread is interrupted, we replace the Rip by the address of this function
# We pass in rdi the address of a data structure which contains
# - the next instruction to run
# - 8-byte memory area to store a value
# - previous value of rdi
# Since the processor will jump directly to this function
# we need to simulate a call by push the next instruction on the stack
# then going for the prologue.
# So when `leave` is called, the epilogue is executed and when
# `ret` is called, the next instruction will be used.
# This function must guarantee that all the registers will
# have the same values before the jump.
# This function does not modify any registers except rdi
# but it's restored before leaving
# Added an epilogue and prologue in case suspend the thread while executing this code
# and try to unwind the callstack :crossfinger:

dd_restart_wrapper:
    # This function is not called.
    # We simulate a call by pushing [rdi] on the stack
    # [rdi] contains the return address
    push [rdi]    # [rdi] points to the next RIP

    # Prologue
    push rbp
    mov rbp, rsp
    and rsp, -16  # Make sure the stack is 16-byte aligned

    # Mark the execution as resumed
    mov qword ptr [rdi + 8], 1
    
    # Restore rdi
    mov rdi, qword ptr [rdi + 16]
    
    # Epilogue
    leave

    ret

dd_restart_wrapper_end:
dd_restart_wrapper_size = . - dd_restart_wrapper  # Calculate size of the function

.section .note.GNU-stack,"",@progbits