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
            log-dead-letters = off
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
let spool = 
    ['a'..'z'] 
    |> Seq.map (fun f -> f, select ("akka.tcp://server@"+args.[3]+":"+args.[4]+"/user/worker" + string f) system)
    |> Map.ofSeq    

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

let cli = spawn system "cli" <| fun mailbox ->
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Start k ->
            let inp = Console.ReadLine()
            if k = "sys" then
                let ref = select "user/worker" system
                ref <! Start inp
            else    sender <! Start inp
        | _ -> ()
        return! loop()    
    }
    loop()

let worker (mailbox:Actor<_>) =
    let mutable uid = ""
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Start m ->
            let inp = m.Split ';'
            match inp.[0] with
            | "reg" -> 
                let id = inp.[1].ToLower()
                if Regex.IsMatch(id, @"[^a-z]") then
                    printfn "INVALID! username can only contain letters"
                    cli <! Start ""
                else
                    uid <- inp.[1].ToLower()
                    spool.[id.[0]] <! Register(id, "NI")
            | "login" -> 
                if Regex.IsMatch(uid, @"[^a-z]") then
                    printfn "INVALID! username can only contain letters"
                    cli <! Start ""
                else
                    uid <- inp.[1].ToLower()
                    spool.[uid.[0]] <! Login(inp.[1], "NI")
            | "logout" -> 
                spool.[uid.[0]] <! Logout(uid)
                uid <- ""
            | "tweet" -> spool.[uid.[0]] <! Tweeter(uid, inp.[1])
            | "retweet" -> 
                if Map.containsKey inp.[1].[0] spool then spool.[inp.[1].[0]] <! RTW(uid, inp.[1])
                else
                    printfn "INVALID tweet id!"
                    cli <! Start ""
            | "subs" ->
                if Map.containsKey inp.[1].[0] spool then spool.[inp.[1].[0]] <! Subs(uid, inp.[1])
                else
                    printfn "INVALID!"
                    cli <! Start ""
            | "gethash" -> spool.[uid.[0]] <! GetHash(uid, inp.[1])
            | "getsub" -> spool.[uid.[0]] <! GetSub(uid)
            | "getmen" -> spool.[uid.[0]] <! GetMen(uid)
            | "exit" -> mailbox.Context.System.Terminate() |> ignore
            | _ -> 
                printfn "NO SUCH CMD!"
                cli <! Start ""
        | RECV t ->
            printfn "%s" t.m
        | ACK m -> 
            printfn $"{m}"
            cli <! Start ""
        | _ -> ()
        return! loop()    
    }
    loop()

printfn "Commands:\n\treg;username -- Register username\n\tlogin;username -- Login username\n\tlogout\n\ttweet;message -- Tweet message\n\tretweet;tid -- Retweet Tweet with id == tid\n\tsubs;uid -- Subscribe to user with id == uid\n\tgethash;hashtag -- Get all tweets that contain hashtag\n\tgetsub -- Get all subscribed tweets\n\tgetmen -- Get all tweets that mention user\n"
printfn "\nYou will recieve tweets in the form:\n[tweeter username][Tweet/Mention] [tweet id]\n[tweet message]\n\nIn retweet, tid refers to this tweet id\n\n"
let w = spawn system "worker" worker
cli <! Start "sys"
system.WhenTerminated.Wait()
printfn "END"