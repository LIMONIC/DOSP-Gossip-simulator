# Gossip Simulator

# I. Introduction

This project employed Actor Mode with Akka.NET to implement an information propagation model for Gossip and Push-sum algorithm in networks with 4 different topologies. 

# II. Usage

Use following commands to run the F# script.

```latex
dotnet fsi Project2.fsx <number of node:int> <topology:string> <algorithm:string>
```

- Number of node should be an integer that can get cube root.
- Topology includes: `"full"` `"line"` `"3D"` `"imp3D"`
- Algorithm includes: `"gossip"` `"push-sum"`

# III. Implementation

The project implements a distributed system composed of many actors through a specific topology. At the beginning, only one actor has the gossip message. It then transmits that message to its neighboring nodes through the topological network until all actors in the entire system have received same message.

The implementation consists of three parts: network topology, distributed message delivery system, and the communication algorithm. The main program includes building the topology, generating actor nodes, starting the timer, sending a message to an actor and reporting the elapsed time when the convergence condition is reached.

## Topology

Adjacent matrixes are used to represent the connection of actors. 

### Full Network

Each actor is connect to all other actors.

### Line Network

Actors are arranged in a line. Each actor has only 2 neighbors (one left and one right, except for the first and last actor).

![Untitled](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/4ee9bb90-3d5a-4aee-9cd3-209bbc7c6ee1/Untitled.png)

Fig 1. Adjacent matrix of full network with 8 actors. 1 indicates the node i and j are connected.

![Untitled](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/a192f255-1df4-44e7-838d-c44a681e9ec4/Untitled.png)

Fig 2. Adjacent matrix of line network with 8 actors. 0 indicates the node i and j are not connected.

### 3D Grid Network

Actors form a cube, and each actor can only talk to its neighbor. A BFS algorithm is implemented to convert the connection of actors from 3-dimention to 2-dimention matrix.  

### Imperfect 3D Grid Network

A random connection is added to each actor based on 3D Grid Network

![Untitled](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/d94610d3-e8cb-4ff2-97c2-c31361d1f208/Untitled.png)

Fig 3. Adjacent matrix of 3D grid network with 27 actors.

![Untitled](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/108104b3-1af3-4969-ba41-efa50c3a8eaf/Untitled.png)

Fig 4. Adjacent matrix of imperfect 3D grid network with 27 actors. A random connection is introduced to each actor.

## Gossip Algorithm

In the gossip algorithm, one of the actors is given a specific message during initialization. The actor will share the received message with its neighbors. The actor's connection to its neighbors is defined by a specified topology. When an actor receives the same message more than a specified number of times, e.g. 10 times, it considers that the message has been sufficiently propagated and stops execution.

Therefore, we will keep a count for each actor and update the count when a new message is received from a neighbor. The algorithm will be executed in the following way:

1. Increment the count by 1 when a message is received.
2. Find the neighboring actors and send messages to them.
3. When 10 messages are received, report completion to the boss actor and terminate.

For a large-scale distributed system, it is possible for some actors that waiting for message but all of its neighbors are finished their tasks and become offline. In such a situation, it prevent the entire system from reaching the end point. To solve problem, a state array was maintained for determining if a actor can go offline. An actor' status will be updated to offline only if the actor and all of its neighbors are finished their task. 

## Push-sum Algorithm

In the Push-Sum algorithm, each actor is given an initial (s, w) during initialization. The actor communicates with its neighbors by dividing its (s, w) value into two equal parts and sending only one of them to the target node. The target node receives the (s, w) value and adds it to its own value. This is a process of averaging s and w. Therefore the ratio of s to w will convergence to a certain value. We consider that all nodes have completed the communication when the difference between the previous s/w and the current s/w is less than a threshold for three consecutive times.

To avoid repeatedly sending convergence completion messages to the boss actor, we define a Boolean variable for the actor to perform recursive parsing. This variable is set to false by default. each time we update the current state based on the last state. If the actor terminates, we only need to resolve to true for the next recursion. We send a message to the boss only if count = 3 && actorStatus = false.

# IV. Result

## Test Environment

> OS: Windows 10 Home 21H1 19043.1266
Processor	Intel(R) Core(TM) i9-9980HK CPU @ 2.40GHz
Installed RAM	32.0 GB (31.7 GB usable)
System type	64-bit operating system, x64-based processor
> 

## Gossip Algorithm

![Picture1.png](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/9344583b-dfd8-478c-9f9b-e4940056e823/Picture1.png)

Fig 5. Time to receive message for nodes in network with different topologies 

![Picture2.png](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/26a8aef8-1e00-4567-82b0-b2fc4ec694e6/Picture2.png)

Fig 6. Time to receive message for nodes in larger network with different topologies 

Networks with linear topology take the longest time to communicate. Networks with 3-D and imperfect 3-D topologies exhibit similar results, with a significantly faster time than linear topology networks when node number larger than 200. In contrast, the fully connected network took more time after 200 nodes than the time required by the linear topology network. This may be due to the fact that there are too many connections in the network and the time spent on each connection overrides the performance benefits of adding more connections.

Regarding the results of 3D and imperfect 3D, the 3D topology without random processes has better performance as the number of nodes increases. One possible reason is that a huge network size diminishes the message propagation speed brought by adding extra connections. Also, since the project implemented a O(n) algorithm to generating random connections, increasing the number of nodes necessarily leads to an increase in total time.

## Push-Sum Algorithm

![Picture4.png](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/bfdee52e-ca56-4105-95f7-04a55cb8a940/Picture4.png)

Fig 7. Time to receive message for nodes in network with different topologies 

![Picture6.png](https://s3-us-west-2.amazonaws.com/secure.notion-static.com/f55ab724-7af5-429c-9792-e9d864db4a26/Picture6.png)

Fig 8. Time to receive message for nodes in larger network with different topologies.

Push-Sum has a faster information propagation speed than the Gossip algorithm. In Push-Sum algorithm, there is a significant performance gap between the linear structure and other topologies. This is due to the bottleneck in information propagation caused by the overly simple connections. In order to converge the final result, information needs to be passed repeatedly through the network, increasing the time required to complete the task.

A fully connected topology works best in this experiment. This is because all nodes are involved in the assignment of weights W in each round of communication, making the overall convergence faster .The Push-Sum algorithm is not determined by the number of receiving message like the Gossip algorithm. Its speed depends on the number of nodes involved in the averaging process, i.e. the number of connections. This is also the reason why imperfect 3D topologies with more connections are faster than perfect 3D structures.