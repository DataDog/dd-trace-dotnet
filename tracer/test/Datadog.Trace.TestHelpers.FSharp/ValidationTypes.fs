namespace Datadog.Trace.TestHelpers.FSharp

module ValidationTypes =
    // Copy validation types from https://fsharpforfunandprofit.com/posts/recipe-part3/
    type Result<'TSuccess,'TFailure> =
        | Success of 'TSuccess
        | Failure of 'TFailure
    
    let succeed x =
        Success x
    
    let fail x =
        Failure x

    let bind switchFunction twoTrackInput =
        match twoTrackInput with
        | Success s -> switchFunction s
        | Failure f -> Failure f

    let (>=>) switch1 switch2 =
        switch1 >> (bind switch2)

    let plus addSuccess addFailure switch1 switch2 x =
        match (switch1 x),(switch2 x) with
        | Success s1,Success s2 -> Success (addSuccess s1 s2)
        | Failure f1,Success _  -> Failure f1
        | Success _ ,Failure f2 -> Failure f2
        | Failure f1,Failure f2 -> Failure (addFailure f1 f2)

    // create a "plus" function for validation functions
    let (&&&) v1 v2 =
        let addSuccess r1 r2 = r1 // return first
        let addFailure s1 s2 = s1 + "; " + s2  // concat
        plus addSuccess addFailure v1 v2