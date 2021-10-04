#r "nuget: Akka.FSharp" 

open System
open Akka.FSharp
open Akka.Actor

let system = System.create "system" (Configuration.defaultConfig())


type Msg = 
    | Init of int*string
    | Start of string
    | Cons of float
    | GOSSIP
    | PS
    | PUSHSUM of float*float
    | AddNB of Set<IActorRef>
    | RemNB of Set<IActorRef>
    | Stop of float
    | Check

let rand = Random()
let rec bins (n, low, high) = 
    if low >= high then low
    else
        let m = (low + high) / 2
        if n > m*m*m + (m+1)*(m+1)*(m+1) then bins(n, m+1, high)
        else if n < m*m*m + (m+1)*(m+1)*(m+1) then bins(n, low, m)
        else m

let nb3d (k, i, w: list<IActorRef>) = 
    let chk c = (c >= 0 && c < (k*k*k + (k+1)*(k+1)*(k+1)))
    if i < (k+1)*(k+1)*(k+1) then 
        let j = i
        let z = j / ((k+1) * (k+1))
        let y = (j - ((k+1) * (k+1) * z)) / (k+1)
        let x = (j - ((k+1) * (k+1) * z)) - ((k+1) * y)
        [for p in 0..1 do
            for q in 0..1 do
                for r in 0..1 do
                    let nb = (k+1)*(k+1)*(k+1) + (k)*(k)*(z-r) + (k)*(y-q) + (x-p) 
                    if chk(z-r) && chk(y-q) && chk(x-p) && chk(nb) then yield w.[nb]
        ]
    else
        let j = i - (k+1)*(k+1)*(k+1)
        let z = j / (k * k)
        let y = (j - (k * k * z)) / k
        let x = (j - (k * k * z)) - (k * y)
        [for p in 0..1 do
            for q in 0..1 do
                for r in 0..1 do
                    let nb = (k+1)*(k+1)*(z+r) + (k+1)*(y+q) + (x+p)
                    if chk(z+r) && chk(y+q) && chk(x+p) && chk(nb) then yield w.[nb]
        ]

let worker (mailbox:Actor<_>) =
    let mutable s = (float 0)
    let mutable w = (float 1)
    let mutable r = (float 0)
    let mutable nc = 0
    let mutable ns = Set.empty
    let mutable nl = []
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Cons m ->
            s <- m
        | AddNB w ->
            ns <- ns + w
            nl <- Set.toList ns
        | RemNB w ->
            ns <- ns - w
            nl <- Set.toList ns
        | GOSSIP ->
            if nl.Length = 0 then mailbox.Context.Parent <! Stop r
            else
                mailbox.Context.Parent <! Check
                nc <- nc + 1
                if nc < 10 then 
                    nl.[rand.Next(nl.Length)] <! GOSSIP
                else if nc = 10 then
                    for w in nl do
                        w <! RemNB (Set([mailbox.Context.Self]))
                    nl.[rand.Next(nl.Length)] <! GOSSIP     //Important
                    mailbox.Context.Parent <! Stop r
                    // mailbox.Context.Stop(mailbox.Context.Self)
                // printf "%d\t%s <- %s\n" s (mailbox.Context.Self.ToString()) (sender.ToString())
        | PS ->
            if nl.Length <> 0 then 
                mailbox.Context.Parent <! Check
                nl.[rand.Next(nl.Length)] <! PUSHSUM (s/2.0, w/2.0)
                s <- s - s/2.0
                w <- w - w/2.0
        
        | PUSHSUM (rs, rw) ->
            if nl.Length <> 0 then 
                mailbox.Context.Parent <! Check
                s <- s + rs
                w <- w + rw
                nl.[rand.Next(nl.Length)] <! PUSHSUM (s/2.0, w/2.0)
                s <- s - s/2.0
                w <- w - w/2.0

        | Check ->
            if abs (r - (s/w)) < 1e-10  then nc <- nc + 1 else nc <- 0
            if nc = 2 then 
                for i in nl do
                    i <! RemNB (Set([mailbox.Context.Self]))
                mailbox.Context.Parent <! Stop r
            else    
                r <- s/w
        | _ -> ()
        return! loop()    
    }
    loop()

let sim =  spawn system "sim" <| fun mailbox ->
    let mutable algo = PS
    let mutable n = 0
    let mutable c = 0
    let mutable w = []
    let mutable sw = Set.empty
    let mutable b = DateTime.Now
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Init (nn, t) ->
            n <- nn

            match t with
            | "FULL" -> 
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]
                sw <- Set(w)
                for i in 0..n-1 do w.[i] <! AddNB (sw - Set([w.[i]]))
            | "LINE" -> 
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]
                sw <- Set(w)
                for i in 0..n-1 do 
                    w.[i] <! AddNB (
                        if i > 0 && i < n-1 then Set([w.[i-1]; w.[i+1]]) 
                        else if i > 0 then Set([w.[i-1]]) 
                        else Set([w.[i+1]])
                        )
            | "3D" -> 
                let k = bins(n, 1, int 1e3)
                n <- k*k*k + (k+1)*(k+1)*(k+1)
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]     
                sw <- Set(w)
                for i in 0..n-1 do w.[i] <! AddNB (Set(nb3d (k, i, w)))
            | "IMP3D" -> 
                let k = bins(n, 1, int 1e3)
                n <- k*k*k + (k+1)*(k+1)*(k+1)
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]     
                sw <- Set(w)
                for i in 0..n-1 do 
                    w.[i] <! AddNB (
                        let nb = Set(nb3d (k, i, w))
                        let rn = Set.toList (sw - nb - Set([w.[i]]))
                        if rn.Length > 0 then (nb + Set([rn.[rand.Next(rn.Length)]]))
                        else nb
                        )                
            | _ -> ()
            printfn "n = %d" n
        | Start str ->
            printf "Percent Terminated: 000.000"
            match str with
            | "GOSSIP" -> 
                algo <- GOSSIP
                w.[rand.Next(w.Length)] <! GOSSIP
            | "PUSHSUM" -> 
                for i in 1..n do 
                    w.[i-1] <! Cons (float i)
                    mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.Zero, TimeSpan (int64 (10000*n)), w.[i-1], Check)  // n * 1ms
                algo <- PS
                w.[rand.Next(w.Length)] <! PS
            | _ -> ()
        | Stop r ->
            c <- c + 1
            sw <- sw - Set([sender])
            w <- Set.toList sw
            let pc = 100.0 * (float c) / (float n)
            printf "\b\b\b\b\b\b\b%07.3f" pc 
            if c = n then 
                printfn "\ns/w = %f" r
                mailbox.Context.System.Terminate() |> ignore
        | Check ->
            if w.Length > 0 && (DateTime.Now - b).Ticks > (int64 100000) then w.[rand.Next(w.Length)] <! algo
            b <- DateTime.Now
        | _ -> ()

        return! loop()
    }
    loop()

let args = fsi.CommandLineArgs
sim <! Init ((int args.[1]), args.[2].ToUpper())
let time = DateTime.Now
sim <! Start (args.[3].ToUpper())
system.Scheduler.ScheduleTellRepeatedly(TimeSpan.Zero, TimeSpan (int64 100000), sim, Check) //10ms
system.WhenTerminated.Wait()
printfn "\nTIME: %f ms" (DateTime.Now - time).TotalMilliseconds