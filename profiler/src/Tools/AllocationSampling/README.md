# Manual Testing for Randomized Allocation Sampling

This folder has a test app (Allocate sub-folder) and a profiler (AllocationProfiler sub-folder) that together can be used to experimentally
observe the distribution of sampling events that are generated for different allocation scenarios. To run it:

1. Build both projects (Allocate should be compiled for .NET 10).
2. Run the Allocate app with dotnet and use the --scenario argument to select an allocation scenario you want to validate
3. The Allocate app will print its own PID to the console and wait.
4. Run the AllocationProfiler passing in the allocate app PID as an argument
5. Hit Enter in the Allocate app to begin the allocations. You will see output in the profiler app's console showing the measurements. For example:

```
   SCount          SSize   UnitSize     UpscaledSize     PoissonSize  UpscaledCount  PoissonCount  Name
-----------------------------------------------------------------------------------------------------------------------------
      20            480         24          2048306         2048240          85346         85343  Object24
      34           1088         32          3482153         3482144         108817        108817  Object32
      51           2448         48          5223401         5223624         108820        108825  Object48
      81           6480         80          8297814         8297640         103722        103720  Object80
     142          20448        144         14551917        14551026         101054        101048  Object144
```

- The **S**-prefixed colums refer to data from AllocationSampled events payload
- The **Upscaled**XXX columns are upscaled using the remainder in the event payload
- The **Poisson**XXX columns are upscaled using the Poisson process formula (not using the remainder in the event payload)

In this special case, the same number of 100000 instances were created and should be checked in the **(Upscaled/Poisson)Count** columns.

The % of differences in the count value are also provided (remainder | Poisson)
```
Object144
-------------------------
   1  -17.5 % |  -17.5 %
   2  -16.7 % |  -16.7 %
   3  -16.0 % |  -16.0 %
   4  -15.3 % |  -15.3 %
   5  -15.3 % |  -15.3 %
        ...
  49    1.1 % |    1.0 %
  50    1.8 % |    1.8 %
  51    1.8 % |    1.8 %
        ...
  96   16.0 % |   16.0 %
  97   16.0 % |   16.0 %
  98   16.7 % |   16.7 %
  99   18.1 % |   18.1 %
 100   21.0 % |   21.0 %
 ```


Feel free to allocate the patterns you want in other methods of the **_Allocate_** project and use the _DynamicAllocationSampling_ events listener to get a synthetic view of the different allocation events.