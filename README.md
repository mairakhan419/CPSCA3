s# CPSC 565 Assignment 3
By: Maira Khan

### What This Simulation Does

This Unity simulation explores the evolutionary development of an ant colony, and how the ant colony learns over successive generations how to build the biggest ant nest. The simulation will start with an initial population consisting of 50 worker ants and a single queen ant, representing the first generation.

Each generation interacts with the environment for a fixed duration of one minute. During this period, a fitness value will be applied to the worker ants, based on their interaction with the environment. The fitness values influence the genetic composition of the subsequent generation.

At the end of each generation's period, an evolutionary algorithm will be applied to produce a new population of 50 new worker ants, based on the previous generation's gene pool. Over time, it is expected that the ant colony should become better at surviving and building larger nests.

### Components That Are Seen on The GUI Screen

1. The terrain it self:
    - **Light green mulch blocks:** these blocks will be consumed by the worker ants for energy points.
    - **Dark green grass blocks** these blocks can be dug up by the worker ants.
    - **Purple acidic blocks:** these blocks are dangerous for the ants, and worker ants should try their best to avoid them.
    - **Red nest block:** this block represents a block that is part of the ant colony's nest.
2. The two different types of ants:
    - **Worker Ants:** the bright pink ants that are moving around the terrain.
    - **Queen Ants:** the bigger red ant that has her head in the bottom nest block for building the inside of the nest.
3. UI Dashboard: the top UI dashboard contains the following data for each generation.
    - **Time Left:** the one minute timer countdown will be shown here.
    - **Queen Health:** the queen's health is displayed here and gets updated everytime she either receives energy from one of the worker ants or uses 1/3 of her total energy to produce another nest block.
    - **Blocks Placed:** this will display the total number of nest blocks that were used to build the ants nest.
    - **Generation:** this shows the current generation that the simulation is running for
    - **Ants Alive:** this shows the number of worker ants that are stil alive in the current generation, and as the ants die off due to their health reaching 0, this number will update to show the number of remaining worker ants.

### Behaviour of The Worker Ants in Simulation

-   At the beginning of each simulation, the worker ants are spread around all over the terrain. The first generation starts with randomised genomes. And each worker ant begins with a health number of 100.
-   Worker ants move around the terrain by jumping tile-to-tile. Each ant has its own built-in movement timing, so some ants move more frequently while others move less often. This timing is inherited from the ants genome. Thus ants with shorter waiting times between jumps move more, while ants with longer waiting times move more slowly across the terrain.
-   When a worker is standing on top of a mulch block, it consumes that mulch block and this gets converted into 50 health points. This mulch block is then removed from the terrain. If multiple ants attempt to collect the same mulch block, only the first ant that got to the mulch block is allowed to claim it.
-   After consuming one block of mulch, the worker ant begins carrying it back to the queen. While returning after mulch consumption, the ant stops searching for additional mulch to consume.
-   When a worker ant reaches the queen with food, it transfers 10 health points to the queen. This donated energy contributes to the queens health. After donating energy, the worker ant returns to exploring the terrain to consume more mulch.
-   Worker ants also have a genetically determined tendency to avoid acid blocks as well. Some ants are more sensitive to the acid blocks and more thus likely to avoid it. The ant will scan the surrounding terrain and prefer moving in directions that lead away from the acid. The stronger their acid avoidance trait rate, the more likely they are to steer away from these tiles.
-   Worker ants can jump over to tiles with a height limit of less than 3 blocks from the block they are standing on. If the height difference is greater than 2 blocks, then the ant will turn away to another direction.
-   Worker ants are capable of digging grass blocks beneath them. This behaviour is also genetically determined. Each ant has a digging probability. When they are standing on top of a grass block, the ant has a chance to that grass block. Ants with higher digging probabilites will cut through more of the terrain.
-   Worker ants do not always move in straight lines, and have a genetically determined probability of turning. When making a decision to move or not, the ant may randomly rotate left or right instead of contiuing down the path it is on. Ants with higher probability of turning will explore more erratically, while the ones with lower turning probability will move in straighter paths.
-   Lastly, worker ants gradually lose energy over time as they move and operate in the environment. For every second in the simulation running, the worker ants will lose one health point. In addition, standing on acid blocks doubles the decrease every second. If an ants health level reaches zero, then it dies and is removed from the GUI screen.

### Behaviour of The Queen Ant in Simulation

the queen builds the nest based on 1/3 of her energy. she starts with ... energy points and then she will be losing ... energy points every time she adds a nest block to the nest.

### Evolution Algorithm Applied to New Generation of Worker Ants

What each ants genomes are built up of:

how the fitness values are assigned:

how the parents are chosen:

evolution algorithm applied:

### Instructions For How To Run Simulation

1. Download the codebase on this repository
2. Run the simulation on Unity editor
3. Zoom into the middle of the terrain, this is where the nest is getting built.

### Controls For Navigation

### Discussion

i ran the simulation up to generation ... and notices some interesting results.
