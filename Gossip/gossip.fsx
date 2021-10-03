#time "on"
#r "nuget: Akka.FSharp" 

open System
open Akka.FSharp
open Akka.Actor

let system = System.create "system" (Configuration.defaultConfig())


type Msg = 
    | Init of int*string
    | Start of string
    | GOSSIP
    | PUSHSUM
    | AddNB of Set<IActorRef>
    | Stop

let rand = Random()
let rec bins (n, low, high) = 
    if low = high then low
    else
        let m = (low + high) / 2
        if n > m*m*m + (m+1)*(m+1)*(m+1) then bins(n, m, high)
        else if n < m*m*m + (m+1)*(m+1)*(m+1) then bins(n, low, m)
        else m

let nb3d (k, i, w: list<IActorRef>) = 
    let chk c = (c >= 0 && c < (k*k*k + (k+1)*(k+1)*(k+1)))
    if i < (k+1)*(k+1)*(k+1) then 
        let j = i
        let z = j / ((k+1) * (k+1))
        let y = (j - ((k+1) * (k+1) * z)) / (k+1)
        let x = (j - ((k+1) * (k+1) * z)) - ((k+1) * y)
        [for p in 0..1 do for q in 0..1 do for r in 0..1 do
            let nb = (k+1)*(k+1)*(k+1) + (k)*(k)*(z-r) + (k)*(y-q) + (x-p)
            if chk(z-r) && chk(y-q) && chk(x-p) then yield w.[nb]]
    else
        let j = i - (k+1)*(k+1)*(k+1)
        let z = j / (k * k)
        let y = (j - (k * k * z)) / k
        let x = (j - (k * k * z)) - (k * y)
        [for p in 0..1 do for q in 0..1 do for r in 0..1 do
            let nb = (k+1)*(k+1)*(z+r) + (k+1)*(y+q) + (x+p)
            if chk(z+r) && chk(y+q) && chk(x+p) then yield w.[nb]]

let worker (mailbox:Actor<_>) =
    let mutable s = 0
    let mutable w = 0
    let mutable nb = Set.empty
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | AddNB w ->
            nb <- nb + w
        | _ -> ()
        return! loop()    
    }
    loop()

let sim =  spawn system "sim" <| fun mailbox ->
    let mutable n = 0
    let mutable w = []
    let mutable d = Map.empty
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Init (nn, t) ->
            n <- nn
            match t with
            | "FULL" -> 
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]
                let sw = Set(w)
                for i in 0..n-1 do w.[i] <! AddNB (sw - Set([w.[i]]))
            | "LINE" -> 
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]
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
                for i in 0..n-1 do w.[i] <! AddNB (Set(nb3d (k, i, w)))
            | "IMP3D" -> 
                let sw = Set(w)
                let k = bins(n, 1, int 1e3)
                n <- k*k*k + (k+1)*(k+1)*(k+1)
                w <- [for i in 1..n do yield spawn mailbox.Context ("worker-"+string i) worker]     
                for i in 0..n-1 do w.[i] <! AddNB (
                    let nb = Set(nb3d (k, i, w))
                    let rn = Set.toList (sw - nb - Set([w.[i]]))
                    nb + Set([rn.[rand.Next(rn.Length)]])
                    )                
            | _ -> ()
        | Start algo ->
            match algo with
            | "GOSSIP" -> ()
            | "PUSHSUM" -> ()
            | _ -> ()
        | _ -> ()

        return! loop()
    }
    loop()

let args = fsi.CommandLineArgs
sim <! Init ((int args.[1]), args.[2])
Console.ReadLine()
printfn "END"