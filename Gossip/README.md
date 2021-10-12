# Project 2

Project 2 for the course COP5615 Distributed Operating System Principles.

### Authors
Shivam Bang (UFID 77461830)
Rajat Bishnoi (UFID 29429756)

## Running the program
Run the program by opening up your command prompt and change the directory to the unzipped project. 

### Synatx
To run the program:
```sh
dotnet fsi gossip.fsx numNodes topology algorithm
```

### Output
The program outputs the number of nodes (since it can be different than input for 3D and IMP3D), completed node percent, s/w ratio, sum of nodes and the time taken.
A file named "TEST-[algorithm]-[topology]-[numNodes].txt" is also created which contains the s/w ratio for all the nodes.

## Working
The program works for all topologies and algorithms.

## Highest network
Under 2 mins, largest networks:
|       | Gossip | Push-Sum |
|-------|--------|----------|
| FULL  | 1728   | 2744     |
| LINE  | 15625  | 216      |
| 3D    | 15625  | 10648    |
| IMP3D | 15625  | 15625    |