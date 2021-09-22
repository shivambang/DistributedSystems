#r "nuget: Akka.FSharp" 

open System.Security.Cryptography
open System.Text
open Akka.FSharp
open Akka.Actor

let system = System.create "system" (Configuration.defaultConfig())
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
    | Compute of string
    | MXZ of int
    | Fin

let worker nz (mailbox:Actor<_>) =
    let n = nz
    let sha256Hash = SHA256Managed.Create()
    let rec loop () = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Compute (prefix) ->
            // [32..127] |> List.map fun x -> sha256Hash.ComputeHash(prefix + char x)
            let mutable mxz = 0
            for i in [32..127] do
                let str = prefix + string (char i)
                let en = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(str)) |> Array.map(fun x -> x.ToString("x2")) |> System.String.Concat
                let mutable k = 0
                while en.[k] = '0' do
                    k <- k + 1
                if k >= n && k >= mxz then 
                    mxz <- k
                    printf "%s\t%s\n" str en
            
            mailbox.Context.Parent <! MXZ (mxz)
        | Fin -> mailbox.Context.Parent <! Fin
        | _ -> ()
        return! loop()    
    }
    loop()

let boss =  spawn system "boss" <| fun mailbox ->
    let mutable fw = 0
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Start k -> 
            let mutable prefix = "sbang"
            let pool = [for i in 1..4 do yield spawn mailbox.Context ("worker" + string i) (worker k)]
            while prefix.Length < 8 do
                for w in pool do
                    w <! Compute(prefix)
                    prefix <- cp prefix
            for w in pool do w <! Fin
        | Fin -> 
            fw <- fw + 1
            if fw = 4 then mailbox.Context.System.Terminate() |> ignore         

        | _ -> ()

        return! loop()
    }
    loop()

boss <! Start 4
system.WhenTerminated.Wait()
printfn "END"