#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System.Security.Cryptography
open System.Text
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open Akka.Serialization

let config =
    ConfigurationFactory.ParseString
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
                    hostname = 192.168.0.100
                    port = 8989
                }
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
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Compute (prefix, n) ->
            // [32..127] |> List.map fun x -> sha256Hash.ComputeHash(prefix + char x)
            let mutable mxz = 0
            let mutable str = ""
            printfn "YES"
            for i in [32..126] do
                let ps = prefix + string (char i)
                let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(ps)) |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
                let mutable k = 0
                while en.[k] = '0' do
                    k <- k + 1
                if k >= n then 
                    if k > mxz then mxz <- k
                    str <- str + sprintf "REM%s\t%s\n" ps en
            
            sender <! Fin (mxz, str)
        | _ -> ()
        return! loop()    
    }
    loop()

let client =  spawn system "client" <| fun mailbox ->
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Start -> 
            let pool = [for i in 1..8 do yield spawn mailbox.Context ("worker" + string i) worker]
            let server = select ("akka.tcp://server@192.168.0.105:8989/user/boss") system
            server <! RemoteWork pool
        | _ -> ()

        return! loop()
    }
    loop()

client <! Start
system.WhenTerminated.Wait()
printfn "END"