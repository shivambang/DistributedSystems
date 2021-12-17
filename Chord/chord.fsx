#r "nuget: Akka.FSharp" 

open System.Security.Cryptography
open System.Text
open System
open Akka.FSharp
open Akka.Actor

let rand = Random()
type Msg = 
    | Init of int*int
    | Create of UInt64*int
    | Join of UInt64
    | FindS of UInt64*UInt64*int*int
    | SetS of UInt64*int*int
    | StartStab
    | Pred of UInt64
    | Stab of UInt64
    | Notify of UInt64
    | Fix
    | Find of UInt64
    | Start of UInt64
    | Stop of UInt64*int

let worker (mailbox:Actor<_>) =
    let mutable nrx = 0
    let mutable nr = 0
    let mutable t = 10000UL
    let mutable n = 0UL
    let mutable pred = 0UL
    let mutable succ = 0UL
    let mutable next = -1
    let mutable bool = true
    let mutable k = 0
    let mutable finger = [||]
    let pow = [for i in 0..63 do yield pown 2UL i]
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Create (x, xr) ->
            nr <- xr
            n <- x 
            pred <- x
            succ <- x
            finger <- [|for i in 0..63 do yield x|]
        | Join node ->
            select ("akka://system/user/sim/" + string node) mailbox.Context.System <! FindS (n, n, -1, 0)
            mailbox.Context.Self <! FindS(node, node, -1, 0)
        | FindS (id, node, nxt, nx) ->
            // printfn "%d %d" id node
            // if nxt = -1 then System.IO.File.AppendAllText("test.txt", sprintf "FindS %d %d\n" id n)
            if ((n < succ) && (n < id && id < succ)) || ((n >= succ) && (n < id || id < succ)) then
                select ("akka://system/user/sim/" + string node) mailbox.Context.System <! SetS (succ, nxt, nx)
            else
                let mutable p = n    
                for i in 0..63 do 
                    if n < id && (n < finger.[i] && finger.[i] < id) then p <- finger.[i]
                    if n > id && (n < finger.[i] || finger.[i] < id) then p <- finger.[i]
                select ("akka://system/user/sim/" + string p) mailbox.Context.System <! FindS (id, node, nxt, nx+1)
                
        | SetS (node, nxt, nx) ->
            if nxt <= 0 then
                succ <- node
                finger.[0] <- node
            else if nxt < 64 then
                bool <- bool && (node = finger.[nxt])
                finger.[nxt] <- node
            else 
                nrx <- nrx + nx
                nr <- nr - 1
                if nr = 0 then 
                    let mutable s = ""
                    for i in 0..63 do 
                        s <- s + sprintf "FIN %d %d %d\n" n i finger.[i]
                    // System.IO.File.AppendAllText("test.txt", s)
                    mailbox.Context.Parent <! Stop (n, nrx)
                // printfn "%d" n

        | Fix ->
            next <- next + 1
            if next >= 64 then 
                if bool && k = 2 then mailbox.Context.Parent <! Start n
                if bool && t < pow.[32] then 
                    k <- k + 1
                    t <- t*2UL
                else if t > 10000UL then
                    t <- t/2UL
                bool <- true
                next <- 0
                // System.IO.File.AppendAllText("test.txt", sprintf "%d %d\n" n t)
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (t)), mailbox.Context.Self, Fix)
            mailbox.Context.Self <! FindS(n + pow.[next], n, next, 0)
            // System.IO.File.AppendAllText("test.txt", sprintf "Fix %d %d %d\n" pred n next)
            
        | StartStab ->
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (t)), mailbox.Context.Self, StartStab)
            select ("akka://system/user/sim/" + string succ) mailbox.Context.System <! Pred n
        | Pred x ->
            select ("akka://system/user/sim/" + string x) mailbox.Context.System <! Stab pred
        | Stab x ->
            if (n < succ) && (n < x && x < succ) then succ <- x
            if (n > succ) && (n < x || x < succ) then succ <- x
            select ("akka://system/user/sim/" + string succ) mailbox.Context.System <! Notify n
            // if x = succ then System.IO.File.AppendAllText("test.txt", sprintf "Su %020d %020d\n" n succ)


        | Notify x ->
            if pred = n then pred <- x
            if (pred < n) && (pred < x && x < n) then pred <- x
            if (pred > n) && (pred < x || x < n) then pred <- x
            // if x = pred then System.IO.File.AppendAllText("test.txt", sprintf "Pr %020d %020d\n" n pred)

        | Find x ->
            mailbox.Context.Self <! FindS (uint64 (rand.Next()) * uint64 (rand.Next()), n, 64, 0)

        | _ -> ()
        return! loop()    
    }
    loop()

let boss (mailbox:Actor<_>) = 
    let sha256Hash = SHA256Managed.Create()
    let mutable s = Set.empty
    let mutable r = Set.empty
    let mutable nrx = 0
    let mutable nr = 0
    let mutable n = 0
    let mutable c = true
    let mutable keys = []
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Init (nk, nrk) ->
            n <- nk
            nr <- nrk
            let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes("worker-1"))
            let en = en.[..7] |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
            let x = UInt64.Parse(en, System.Globalization.NumberStyles.HexNumber);
            let rx = spawn mailbox.Context (string x) worker
            s <- s.Add rx
            rx <! Create (x, nr)
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rx, Fix )
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rx, StartStab)
            for k in 2..n do 
                let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes("worker-" + string k))
                let en = en.[..7] |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
                let w = UInt64.Parse(en, System.Globalization.NumberStyles.HexNumber);
                let rw = spawn mailbox.Context (string w) worker
                rw <! Create (w, nr)
                rw <! Join x
                s <- s.Add rw
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rw, Fix )
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rw, StartStab)
            // for i in s do mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan (int64 (200000) * int64 (n)), TimeSpan (int64 (10000000)), i, Find keys.[rand.Next(keys.Length)])
            printf "Peers: 0000000"
        | Start x ->
            r <- r.Add x
            printf "\b\b\b\b\b\b\b%07d" r.Count
            if c && 5*r.Count >= 4*s.Count then
                c <- false
                for i in s do mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.Zero, TimeSpan (int64 (10000000)), i, Find 0UL)
        | Stop (x, nx) ->
            s <- s.Remove sender
            nrx <- nrx + nx
            printf "\b\b\b\b\b\b\b%07d" s.Count
            if s.IsEmpty then
                printfn "\nAverage Hops = %.2f" (float nrx/(float n * float nr))
                // System.IO.File.AppendAllText("test.txt", sprintf "%d %.2f\n" n (float nrx/(float n * float nr)))
                mailbox.Context.System.Terminate() |> ignore
            // mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (10000000)), mailbox.Context.Self, Find)
        | _ -> ()

        return! loop()
    }
    loop()

let args = fsi.CommandLineArgs
let n = int args.[1]
let nr = int args.[2]
let system = System.create "system" (Configuration.defaultConfig())
let sim =  spawn system "sim" boss
sim <! Init (n, nr)
system.WhenTerminated.Wait()
printfn "END"