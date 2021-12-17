#r "nuget: Akka.FSharp" 

open System.Security.Cryptography
open System.Text
open System
open Akka.FSharp
open Akka.Configuration
let config =
    ConfigurationFactory.ParseString
        @"akka {
            log-dead-letters = off
        }"
let rand = Random()
type Msg = 
    | Init of int*int*int
    | Create of UInt64*int
    | Join of UInt64
    | FindS of UInt64*UInt64*int*int
    | SetS of UInt64*int*int
    | StartStab
    | Pred of UInt64
    | Stab of UInt64
    | Notify of UInt64
    | Fix
    | Check
    | CP
    | PC
    | FN of int
    | Fail of UInt64*UInt64
    | Find
    | Start of UInt64
    | Stop of UInt64*int

let worker (mailbox:Actor<_>) =
    let mutable nrx = 0
    let mutable nr = 0
    let mutable fr = 0
    let mutable t = 10000UL
    let mutable n = 0UL
    let mutable pred = 0UL
    let mutable succ = 0UL
    let mutable next = -1
    let mutable bool = true
    let mutable chk = false
    let mutable k = 0
    let mutable finger = [||]
    let mutable pnr = [|0; 0|]
    let pow = [for i in 0..63 do yield pown 2UL i]
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Create (x, xr) ->
            nr <- xr
            fr <- xr
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
                    if n < id && (n < finger.[i] && finger.[i] < id) then if n <> finger.[i] then p <- finger.[i]
                    if n > id && (n < finger.[i] || finger.[i] < id) then if n <> finger.[i] then p <- finger.[i]
                select ("akka://system/user/sim/" + string p) mailbox.Context.System <! FindS (id, node, nxt, nx+1)
                
        | SetS (node, nxt, nx) ->
            if nxt <= 0 then
                succ <- node
                finger.[0] <- node
            else if nxt < 64 then
                bool <- bool && (node = finger.[nxt])
                finger.[nxt] <- node
            else 
                mailbox.Context.Parent <! FN nx
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
        | Check ->
            if chk then
                chk <- false
                select ("akka://system/user/sim/" + string pred) mailbox.Context.System <! CP
            else if pred <> n then
                let mutable p = Set.empty.Add succ
                for i in 0..63 do 
                    if pred <> finger.[i] then p <- p.Add finger.[i]
                for i in p do
                    select ("akka://system/user/sim/" + string i) mailbox.Context.System <! Fail (pred, n)
                pred <- n
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (10000)), mailbox.Context.Self, StartStab)
        | CP ->
            select ("akka://system/user/sim/" + string succ) mailbox.Context.System <! PC
        | PC ->
            chk <- true
        | Fail (p, s) ->
            let mutable x = n
            for i in 0..63 do
                if p = finger.[i] then finger.[i] <- x
                else x <- finger.[i]
            if p = succ then
                succ <- s
                select ("akka://system/user/sim/" + string succ) mailbox.Context.System <! Notify n

            else select ("akka://system/user/sim/" + string succ) mailbox.Context.System <! Fail (p, s)
        | Find ->
            if fr > 0 then
                fr <- fr - 1
                mailbox.Context.Parent <! PC
                mailbox.Context.Self <! FindS (uint64 (rand.Next()) * uint64 (rand.Next()), n, 64, 0)
            else
                if pnr.[0] = nr then pnr.[1]  <- pnr.[1] + 1
                else pnr.[0] <- nr
            if pnr.[1] = 3 then mailbox.Context.Parent <! Stop (n, nrx)

        

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
    let mutable nrc = 0
    let mutable nf = 0
    let mutable nl = 0
    let mutable n = 0
    let mutable p = 0
    let mutable c = true
    let mutable keys = []
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Init (nk, nrk, pk) ->
            n <- nk
            nr <- nrk
            p <- pk
            let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes("worker-1"))
            let en = en.[..7] |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
            let x = UInt64.Parse(en, System.Globalization.NumberStyles.HexNumber);
            let rx = spawn mailbox.Context (string x) worker
            s <- s.Add rx
            rx <! Create (x, nr)
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rx, Fix )
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rx, StartStab)
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rx, Check)
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
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.Zero, rw, Check)
            // for i in s do mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan (int64 (200000) * int64 (n)), TimeSpan (int64 (10000000)), i, Find keys.[rand.Next(keys.Length)])
            
        | Start x ->
            r <- r.Add x
            if c && 10*r.Count >= 9*s.Count then
                // System.IO.File.AppendAllText("test.txt", sprintf "\nNodes = %d" s.Count)
                c <- false
                nr <- 0
                for i in s do mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.Zero, TimeSpan (int64 (10000000)), i, Find)
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (20000000)), mailbox.Context.Self, Check)
        | Stop (x, nx) ->
            s <- s.Remove sender
            if s.IsEmpty then
                printfn "Nodes Failed = %d" nf
                printfn "Average Hops = %.2f" (float nrx/float nrc)
                mailbox.Context.System.Terminate() |> ignore
            // mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (10000000)), mailbox.Context.Self, Find)
        | Check ->
            if s.IsEmpty then 
                printfn "Nodes Failed = %d" nf
                printfn "Average Hops = %.2f" (float nrx/float nrc)
                mailbox.Context.System.Terminate() |> ignore
            else
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan (int64 (100000000)), mailbox.Context.Self, Check)
                let j = s
                for i in j do
                    if rand.Next(100) < p then
                        mailbox.Context.Stop(i)
                        s <- s.Remove i
                        nf <- nf + 1
        | PC -> nr <- nr + 1
        | FN nx -> 
            nrx <- nrx + nx
            nrc <- nrc + 1
        | _ -> ()

        return! loop()
    }
    loop()

let args = fsi.CommandLineArgs
let n = int args.[1]
let nr = int args.[2]
let p = int args.[3]
let system = System.create "system" (config)
let sim =  spawn system "sim" boss
let time = DateTime.Now
sim <! Init (n, nr, p)
system.WhenTerminated.Wait()
// System.IO.File.AppendAllText("test.txt", sprintf "\nTIME: %f ms" (DateTime.Now - time).TotalMilliseconds)