#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open System
open Akka.FSharp
open Akka.Actor
open Akka.Remote
open Akka.Configuration
open Akka.Serialization

//Set hostname to current PC IP address
let args = fsi.CommandLineArgs
let ip = args.[1]
let port = args.[2]
let config =
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                serializers {
                    hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                }
                serialization-bindings {
                    ""System.Object"" = hyperion
                }               

            }
            remote {
                helios.tcp {
                    hostname = "+ip+"
                    port = "+port+"
                }
            }
        }")

let system = System.create "client" config

type Tweet(tid:String, m:String, uid:String) = 
    member val uid = uid with get
    member val m = m with get
    member val tid = tid with get

type Msg = 
    | Start of String
    | INIT of Map<char, IActorRef>*Map<int, IActorRef>
    | Register of String*String
    | Login of String*String
    | Logout of String
    | Subs of String*String
    | Tweeter of String*String
    | RTW of String*String
    | Retweet of String*Tweet
    | GetHash of String*String
    | GetSub of String
    | GetMen of String
    | GH of IActorRef*String
    | Parse of String*String*String
    | Send of Tweet*Set<String>
    | Mend of Tweet*List<String>
    | ACK of String
    | RECV of Tweet

type Cmd =
    | INI of int
    | SUB
    | TW
    | REG of String*TimeSpan
    | CHK

let uid (id:int) =
    let mutable s = string (char (id%26 + 97))
    let mutable k = id / 26
    while k >= 26 do
        s <- s + string (char (k%26 + 97))
        k <- k / 26
    if k > 0 then s + string (char ((k-1)%26 + 97))
    else s

let worker (mailbox:Actor<_>) =
    let spool = 
        ['a'..'z'] 
        |> Seq.map (fun f -> f, select ("akka.tcp://server@"+args.[3]+":"+args.[4]+"/user/worker" + string f) system)
        |> Map.ofSeq    
    let mutable uid = ""
    let mutable tid = ""
    let mutable boss = null
    let mutable time = [("TWEET", DateTime.Now); ("RETWEET", DateTime.Now); ("GETHASH", DateTime.Now); ("GETSUB", DateTime.Now); ("GETMEN", DateTime.Now);] |> Map.ofSeq
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Start m -> 
            uid <- m
            boss <- sender
            spool.[uid.[0]] <! Register(uid, "NI")
        | Subs (id, sid) ->
            spool.[sid.[0]] <! Subs(id, sid)
        | Login (id, pass) ->
            spool.[uid.[0]] <! Login(id, pass)
        | Logout (id) ->
            spool.[uid.[0]] <! Logout(id)
        | Tweeter (id, m) ->
            if tid <> "" && m.Length = 16 then
                time <- Map.add "RETWEET" DateTime.Now time
                spool.[tid.[0]] <! RTW (id, tid)
                tid <- ""
            else
                time <- Map.add "TWEET" DateTime.Now time
                spool.[uid.[0]] <! Tweeter (id, m)
            // printfn "%s" m
        | GetHash (id, h) ->
            time <- Map.add "GETHASH" DateTime.Now time
            spool.[uid.[0]] <! GetHash(id, h)
        | GetSub (id) ->
            time <- Map.add "GETSUB" DateTime.Now time
            spool.[uid.[0]] <! GetSub(id)
        | GetMen (id) ->
            time <- Map.add "GETMEN" DateTime.Now time
            spool.[uid.[0]] <! GetMen(id)
        | ACK m -> 
            if Map.containsKey m time then boss <! REG(m, (DateTime.Now - time.[m])) else boss <! REG (m, TimeSpan.Zero)
            // printfn $"{m}"
            // cli <! Start ""
        | RECV t ->
            tid <- t.tid
            if t.uid = "H" then boss <! REG("GETHASH", (DateTime.Now - time.["GETHASH"]))
            if t.uid = "S" then boss <! REG("GETSUB", (DateTime.Now - time.["GETSUB"]))
            if t.uid = "M" then boss <! REG("GETMEN", (DateTime.Now - time.["GETMEN"]))
        | _ -> ()
        return! loop()    
    }
    loop()

let boss = spawn system "boss" <| fun mailbox ->
    let rand = Random()
    let mutable a = 0
    let mutable b = DateTime.Now
    let mutable p = 0
    let mutable n = 0
    let mutable k = 0
    let mutable w = []
    let mutable d = []
    let mutable r = []
    let mutable rp = []
    let mutable time = [("TWEET", TimeSpan.Zero); ("RETWEET", TimeSpan.Zero); ("GETHASH", TimeSpan.Zero); ("GETSUB", TimeSpan.Zero); ("GETMEN", TimeSpan.Zero);] |> Map.ofSeq
    let mutable nr = [("LOGIN", 0); ("LOGOUT", 0); ("TWEET", 0); ("RETWEET", 0); ("GETHASH", 0); ("GETSUB", 0); ("GETMEN", 0);] |> Map.ofSeq
    let mutable mn = Map.empty
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | INI m -> 
            n <- m
            k <- max (5*n/100) 1
            w <- [for i in 0..n-1 do spawn system ("worker" + string i) worker]
            d <- [for i in 0..n-1 do yield uid i]
            for i in 0..n-1 do 
                w.[i] <! Start d.[i]
                a <- a + 1
            r <- [for i in 0..n-1 do yield if rand.Next(n) <= ((n-i)/k+1)*k then true else false]
            rp <- r
        | SUB ->
            b <- DateTime.Now
            // let u = [for i in 1..n do yield 0] |> Array.ofList
            for i in 1..n do
                for j in 1..i-1 do
                    if i % j = 0 then 
                        w.[i-1] <! Subs (d.[i-1], d.[j-1]); a <- a + 1
                        p <- p + 1
                        // u.[j-1] <- u.[j-1] + 1
            // for i in 1..n do System.IO.File.AppendAllText("test.txt", sprintf "%d %d\n" i u.[i-1])
        | TW ->
            p <- p + 1
            for i in 0..n-1 do
                if r.[i] then 
                    w.[i] <! Login (d.[i], "NI")
                    if rand.Next(10) = 0 then w.[i] <! GetHash (d.[i], d.[rand.Next(n)]); a <- a + 1
                    if rand.Next(10) = 0 then w.[i] <! GetSub d.[i]; a <- a + 1
                    if rand.Next(10) = 0 then w.[i] <! GetMen d.[i]; a <- a + 1
                    let mutable m = "this tweet rocks"
                    if rand.Next(4) = 0 then m <- m + " #" + d.[rand.Next(n)]; a <- a + 1
                    if rand.Next(2) = 0 then m <- m + " @" + d.[rand.Next(n)]; a <- a + 1
                    w.[i] <! Tweeter (d.[i], m); a <- a + 1
                else if rp.[i] then w.[i] <! Logout d.[i]; a <- a + 1
            if p < 72 then system.Scheduler.ScheduleTellOnce(TimeSpan (int64 1000000), mailbox.Context.Self, TW)
            else  system.Scheduler.ScheduleTellOnce(TimeSpan (int64 10000000), mailbox.Context.Self, CHK)
            rp <- r
            r <- [for i in 0..n-1 do yield if rand.Next(n) <= ((n-i)/k+1)*k then true else false]
        | REG (m, ts) ->
            if m <> "SUCCESS!" then a <- a - 1
            if m = "REGISTERED!" then
                p <- p + 1
                if p = n then 
                    p <- 0
                    mailbox.Context.Self <! SUB
            else if m = "SUBS!" then
                p <- p - 1
                if p = 0 then 
                    printfn $"Time to Sub: {(DateTime.Now - b).TotalMilliseconds} ms"
                    mailbox.Context.Self <! TW
                    b <- DateTime.Now
            else
                if Map.containsKey m time then 
                    let t, q = if Map.containsKey (sender, m) mn then mn.[(sender, m)] else (0.0, 0.0)
                    mn <- Map.add (sender, m) (t+ts.TotalMilliseconds, q+1.0) mn
                    time <- Map.add m (time.[m] + ts) time
                if Map.containsKey m nr then nr <- Map.add m (nr.[m] + 1) nr
        | CHK ->
            if a <= 0 then
                printfn $"Time Elapsed: {(DateTime.Now - b).TotalMilliseconds} ms"
                printfn "Type\tNo"
                let mutable init = Map [("TWEET", 0.0); ("RETWEET", 0.0); ("GETHASH", 0.0); ("GETSUB", 0.0); ("GETMEN", 0.0);]
                // |> List.iter (fun (k, v) -> printfn $"{k}\t{nr.[k]}\t{time.[k].TotalSeconds} s")
                mn |> Map.iter (fun (s, m) (t, q) -> init <- Map.add m (init.[m] + t/q) init)
                nr |> Map.iter (fun k v -> printfn "%s\t%d\t" k nr.[k])
                system.Terminate() |> ignore
            else  system.Scheduler.ScheduleTellOnce(TimeSpan (int64 10000000), mailbox.Context.Self, CHK)
        return! loop()    
    }
    loop()

boss <! INI (int args.[5])
system.WhenTerminated.Wait()
printfn "END"