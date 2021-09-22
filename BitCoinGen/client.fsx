#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"

open System.Security.Cryptography
open System.Text
open Akka.FSharp
open Akka.Actor
open Akka.Configuration

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = ""192.168.0.100""
                port = 8989
            }
        }"

let system = System.create "client" config

type Msg = 
    | Start
    | Compute of string*int
    | MXZ of int
    | Fin of int*string
    | RemoteWork of list<IActorRef>

let worker (mailbox:Actor<_>) =
    let sha256Hash = SHA256Managed.Create()
    let rec loop () = actor {
        let sender = mailbox.Sender()
        let! msg = mailbox.Receive()
        match msg with
        | Compute (prefix, n) ->
            // [32..127] |> List.map fun x -> sha256Hash.ComputeHash(prefix + char x)
            let mutable mxz = 0
            let mutable str = ""
            for i in [32..126] do
                let ps = prefix + string (char i)
                let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(ps)) |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
                let mutable k = 0
                while en.[k] = '0' do
                    k <- k + 1
                if k >= n then 
                    if k > mxz then mxz <- k
                    str <- str + sprintf "%s\t%s\n" ps en
            
            sender <! Fin (mxz, str)
        | _ -> ()
        return! loop()    
    }
    loop()

let client =  spawn system "client" <| fun mailbox ->
    let pool = [for i in 1..2 do yield spawn mailbox.Context ("worker" + string i) worker]
    let rec loop() = actor {
        let sender = mailbox.Sender()
        let! msg = mailbox.Receive()
        match msg with
        | Start -> 
            let server = system.ActorSelection("akka.tcp://boss@192.168.9.105:8989/user/boss")
            server <! RemoteWork pool
        | _ -> ()

        return! loop()
    }
    loop()

client <! Start
system.WhenTerminated.Wait()
printfn "END"