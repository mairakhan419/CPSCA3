using Antymology.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.Generic;
using SysRandom = System.Random;

namespace Antymology.Terrain
{
    public class WorldManager : Singleton<WorldManager>
    {
       private static readonly SysRandom rng = new SysRandom();
        public int WorldSizeX => Blocks.GetLength(0);
        public int WorldSizeY => Blocks.GetLength(1);
        public int WorldSizeZ => Blocks.GetLength(2);
        // public Transform QueenTransform { get; private set; }
        public QueenAntScript Queen { get; private set; }


        #region Fields
        private readonly HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> claimedMulch = new HashSet<Vector3Int>();

        /// <summary>
        /// The prefab containing the ant.
        /// </summary>
        public GameObject antPrefab;
        /// <summary>
        /// The prefab for creating ants.
        /// </summary>
        // public GameObject workerAntSpawnerPrefab;


        /// <summary>
        /// The prefab containing the ant.
        /// </summary>
        public GameObject queenAntPrefab;
        public Transform QueenTransform { get; private set; }

        /// <summary>
        /// The material used for eech block.
        /// </summary>
        public Material blockMaterial;

        /// <summary>
        /// The raw data of the underlying world structure.
        /// </summary>
        private AbstractBlock[,,] Blocks;

        /// <summary>
        /// Reference to the geometry data of the chunks.
        /// </summary>
        private Chunk[,,] Chunks;

        /// <summary>
        /// Random number generator.
        /// </summary>
        private System.Random RNG;

        /// <summary>
        /// Random number generator.
        /// </summary>
        private SimplexNoise SimplexNoise;
        private readonly HashSet<Vector3Int> removedMulchTiles = new HashSet<Vector3Int>();
        private List<Vector3Int> destroyedGrassBlocks = new List<Vector3Int>();
    private readonly List<Vector3Int> initialMulchTiles = new();

        #endregion

        #region Initialization

        /// <summary>
        /// Awake is called before any start method is called.
        /// </summary>
        void Awake()
        {
            // Generate new random number generator
            RNG = new System.Random(ConfigurationManager.Instance.Seed);

            // Generate new simplex noise generator
            SimplexNoise = new SimplexNoise(ConfigurationManager.Instance.Seed);

            // Initialize a new 3D array of blocks with size of the number of chunks times the size of each chunk
            Blocks = new AbstractBlock[
                ConfigurationManager.Instance.World_Diameter * ConfigurationManager.Instance.Chunk_Diameter,
                ConfigurationManager.Instance.World_Height * ConfigurationManager.Instance.Chunk_Diameter,
                ConfigurationManager.Instance.World_Diameter * ConfigurationManager.Instance.Chunk_Diameter];

            // Initialize a new 3D array of chunks with size of the number of chunks
            Chunks = new Chunk[
                ConfigurationManager.Instance.World_Diameter,
                ConfigurationManager.Instance.World_Height,
                ConfigurationManager.Instance.World_Diameter];
        }

        /// <summary>
        /// Called after every awake has been called.
        /// </summary>
        private void Start()
        {
            GenerateData();
            GenerateChunks();
            // CacheInitialMulchTiles(0, WorldSizeY - 1);
            Camera.main.transform.position = new Vector3(0 / 2, Blocks.GetLength(1), 0);
            Camera.main.transform.LookAt(new Vector3(Blocks.GetLength(0), 0, Blocks.GetLength(2)));

            GenerateAnts();
        }
        /// <summary>
        /// Returns the geometric center of the world.
        /// </summary>
        private Vector3 GetWorldCenterOnSurface()
        {
            int x = Blocks.GetLength(0) / 2;
            int z = Blocks.GetLength(2) / 2;

            int y = Blocks.GetLength(1) - 1;
            while (y > 0 && GetBlock(x, y, z) is AirBlock)
                y--;

            return new Vector3(x, y, z);
        }
        private Vector3 PlaceOnGround(GameObject prefab, Vector3 approxXZ)
        {
            Vector3 rayStart = new Vector3(approxXZ.x, WorldSizeY + 10f, approxXZ.z);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10000f))
            {
                float lift = 0f;
                Collider c = prefab.GetComponentInChildren<Collider>();
                if (c != null)
                    lift = c.bounds.extents.y;

                return hit.point + Vector3.up * (lift + 0.02f);
            }

            return approxXZ;
        }



        /// <summary>
        /// TO BE IMPLEMENTED BY YOU
        /// </summary>
        private void GenerateAnts()
        {
            if (queenAntPrefab == null)
            {
                Debug.LogError("WorldManager: queenAntPrefab not assigned");
                return;
            }

            // if (workerAntSpawnerPrefab == null)
            // {
            //     Debug.LogError("WorldManager: workerAntSpawnerPrefab not assigned.");
            //     return;
            // }

            Vector3 centerXZ = new Vector3(WorldSizeX / 2f, 0f, WorldSizeZ / 2f);

            Vector3 queenPos = PlaceOnGround(queenAntPrefab, centerXZ);
            Debug.Log("REAL QUEEN POS: " + queenPos);
            GameObject queenGO = Instantiate(queenAntPrefab, queenPos, Quaternion.identity);
            QueenTransform = queenGO.transform;
            // Queen = queenGO.GetComponent<QueenAnt>();



            // Vector3 spawnerPos = PlaceOnGround(workerAntSpawnerPrefab, centerXZ);
            // Instantiate(workerAntSpawnerPrefab, spawnerPos, Quaternion.identity);
        }


        #endregion

        #region Methods

        /// <summary>
        /// Retrieves an abstract block type at the desired world coordinates.
        /// </summary>
        public AbstractBlock GetBlock(int WorldXCoordinate, int WorldYCoordinate, int WorldZCoordinate)
        {
            if
            (
                WorldXCoordinate < 0 ||
                WorldYCoordinate < 0 ||
                WorldZCoordinate < 0 ||
                WorldXCoordinate >= Blocks.GetLength(0) ||
                WorldYCoordinate >= Blocks.GetLength(1) ||
                WorldZCoordinate >= Blocks.GetLength(2)
            )
                return new AirBlock();

            return Blocks[WorldXCoordinate, WorldYCoordinate, WorldZCoordinate];
        }

        /// <summary>
        /// Retrieves an abstract block type at the desired local coordinates within a chunk.
        /// </summary>
        public AbstractBlock GetBlock(
            int ChunkXCoordinate, int ChunkYCoordinate, int ChunkZCoordinate,
            int LocalXCoordinate, int LocalYCoordinate, int LocalZCoordinate)
        {
            if
            (
                LocalXCoordinate < 0 ||
                LocalYCoordinate < 0 ||
                LocalZCoordinate < 0 ||
                LocalXCoordinate >= Blocks.GetLength(0) ||
                LocalYCoordinate >= Blocks.GetLength(1) ||
                LocalZCoordinate >= Blocks.GetLength(2) ||
                ChunkXCoordinate < 0 ||
                ChunkYCoordinate < 0 ||
                ChunkZCoordinate < 0 ||
                ChunkXCoordinate >= Blocks.GetLength(0) ||
                ChunkYCoordinate >= Blocks.GetLength(1) ||
                ChunkZCoordinate >= Blocks.GetLength(2)
            )
                return new AirBlock();

            return Blocks
            [
                ChunkXCoordinate * LocalXCoordinate,
                ChunkYCoordinate * LocalYCoordinate,
                ChunkZCoordinate * LocalZCoordinate
            ];
        }

        /// <summary>
        /// sets an abstract block type at the desired world coordinates.
        /// </summary>
        public void SetBlock(int WorldXCoordinate, int WorldYCoordinate, int WorldZCoordinate, AbstractBlock toSet)
        {
            if
            (
                WorldXCoordinate < 0 ||
                WorldYCoordinate < 0 ||
                WorldZCoordinate < 0 ||
                WorldXCoordinate > Blocks.GetLength(0) ||
                WorldYCoordinate > Blocks.GetLength(1) ||
                WorldZCoordinate > Blocks.GetLength(2)
            )
            {
                Debug.Log("Attempted to set a block which didn't exist");
                return;
            }

            Blocks[WorldXCoordinate, WorldYCoordinate, WorldZCoordinate] = toSet;

            SetChunkContainingBlockToUpdate
            (
                WorldXCoordinate,
                WorldYCoordinate,
                WorldZCoordinate
            );
        }

        /// <summary>
        /// sets an abstract block type at the desired local coordinates within a chunk.
        /// </summary>
        public void SetBlock(
            int ChunkXCoordinate, int ChunkYCoordinate, int ChunkZCoordinate,
            int LocalXCoordinate, int LocalYCoordinate, int LocalZCoordinate,
            AbstractBlock toSet)
        {
            if
            (
                LocalXCoordinate < 0 ||
                LocalYCoordinate < 0 ||
                LocalZCoordinate < 0 ||
                LocalXCoordinate > Blocks.GetLength(0) ||
                LocalYCoordinate > Blocks.GetLength(1) ||
                LocalZCoordinate > Blocks.GetLength(2) ||
                ChunkXCoordinate < 0 ||
                ChunkYCoordinate < 0 ||
                ChunkZCoordinate < 0 ||
                ChunkXCoordinate > Blocks.GetLength(0) ||
                ChunkYCoordinate > Blocks.GetLength(1) ||
                ChunkZCoordinate > Blocks.GetLength(2)
            )
            {
                Debug.Log("Attempted to set a block which didn't exist");
                return;
            }
            Blocks
            [
                ChunkXCoordinate * LocalXCoordinate,
                ChunkYCoordinate * LocalYCoordinate,
                ChunkZCoordinate * LocalZCoordinate
            ] = toSet;

            SetChunkContainingBlockToUpdate
            (
                ChunkXCoordinate * LocalXCoordinate,
                ChunkYCoordinate * LocalYCoordinate,
                ChunkZCoordinate * LocalZCoordinate
            );
        }

        #endregion

        #region Helpers

        #region Blocks

        /// <summary>
        /// Is responsible for generating the base, acid, and spheres.
        /// </summary>
        private void GenerateData()
        {
            GeneratePreliminaryWorld();
            GenerateAcidicRegions();
            GenerateSphericalContainers();
        }

        /// <summary>
        /// Generates the preliminary world data based on perlin noise.
        /// </summary>
        private void GeneratePreliminaryWorld()
        {
            Blocks[82,0,96] = new AcidicBlock();
            for (int x = 0; x < Blocks.GetLength(0); x++)
                for (int z = 0; z < Blocks.GetLength(2); z++)
                {
                    /**
                     * These numbers have been fine-tuned and tweaked through trial and error.
                     * Altering these numbers may produce weird looking worlds.
                     **/
                    int stoneCeiling = SimplexNoise.GetPerlinNoise(x, 0, z, 10, 3, 1.2) +
                                       SimplexNoise.GetPerlinNoise(x, 300, z, 20, 4, 0) +
                                       10;
                    int grassHeight = SimplexNoise.GetPerlinNoise(x, 100, z, 30, 10, 0);
                    int foodHeight = SimplexNoise.GetPerlinNoise(x, 200, z, 20, 5, 1.5);

                    for (int y = 0; y < Blocks.GetLength(1); y++)
                    {
                        if (y <= stoneCeiling)
                        {
                            Blocks[x, y, z] = new StoneBlock();
                        }
                        else if (y <= stoneCeiling + grassHeight)
                        {
                            Blocks[x, y, z] = new GrassBlock();
                        }
                        else if (y <= stoneCeiling + grassHeight + foodHeight)
                            {
                            // double mulchChance = 0.0;
                                double mulchChance = 1;


                                // 1. Get the block directly underneath the current position
                            AbstractBlock blockUnderneath = Blocks[x, y - 1, z];

                                // 2. Only attempt to spawn mulch if the block below is NOT Air and NOT Acid
                                // (You can refine this to specifically "is GrassBlock" if you prefer)
                                bool isGroundBelow = blockUnderneath != null && !(blockUnderneath is AirBlock) && !(blockUnderneath is AcidicBlock);

                                if (isGroundBelow && rng.NextDouble() < mulchChance)
                                {
                                    Blocks[x, y, z] = new MulchBlock();
                                }
                                else
                                {
                                    Blocks[x, y, z] = new AirBlock();
                                }
                            }

                        else
                            {
                                Blocks[x, y, z] = new AirBlock();
                            }
                        if
                        (
                            x == 0 ||
                            x >= Blocks.GetLength(0) - 1 ||
                            z == 0 ||
                            z >= Blocks.GetLength(2) - 1 ||
                            y == 0
                        )
                            Blocks[x, y, z] = new ContainerBlock();
                    }
                }
        }

        /// <summary>
        /// Alters a pre-generated map so that acid blocks exist.
        /// </summary>
        private void GenerateAcidicRegions()
        {
            for (int i = 0; i < ConfigurationManager.Instance.Number_Of_Acidic_Regions; i++)
            {
                int xCoord = RNG.Next(0, Blocks.GetLength(0));
                int zCoord = RNG.Next(0, Blocks.GetLength(2));
                int yCoord = -1;
                for (int j = Blocks.GetLength(1) - 1; j >= 0; j--)
                {
                    if (Blocks[xCoord, j, zCoord] as AirBlock == null)
                    {
                        yCoord = j;
                        break;
                    }
                }

                //Generate a sphere around this point overriding non-air blocks
                for (int HX = xCoord - ConfigurationManager.Instance.Acidic_Region_Radius; HX < xCoord + ConfigurationManager.Instance.Acidic_Region_Radius; HX++)
                {
                    for (int HZ = zCoord - ConfigurationManager.Instance.Acidic_Region_Radius; HZ < zCoord + ConfigurationManager.Instance.Acidic_Region_Radius; HZ++)
                    {
                        for (int HY = yCoord - ConfigurationManager.Instance.Acidic_Region_Radius; HY < yCoord + ConfigurationManager.Instance.Acidic_Region_Radius; HY++)
                        {
                            float xSquare = (xCoord - HX) * (xCoord - HX);
                            float ySquare = (yCoord - HY) * (yCoord - HY);
                            float zSquare = (zCoord - HZ) * (zCoord - HZ);
                            float Dist = Mathf.Sqrt(xSquare + ySquare + zSquare);
                            if (Dist <= ConfigurationManager.Instance.Acidic_Region_Radius)
                            {
                                int CX, CY, CZ;
                                CX = Mathf.Clamp(HX, 1, Blocks.GetLength(0) - 2);
                                CZ = Mathf.Clamp(HZ, 1, Blocks.GetLength(2) - 2);
                                CY = Mathf.Clamp(HY, 1, Blocks.GetLength(1) - 2);
                                if (Blocks[CX, CY, CZ] as AirBlock != null)
                                    Blocks[CX, CY, CZ] = new AcidicBlock();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Alters a pre-generated map so that obstructions exist within the map.
        /// </summary>
        private void GenerateSphericalContainers()
        {

            //Generate hazards
            for (int i = 0; i < ConfigurationManager.Instance.Number_Of_Conatiner_Spheres; i++)
            {
                int xCoord = RNG.Next(0, Blocks.GetLength(0));
                int zCoord = RNG.Next(0, Blocks.GetLength(2));
                int yCoord = RNG.Next(0, Blocks.GetLength(1));


                //Generate a sphere around this point overriding non-air blocks
                for (int HX = xCoord - ConfigurationManager.Instance.Conatiner_Sphere_Radius; HX < xCoord + ConfigurationManager.Instance.Conatiner_Sphere_Radius; HX++)
                {
                    for (int HZ = zCoord - ConfigurationManager.Instance.Conatiner_Sphere_Radius; HZ < zCoord + ConfigurationManager.Instance.Conatiner_Sphere_Radius; HZ++)
                    {
                        for (int HY = yCoord - ConfigurationManager.Instance.Conatiner_Sphere_Radius; HY < yCoord + ConfigurationManager.Instance.Conatiner_Sphere_Radius; HY++)
                        {
                            float xSquare = (xCoord - HX) * (xCoord - HX);
                            float ySquare = (yCoord - HY) * (yCoord - HY);
                            float zSquare = (zCoord - HZ) * (zCoord - HZ);
                            float Dist = Mathf.Sqrt(xSquare + ySquare + zSquare);
                            if (Dist <= ConfigurationManager.Instance.Conatiner_Sphere_Radius)
                            {
                                int CX, CY, CZ;
                                CX = Mathf.Clamp(HX, 1, Blocks.GetLength(0) - 2);
                                CZ = Mathf.Clamp(HZ, 1, Blocks.GetLength(2) - 2);
                                CY = Mathf.Clamp(HY, 1, Blocks.GetLength(1) - 2);
                                Blocks[CX, CY, CZ] = new ContainerBlock();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Given a world coordinate, tells the chunk holding that coordinate to update.
        /// Also tells all 4 neighbours to update (as an altered block might exist on the
        /// edge of a chunk).
        /// </summary>
        /// <param name="worldXCoordinate"></param>
        /// <param name="worldYCoordinate"></param>
        /// <param name="worldZCoordinate"></param>
        private void SetChunkContainingBlockToUpdate(int worldXCoordinate, int worldYCoordinate, int worldZCoordinate)
        {
            //Updates the chunk containing this block
            int updateX = Mathf.FloorToInt(worldXCoordinate / ConfigurationManager.Instance.Chunk_Diameter);
            int updateY = Mathf.FloorToInt(worldYCoordinate / ConfigurationManager.Instance.Chunk_Diameter);
            int updateZ = Mathf.FloorToInt(worldZCoordinate / ConfigurationManager.Instance.Chunk_Diameter);
            Chunks[updateX, updateY, updateZ].updateNeeded = true;

            // Also flag all 6 neighbours for update as well
            if (updateX - 1 >= 0)
                Chunks[updateX - 1, updateY, updateZ].updateNeeded = true;
            if (updateX + 1 < Chunks.GetLength(0))
                Chunks[updateX + 1, updateY, updateZ].updateNeeded = true;

            if (updateY - 1 >= 0)
                Chunks[updateX, updateY - 1, updateZ].updateNeeded = true;
            if (updateY + 1 < Chunks.GetLength(1))
                Chunks[updateX, updateY + 1, updateZ].updateNeeded = true;

            if (updateZ - 1 >= 0)
                Chunks[updateX, updateY, updateZ - 1].updateNeeded = true;
            if (updateX + 1 < Chunks.GetLength(2))
                Chunks[updateX, updateY, updateZ + 1].updateNeeded = true;
        }

        #endregion

        #region Chunks

        /// <summary>
        /// Takes the world data and generates the associated chunk objects.
        /// </summary>
        private void GenerateChunks()
        {
            GameObject chunkObg = new GameObject("Chunks");

            for (int x = 0; x < Chunks.GetLength(0); x++)
                for (int z = 0; z < Chunks.GetLength(2); z++)
                    for (int y = 0; y < Chunks.GetLength(1); y++)
                    {
                        GameObject temp = new GameObject();
                        temp.name = $"Chunk_{x}_{y}_{z}";
                        temp.layer = LayerMask.NameToLayer("Ground");
                        temp.transform.parent = chunkObg.transform;
                        temp.transform.position = new Vector3
                        (
                            x * ConfigurationManager.Instance.Chunk_Diameter - 0.5f,
                            y * ConfigurationManager.Instance.Chunk_Diameter + 0.5f,
                            z * ConfigurationManager.Instance.Chunk_Diameter - 0.5f
                        );
                        Chunk chunkScript = temp.AddComponent<Chunk>();
                        chunkScript.x = x * ConfigurationManager.Instance.Chunk_Diameter;
                        chunkScript.y = y * ConfigurationManager.Instance.Chunk_Diameter;
                        chunkScript.z = z * ConfigurationManager.Instance.Chunk_Diameter;
                        chunkScript.Init(blockMaterial);
                        chunkScript.GenerateMesh();
                        Chunks[x, y, z] = chunkScript;
                    }
        }

        public bool TryReserveTile(Vector3Int tile)
{
    // If already occupied, fail
    if (occupied.Contains(tile)) return false;
    occupied.Add(tile);
    return true;
}

public void ReleaseTile(Vector3Int tile)
{
    occupied.Remove(tile);
}

public bool TryClaimMulch(Vector3Int tile)
{
    // Only allow one ant to claim this tile
    if (claimedMulch.Contains(tile)) return false;
    claimedMulch.Add(tile);
    return true;
}

public void CacheInitialMulchTiles(int yMin, int yMax)
{
    initialMulchTiles.Clear();

    yMin = Mathf.Clamp(yMin, 0, WorldSizeY - 1);
    yMax = Mathf.Clamp(yMax, 0, WorldSizeY - 1);

    for (int x = 0; x < WorldSizeX; x++)
    for (int z = 0; z < WorldSizeZ; z++)
    for (int y = yMin; y <= yMax; y++)
    {
        var b = GetBlock(x, y, z);
        if (b is MulchBlock)
            initialMulchTiles.Add(new Vector3Int(x, y, z));
    }

    Debug.Log($"Cached {initialMulchTiles.Count} mulch tiles.");
}

    public void RestoreMulchTiles()
    {
        Debug.Log("RESTORING MULCH TILES: " + initialMulchTiles.Count);
        foreach (var t in initialMulchTiles)
                SetBlock(t.x, t.y, t.z, new MulchBlock());
    }


        public void RecordRemovedMulch(Vector3Int t)
        {

            removedMulchTiles.Add(t);
        }

        // Method to remove a grass block and track its position
        public void RemoveGrassBlock(Vector3Int position)
    {
                var block = GetBlock(position.x, position.y, position.z);
                if (block is GrassBlock)
                {
                    destroyedGrassBlocks.Add(position);
                    SetBlock(position.x, position.y, position.z, new AirBlock()); // NOT null
                }
    }
    public void RemoveMulchBlock(Vector3Int position)
    {
        var block = GetBlock(position.x, position.y, position.z);
        if (block is MulchBlock)
        {
            RecordRemovedMulch(position);
            SetBlock(position.x, position.y, position.z, new AirBlock());
            ReleaseMulchClaim(position); // optional safety
        }
    }



        public void RegenerateGrassBlocks()
        {
            foreach (var position in destroyedGrassBlocks)
            {
                SetBlock(position.x, position.y, position.z, new GrassBlock()); // Restore the grass block
                Debug.Log($"Grass block regenerated at {position}.");
            }

            destroyedGrassBlocks.Clear(); // Clear the list after regeneration
        }


        // Call this at generation restart
        public void RestoreRemovedMulch()
        {
            Debug.Log("RESTORING MULCH TILES: " + removedMulchTiles.Count);

            // also clear claims so the new generation can target them again
            claimedMulch.Clear();

            foreach (var t in removedMulchTiles)
                SetBlock(t.x, t.y, t.z, new MulchBlock());

            removedMulchTiles.Clear();
        }
public void ReleaseMulchClaim(Vector3Int tile)
        {
            claimedMulch.Remove(tile);
        }

        #endregion

        #endregion
    }
}
