del run-allocations.txt
call run-allocations.cmd > run-allocations.txt 2>&1

del run-contention.txt
call run-contention.cmd > run-contention.txt 2>&1

del run-cpu-walltime.txt
call run-cpu-walltime.cmd > run-cpu-walltime.txt 2>&1

del run-exceptions.txt
call run-exceptions.cmd > run-exceptions.txt 2>&1

del run-garbagecollections.txt
call run-garbagecollections.cmd > run-garbagecollections.txt 2>&1

del run-liveheap.txt
call run-liveheap.cmd > run-liveheap.txt 2>&1

