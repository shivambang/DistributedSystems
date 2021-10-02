#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.Serialization.Hyperion"

open System.Security.Cryptography
open System.Diagnostics
open System.Text
open System
open Akka.FSharp
open Akka.Actor
open Akka.Remote
open Akka.Configuration
open Akka.Serialization

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
                    hostname = 192.168.0.103
                    port = 8989
                }
            }
        }"

let system = System.create "server" config

let cp (p:string) = 
    let mutable i = p.Length - 1
    let pc = p.ToCharArray()
    while i > 4 && int pc.[i] + 1 > 126 do
        pc.[i] <- ' '
        i <- i - 1
    if i = 4 then System.String.Concat(pc) + " "
    else
        pc.[i] <- char (int pc.[i] + 1)
        System.String.Concat(pc)

type Msg = 
    | Start of int
    | Compute of string*int
    | Fin of int*string
    | RemoteWork of list<IActorRef>
    | Stop

let worker (mailbox:Actor<_>) =
    let sha256Hash = SHA256Managed.Create()
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Compute (prefix, n) ->
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
                        str <- str + sprintf "%s\t%s\n" ps en
                prefix <- cp prefix
            
            sender <! Fin (mxz, str)
        | _ -> ()
        return! loop()    
    }
    loop()

let boss =  spawn system "boss" <| fun mailbox ->
    let timer = Stopwatch.StartNew()
    let mutable n = 0
    let mutable nw = 2*Environment.ProcessorCount //No of workers
    let mutable fw = 0
    let mutable rpool = []
    let mutable prefix = "sbang"
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | Start k -> 
            n <- k
            for i in 1..nw do 
                spawn mailbox.Context ("worker" + string i) worker <! Compute(prefix, n)
                for _ in [1..100] do prefix <- cp prefix
        | Fin (k, str) -> 
            // if k >= n then n <- k + 1   //Comment out this line to print all possible bitcoins
            if str <> "" then printf "%s" str
            if prefix.Length > 9 || timer.ElapsedMilliseconds > int64 300000 then  
                fw <- fw + 1
                if fw = nw then 
                    // printfn "Max %d prefix zeroes found!" (n - 1)   
                    for w in rpool do
                        w <! Stop
                    mailbox.Context.System.Terminate() |> ignore         
            else
                sender <! Compute(prefix, n)
                for _ in [1..100] do prefix <- cp prefix

        | RemoteWork pool ->
            rpool <- rpool @ [pool.[0]]
            for w in pool do
                nw <- nw + 1
                w <! Compute(prefix, n)
                for _ in [1..100] do prefix <- cp prefix

        | _ -> ()

        return! loop()
    }
    loop()

let args = fsi.CommandLineArgs
printfn "%s" args.[1]
boss <! Start (int args.[1])
system.WhenTerminated.Wait()
printfn "END"