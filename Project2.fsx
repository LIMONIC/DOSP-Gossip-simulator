#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote" 

open System
open System.Collections.Generic
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 8777
                    hostname = localhost
                }
            }
        }")

(*************************Input*************************)
let args : string array = fsi.CommandLineArgs |> Array.tail
let mutable nodeNum = args.[0] |> int
let mutable topology = args.[1] |> string
let algorithm = args.[2] |> string
(*******************************************************)

let url = "akka.tcp://Project2@localhost:8777/user/"
let system = ActorSystem.Create("Project2", configuration)
let mutable adjTable = Array2D.zeroCreate 0 0
let mutable converged = false
// actorStatus.[i] : if actor i finished its job.
let mutable actorStatus = Array.zeroCreate nodeNum

let getRandom next list =
    list |> Seq.sortBy (fun _ -> next())
    

let gossip (name : string) = 
    spawn system name
    <| fun mailbox ->
        let rec loop count =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? string -> 
                        let newcount = count + 1
                        let mutable neighbors = []
                        let idx = int name
                        // get valid neighbors nodes 
                        [0..nodeNum - 1] 
                        |> List.iter(fun i -> (if adjTable.[idx, i] = 1 && idx <> i && actorStatus.[i] = 0 then neighbors <- List.append neighbors [i]))
                        
                        // all neighbors are terminated
                        if neighbors.Length = 0 then
                            actorStatus.[idx] <- 1
                            system.ActorSelection(url + "boss") <! name
                        else
                            let nextNode = neighbors |> getRandom (fun _ -> Random().Next()) |> Seq.head |> string
                            system.ActorSelection(url + nextNode) <! message

                        if count = 10 then
                            system.ActorSelection(url + "boss") <! name

                        return! loop newcount
                | _ ->  failwith "unknown message"
            } 
        loop 0


let pushSum (name : string) = 
    spawn system name
    <| fun mailbox ->
        let rec loop (prevS:float) (prevW:float) (count:int) (status:bool)=
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                let splitLine = (fun (line : string) -> Seq.toArray (line.Split ','))

                match box message with
                | :? String ->
                        let pair = splitLine message
                        let mutable newS = prevS + float pair.[0]
                        let mutable newW = prevW + float pair.[1]
                        let mutable newStatus = status
                        let diff = prevS / prevW - newS / newW
                        let idx = int name
                        let mutable newCount = 0
                        if diff < 1.0 ** (-10.0) then
                            newCount <- count + 1
                        // if false, can not be consecutive 3 times -> rest count to 0
                        // get valid neighbors nodes 
                        let mutable neighbors = []
                        [0..nodeNum - 1] 
                        |> List.iter(fun i -> (if adjTable.[idx, i] = 1 && idx <> i && actorStatus.[i] = 0 then neighbors <- List.append neighbors [i]))

                        let nextName = neighbors |> getRandom (fun _ -> Random().Next()) |> Seq.head |> string
                        let nextNode = system.ActorSelection(url + nextName)
                        newS <- newS / 2.0
                        newW <- newW / 2.0
                        nextNode <? string newS + "," + string newW |> ignore
                                
                        if newCount = 3 && not status then
                            system.ActorSelection(url + "boss") <! name
                            newStatus <- true
                        return! loop newS newW newCount newStatus
                | _ ->  failwith "unknown message"
            } 
        loop (float (int name)) 1.0 0 false// initial: s = idx of node, w = 1

let boss =
    spawn system "boss"
    <| fun mailbox ->
        let rec loop count =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? string -> 
                        printfn $"[INFO] Converged Actor: {message}"
                        let newCount = count + 1
                        if newCount = nodeNum then
                            printfn "[INFO] All actors converged."
                            converged <- true
                        return! loop newCount
                | _ ->  failwith "unknown message"
            } 
        loop 0

let get3DGrid nodeNum =
    let input = float(nodeNum)
    let root = Math.Round(Math.Pow(input, 1.0/3.0))
    let checkCubeRoot = root * root * root = input

    match (checkCubeRoot) with
    | true -> 
        let edgeInt = int(root)
        (Array3D.init edgeInt edgeInt edgeInt (fun x y z -> ((x*edgeInt*edgeInt  + y * edgeInt + z)))) 
    | _ -> failwith "[ERROR] The node number should be cube of an positive integer." 

let buildAdjTabByBFS (grid3d: _ [,,]) =
    let totNum = (Array3D.length1 grid3d) * (Array3D.length1 grid3d) * (Array3D.length1 grid3d) 
    // totNum |> printfn "%A"
    let mutable adjTab = Array2D.zeroCreate totNum totNum
    let mutable visited = Set.empty
    let queue = new Queue<int>()

    let getcoordinate (id:int) len = 
        let x = id / (len * len)
        let xMod = id % (len * len)
        let y = xMod / len
        let z = xMod % len
        (x, y, z)
    let getId (x,y,z) len =
        x * len * len + y * len + z
    let getNeighbor id len set = 
        let (x, y, z) = getcoordinate id len
        let neighbors = new List<int>()
        [[1;0;0]; [-1;0;0]; [0;1;0]; [0;-1;0]; [0;0;1]; [0;0;-1]] 
        |> List.iter(fun i ->
            let (newX, newY, newZ) = (x + i.[0], y + i.[1], z + i.[2])
            let newId = (getId (newX, newY, newZ) len)
            if ((newX >= 0 && newX < len) &&
                (newY >= 0 && newY < len) &&
                (newZ >= 0 && newZ < len) &&
                not (Set.contains newId set))
                then (neighbors.Add(newId))
        )
        // printfn "neighbors: %A" neighbors
        neighbors
    // init 
    queue.Enqueue(grid3d.[0,0,0])
    visited <- visited.Add(grid3d.[0,0,0])
    // bfs
    while (queue.Count <> 0) do 
        [0..(queue.Count - 1)] |> List.iter(fun i -> 
            let now = queue.Dequeue()
            // printfn $"[DEBUG] current id: {now}\n{visited}"
            (getNeighbor now (Array3D.length1 grid3d) visited) 
            |> Seq.iter (fun j -> 
                adjTab.[now,j] <- 1
                adjTab.[j,now] <- 1
                queue.Enqueue(j)
                visited <- visited.Add(j)
            )
        )
    adjTab

let getLineGrid nodeNum =
    Array2D.init nodeNum nodeNum (fun x y -> (if (y - x = 1||x - y = 1) then 1 else 0))
    
let addRandomNode arr = 
    let len = Array2D.length1 arr
    // let mutable set = Set.empty
    for r = 0 to len - 1 do
        let mutable ran = Random().Next(0, len - 1)
        while arr.[r,ran] = 1 do
            ran <- Random().Next(0, len - 1)
        if arr.[r,ran] = 0 then
            printfn $"[DEBUG] a = {r}, b = {ran}"
            // set <- set.Add(r).Add(ran)
            arr.[r,ran] <- 1 
            arr.[ran,r] <- 1
    arr 

let getAdjTab topology nodeNums =
    match topology with
    | "full" -> Array2D.create nodeNums nodeNums 1
    | "line" -> getLineGrid nodeNums
    | "3D" -> (get3DGrid nodeNums) |> buildAdjTabByBFS 
    | "imp3D" -> (get3DGrid nodeNums) |> buildAdjTabByBFS |> addRandomNode
    | _ -> failwith "[ERROR] wrong topology"

let createNode nodeNum algo = 
    for i = 0 to nodeNum - 1 do
        let name = string i
        match algo with
        | "gossip" ->
            gossip name |> ignore
        | "push-sum" ->
            pushSum name |> ignore
        | _ -> failwith "[ERROR] wrong algorithm"
    printfn $"[INFO] {nodeNum} of actors are generated!"


let main ()= 
    adjTable <- getAdjTab topology nodeNum
    createNode nodeNum algorithm
    let timer = System.Diagnostics.Stopwatch.StartNew()
    let startActor = system.ActorSelection(url + "0")
    match algorithm with
        | "gossip" -> 
            startActor <! "Some message."
        | "push-sum" ->
            startActor <! "1.0,1.0"
        | _ -> failwith "[ERROR] wrong algorithm"
    while not converged do
        0 |> ignore
    // printfn "%A" terminated
    timer.Stop()
    printfn "[INFO] Time consumed: %f ms" timer.Elapsed.TotalMilliseconds
main()
