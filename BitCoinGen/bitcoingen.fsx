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
                hostname = ""192.168.0.105""
                port = 8989
            }
        }"

let system = System.create "server" config
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

type Msg = 
    | Start of int
    | Compute of string*int
    | MXZ of int
    | Fin of int*string
    | RemoteWork of list<IActorRef>

let worker (mailbox:Actor<_>) =
    let sha256Hash = SHA256Managed.Create()
    let rec loop () = actor {
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
            
            mailbox.Context.Parent <! Fin (mxz, str)
        | _ -> ()
        return! loop()    
    }
    loop()

let boss =  spawn system "boss" <| fun mailbox ->
    let mutable n = 0
    let mutable nw = 4
    let mutable fw = 0
    let mutable prefix = "sbang"
    let rec loop() = actor {
        let sender = mailbox.Sender()
        let! msg = mailbox.Receive()
        match msg with
        | Start k -> 
            n <- k
            for i in 1..nw do 
                spawn mailbox.Context ("worker" + string i) worker <! Compute(prefix, n)
                prefix <- cp prefix
        | RemoteWork pool ->
            for w in pool do
                w <! Compute(prefix, n)
                prefix <- cp prefix
        | Fin (k, str) -> 
            printfn "%s" str
            if prefix.Length > 8 then
                fw <- fw + 1
                if fw = nw then mailbox.Context.System.Terminate() |> ignore         
            else
                // if k >= n then n <- k + 1
                sender <! Compute(prefix, n)


        | _ -> ()

        return! loop()
    }
    loop()

boss <! Start 4
system.WhenTerminated.Wait()
printfn "END"