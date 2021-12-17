# Project 4

Project 4 for the course COP5615 Distributed Operating System Principles.

### Authors
Shivam Bang (UFID 77461830)

## Running the program
Run the program by opening up your command prompt and change the directory to the unzipped project. 

### Synatx
To run the engine:
```sh
dotnet fsi engine.fsx server-ip server-port
```
To run the client:
```sh
dotnet fsi client.fsx client-ip client-port server-ip server-port
```
To run the client simulator:
```sh
dotnet fsi client-sim.fsx client-ip client-port server-ip server-port numUsers
```

### Output
The client-sim program prints the time taken to subscribe, number of requests of each kind, and total time taken to fulfill these requests.