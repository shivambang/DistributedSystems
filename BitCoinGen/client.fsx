#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System.Security.Cryptography
open System.Text
open System
open Akka.FSharp
open Akka.Actor
open Akka.Remote
open Akka.Configuration
open Akka.Serialization

let args = fsi.CommandLineArgs

//Set hostname to current PC IP address
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
                    hostname = "+args.[1]+
                    "port = 9898
                }
            }
        }"

let system = System.create "client" config

type Msg = 
    | Start
    | Compute of string*int
    | Fin of int*string
    | RemoteWork of list<IActorRef>
    | Stop

let cp (p:string) = 
    let mutable i = p.Length - 1
    let pc = p.ToCharArray()
    while i > 4 && int pc.[i] + 1 = 128 do
        pc.[i] <- ' '
        i <- i - 1
    if i = 4 then System.String.Concat(pc) + " "
    else
        pc.[i] <- char (int pc.[i] + 1)
        System.String.Concat(pc)

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
            let mutable prefix = prefix
            for _ in [1..100] do
                for i in [32..126] do
                    let ps = prefix + string (char i)
                    let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(ps)) |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
                    let mutable k = 0
                    while en.[k] = '0' do
                        k <- k + 1
                    if k >= n then 
                        if k > mxz then mxz <- k
                        str <- str + sprintf "%s\t%s\tREMOTE\n" ps en
                prefix <- cp prefix
            
            sender <! Fin (mxz, str)
        | Stop -> mailbox.Context.System.Terminate() |> ignore
        | _ -> ()
        return! loop()    
    }
    loop()


let nw = 2*Environment.ProcessorCount //No of workers
let pool = [for i in 1..nw do yield spawn system ("worker" + string i) worker]
let server = select ("akka.tcp://server@"+args.[2]+":8989/user/boss") system
server <! RemoteWork pool
system.WhenTerminated.Wait()
printfn "END"