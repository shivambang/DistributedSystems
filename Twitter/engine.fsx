#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.Serialization.Hyperion"

open System.Security.Cryptography
open System.Diagnostics
open System.Text.RegularExpressions
open System
open Akka.FSharp
open Akka.Actor
open Akka.Remote
open Akka.Configuration
open Akka.Serialization

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

let system = System.create "server" config

let cp (pc:array<char>) = 
    let mutable i = pc.Length - 1; //5
    while i > 2 && int pc.[i] + 1 > 126 do
        pc.[i] <- '?'
        i <- i - 1
    pc.[i] <- char (int pc.[i] + 1)
    pc

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

type User(id:string) =
    member val subs = Map.empty with get, set
    member val tweets = List.empty<string> with get, set
    member val addr = Set.empty<IActorRef> with get, set
    member val send = List.empty<Tweet> with get, set
    member val mend = List.empty<Tweet> with get, set
    member this.id = id
    // member this.pass = pass
    override this.ToString() = 
       this.id


let hasher (mailbox:Actor<_>) =
    let mutable hash = Map.empty 
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Send (t, tags) ->
            for k in tags do
                if Map.containsKey k hash then hash <- Map.add k ([t] @ hash.[k]) hash
                else hash <- Map.add k [t] hash
        | GH (ur, h) ->
            let m = if hash.ContainsKey h then hash.[h] else [Tweet("", "NO TWEETS!", "")]
            let m = List.fold (fun k (i: Tweet) -> k + $"\n[{i.uid}][Tweet] [{i.tid}]\n{i.m}\n") "" m
            ur <! RECV (Tweet("", m, "H"))
        | _ -> ()
        return! loop()    
    }
    loop()

let parser (mailbox:Actor<_>) =
    let nw = 2*Environment.ProcessorCount //No of workers
    let tag = Regex(@"#.*?\s")
    let at = Regex(@"@.*?\s")
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Parse (uid, id, m) ->
            let rm = $"[{uid}][Mention] [{id}] \n{m}"
            let t = Tweet(id, rm, uid)
            [for i in at.Matches(m+" ") -> i.Value.Trim([|'@'; ' '|])]
            |> List.groupBy (fun i -> i.[0])
            |> List.iter (
                fun (k, v) ->
                let ref = select ("user/worker" + string k) system
                ref <! Mend(t, v)
            )
            let t = Tweet(id, m, uid)
            [for i in tag.Matches(m+" ") -> i.Value.Trim([|'#'; ' '|])]
            |> List.groupBy (fun i -> (int i.[0])%nw)
            |> List.iter (
                fun (k, v) ->
                let ref = select ("user/hasher" + string k) system
                ref <! Send(t, Set(v))
            )
        | _ -> ()
        return! loop()    
    }
    loop()

let worker id (mailbox:Actor<_>) =
    let nw = 2*Environment.ProcessorCount //No of workers
    let parse = spawn mailbox.Context ("parser"+string id) parser
    let mutable wpool = Map.empty
    let mutable hpool = Map.empty
    let mutable tid = [|id; '?'; '?'; '?'; '?'; '?';|]
    let mutable tweets = Map.empty<string, Tweet>
    let mutable users = Map.empty
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | INIT (w, h) ->
            wpool <- w
            hpool <- h
        | Register (uid, pass) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then sender <! ACK "USER ALREADY EXISTS!"
            else 
                let user = User(uid)
                user.addr <- Set.add sender user.addr
                user.subs <-
                    ['a'..'z'] 
                    |> Seq.map (fun f -> f, Set.empty) 
                    |> Map.ofSeq    
                users <- Map.add uid user users
                sender <! ACK "REGISTERED!"
        | Login (uid, pass) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then
                let user = users.[uid]
                user.addr <- Set.add sender user.addr
                users <- Map.add uid user users
                sender <! ACK "LOGIN"
            else    sender <! ACK "NO SUCH USER!"
        | Logout (uid) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then
                let user = users.[uid]
                user.addr <- Set.remove sender user.addr
                users <- Map.add uid user users
                sender <! ACK "LOGOUT"
            else    sender <! ACK "NO LOGIN!"
        | Subs (uid, sid) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey sid users then
                let user = users.[sid]
                user.subs <- Map.add uid.[0] (Set.add uid user.subs.[uid.[0]]) user.subs
                users <- Map.add sid user users
                sender <! ACK "SUBS!"
            else    sender <! ACK "NO SUCH USER!"
            
        | Tweeter (uid, m) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then
                tid <- cp tid
                let id = System.String.Concat(tid)
                let user = users.[uid]
                user.tweets <- [id] @ user.tweets
                if user.tweets.Length > 10 then user.tweets <- user.tweets.[0..9]
                tweets <- Map.add id (Tweet(id, m, uid)) tweets
                let rm = $"[{uid}][Tweet] [{id}]\n{m}"
                let t = Tweet(id, rm, uid)
                user.subs
                |> Map.iter (
                    fun k v -> 
                        wpool.[k] <! Send (t, v)
                )
                //wpool.[uid[0]] <! Send (uid, id, m)
                parse <! Parse (uid, id, m)
                sender <! ACK "TWEET"
            else    sender <! ACK "NO LOGIN!"
        
        | RTW (uid, id) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey id tweets then
                wpool.[uid.[0]] <! Retweet (uid, tweets.[id])
                sender <! ACK "RETWEET"
            else sender <! ACK "NO SUCH TWEET!"


        | Retweet (uid, t) ->
            //wpool.[uid[0]] <! Resend (uid, id, tweets.[id])
            if Map.containsKey uid users then
                let rm = $"{uid} Retweeted\n[{t.uid}][Tweet] [{t.tid}]\n{t.m}"
                let t = Tweet(t.tid, rm, uid)
                let user = users.[uid]
                users.[uid].subs
                |> Map.iter (
                    fun k v -> 
                        wpool.[k] <! Send (t, v)
                )

        | Send (t, v) ->
            for k in v do
                users.[k].send <- [t] @ users.[k].send 
                if users.[k].send.Length > 10 then users.[k].send <- users.[k].send.[0..9]
                if not (Set.isEmpty users.[k].addr) then 
                    for i in users.[k].addr do i <! RECV t
        | Mend (t, v) ->
            for k in v do
                users.[k].mend <- [t] @ users.[k].mend 
                if users.[k].mend.Length > 10 then users.[k].mend <- users.[k].mend.[0..9]
                if not (Set.isEmpty users.[k].addr) then 
                    for i in users.[k].addr do i <! RECV t
        | GetHash (uid, h) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then
                hpool.[(int h.[0])%nw] <! GH (sender, h)
                sender <! ACK "SUCCESS!"
            else    sender <! ACK "NO LOGIN!"
        | GetSub (uid) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then
                let user = users.[uid]
                let m = List.fold (fun k (i: Tweet) -> k + $"\n{i.m}\n") "" user.send
                if not (Set.isEmpty user.addr) then 
                    for i in user.addr do 
                        i <! RECV (Tweet("", m, "S"))
                sender <! ACK "SUCCESS!"
            else    sender <! ACK "NO LOGIN!"
        | GetMen (uid) ->
            printfn $"[{sender}] sent {msg}"
            if Map.containsKey uid users then
                let user = users.[uid]
                let m = List.fold (fun k (i: Tweet) -> k + $"\n{i.m}\n") "" user.mend
                if not (Set.isEmpty user.addr) then 
                    for i in user.addr do 
                        i <! RECV (Tweet("", m, "M"))
                sender <! ACK "SUCCESS!"
            else    sender <! ACK "NO LOGIN!"
        | _ -> ()
        return! loop()    
    }
    loop()

let nw = 2*Environment.ProcessorCount //No of workers
let hpool = 
    [0..nw] 
    |> Seq.map (fun f -> f, spawn system ("hasher" + string f) hasher) 
    |> Map.ofSeq        
let wpool = 
    ['a'..'z'] 
    |> Seq.map (fun f -> f, spawn system ("worker" + string f) (worker f)) 
    |> Map.ofSeq    

Map.iter (fun k v -> v <! INIT (wpool, hpool)) wpool
system.WhenTerminated.Wait()
printfn "END"