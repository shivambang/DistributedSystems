# Project 1

Project 1 for the course COP5615 Distributed Operating System Principles.

In this project we use the actor model to mine bitcoins with specific number of leading zeroes.

### Authors
Shivam Bang (UFID 77461830)
Rajat Bishnoi (UFID 29429756)

## Running the program
Run the program by opening up your command prompt and change the directory to the unzipped project. 
Change the hostname in config variable at line 32 to the IP address of your system (for server-client setup) in server.fsx and client.fsx.


### Synatx
To run the main program:
```sh
dotnet fsi bitcoingen.fsx <k>
```
For Server-Client
To run the server:
```sh
dotnet fsi server.fsx <k>
```

To run the client:
```sh
dotnet fsi client.fsx <server''s IP>
```

> Please note: Change the hostname in config variable (line 16) to the system's IP Address in both the server and client programs before run

## Project questions

### Size of the work unit
We ran the program with various work units and determined that it works best with each worker getting around 10^4 subproblems (100*95 to be exact) and deploying workers equal to twice the number of cores available.

Results on a quad-core machine
|     S.No    |     No of workers    |         Work units      |      Real Time    |       CPU Time     |     Ratio    |
|:-----------:|:--------------------:|:-----------------------:|:-----------------:|:------------------:|:------------:|
|       1     |           4          |          100*95         |     70 seconds    |     203 seconds    |      2.90    |
|       2     |           8          |          10*95          |     85 seconds    |     340 seconds    |      4.00    |
|       3     |           8          |          100*95         |     67 seconds    |     285 seconds    |      4.25    |
|       4     |           8          |          1000*95        |     70 seconds    |     288 seconds    |      4.11    |
|       5     |           16         |          100*95         |     76 seconds    |     292 seconds    |      3.84    |


### The result of running for input 4
The result of running the program *k* = 4:
Real: 00:00:00.000, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0
4
sbang!EQ        00007c1381a1c013bd6bbafe64a4c1fcf7c73ae33d7d96a89e46aa86975cd82b
sbang#mx        00001ad339bc5fb9cca23437a6d1cbfafe100c9935396e83db7b20809d5af4fd
sbang,AO        0000b230bd46f41cce4352d828e7a55830fe44fd9d102bf8fa7cbf611b413c0e
sbangR%i        00000e8f45b2e80d3b80e404e6874dbe2a9ffb5642434beb4eab36a3d04a23fe
sbang*(lc       000000ffdc12ac8ca9cc6ce113be35785e4e72102c4ba53e6c5ac52d923008d9
Max 6 prefix zeroes found!
END
Real: 00:01:07.309, CPU: 00:04:45.546, GC gen0: 37843, gen1: 9, gen2: 0

Once a bitcoin with certain zeroes has been found, the boss sends next work unit to find bitcoin with increased zero count. Therefore, all possible bitcoins are not printed by the bitcoingen.fsx program. This is done for a cleaner output.

### The running time.
Real: 67 sec, CPU: 285 sec
By running the program on 8 workers at 100*95 work units we get above observation in which ratio (CPU time/Real Time) is 4.25 which is close to 4 (no of processors)

### The coin with most zeroes
We found coins with max 6 zeroes on a single system and 7 zeroes on the ditributed setup.
sbang*(lc       000000ffdc12ac8ca9cc6ce113be35785e4e72102c4ba53e6c5ac52d923008d9
sbang'!fJu      00000008bc3bce636ef3677e3d35f5473dddcc0046f5e0041defee49d8b0c3ad    (line 49 in server-client.txt file)

### Largest no of working machines
We ran the setup on three machines but it is capable of running on more machines. Output for the server and two clients is in server-client.txt file. We print REMOTE and REMOTE2 infront of client calculated work for easy distinction.