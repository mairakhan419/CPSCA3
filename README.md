# CPSC 565 Assignment 3
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
- At the beginning of each simulation, the worker ants are spread around all over the terrain. The first generation starts with randomised genomes. 
- how much energy each worker ant begins with
- How they make decisions to move around the terrain
- how they eat mulch, plus how they deal with another ant on the same mulch block
- what happens when they are on an acid block
- how does it avoid acid blocks, the probability 
- how it climbs up block, and how it cant climb up block heights higher than 2
- how they dig the grass blocks, the probability
- the probability of turning 
- what happens after they consume one piece of mulch
- how do they donate their energy to the queen ant
- how do they die 

### Behaviour of The Queen Ant in Simulation
the queen builds the nest based on 1/3 of her energy. she starts with ... energy points and then she will be losing ... energy points every time she adds a nest block to the nest. 

### Evolution Algorithm Applied to New Generation of Worker Ants 
Every generation is to 

### Instructions For How To Run Simulation 
1. Download the codebase on this repository
2. Run the simulation on Unity editor
3. Zoom into the middle of the terrain, this is where the nest is getting built.

### Controls For Navigation

### Discussion
i ran the simulation up to generation ... and notices some interesting results. 
