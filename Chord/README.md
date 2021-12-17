# Chord

Project 3 for the course COP5615 Distributed Operating System Principles.

In this project we use the actor model to mimick the chord protocol.

### Authors
Shivam Bang (UFID 77461830)

## Running the program

### Syntax
To run the main program:
```sh
dotnet fsi chord.fsx numNodes numRequests
```
To run the bonus program:
```sh
dotnet fsi fchord.fsx numNodes numRequests nodeFailP
```
>nodeFailP is the probability of failure of a node
>dotnet fsi fchord.fsx 1000 25 50 would mean there are 1000 nodes. Each is required to send 25 requests and has 50% probability of failure.

### Largest no of working nodes
The programs run successfully for 10000 nodes
```
dotnet fsi chord.fsx 10000 25
Average Hops = 5.97
```