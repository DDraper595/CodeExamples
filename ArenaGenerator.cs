using Blockborn.BuildTools;
using Blockborn.Networking;
using Holdara.Environment;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tessera;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ArenaGenerator : LevelGenerator
{
    public Vector3 navMeshSize = new Vector3(256, 75, 256);
    public Vector3 navMeshCenter = new Vector3(0, 25, 0);
    public Vector3 genPos = new Vector3(0, 0, 0);

    public override IEnumerator Generate()
    {
        SetupMapLayout();

        while (!AddressablesManager.Finished)
            yield return null;

        yield return LoadAsset("Assets/Skybox.prefab", (result) =>
        {
            // Rotate around to bring spire closer to play area (saves creating another skybox)
            Transform centre = result.transform.Find("DysonSphereCentre.001");

            if (centre)
                centre.localRotation = Quaternion.Euler(0f, 88.7f, -4.5f);
        });

        yield return LoadGenerator(customGenerator);

        if (!string.IsNullOrEmpty(mapPrefab))
            yield return LoadMapPrefab();

        //SetupTiles();

        yield return LoadEnvironment();

        yield return SetupArena();

        //yield return LoadMapNav();

        AdjustSpawnPoints();

        AddRegionDetails();

        yield return SetStartingLight();
    }

    protected IEnumerator SetupArena()
    {
        TesseraGenerator pathGen = generator.GetPathGen();

        if (pathGen != null)
        {
            generator.GetPathGen().size = mapLayout.pathSize;
            yield return generator.GeneratePaths(mapLayout.pathSeed);
        }

        generator.landGen.size = mapLayout.size;
        yield return generator.GenerateLandStructure(mapLayout.seed);

        // randomise tiles
        objRandomiser.Seed = mapLayout.tileSeed;
        objRandomiser.Random();

        // set maps elemental alignment
        environmentControllers.elementalSplatMapCreator.Seed = mapLayout.splatSeed;

        environmentControllers.elementalSplatMapCreator.elementalTypeA.elementalType = (ElementalType)mapLayout.elemental.x;
        environmentControllers.elementalSplatMapCreator.elementalTypeB.elementalType = (ElementalType)mapLayout.elemental.y;

        // randomise locator volumes
        assetRandomiser.Seed = mapLayout.assetSeed;
        List<TileData> tiles = generator.GetComponentsInChildren<TileData>().ToList();
        tiles.Sort();

        generator.transform.position = genPos;

        // Move background to lowest tile
        if (background)
        {
            float scale = Mathf.Max(mapLayout.size.x, mapLayout.size.z);
            background.transform.localScale = new Vector3(scale / 4f, mapLayout.size.y / 4f, scale / 4f);

            float currentDist = float.MaxValue;
            if (objRandomiser.outputRoot != null)
            {
                for (int i = 0; i < objRandomiser.outputRoot.childCount; i++)
                {
                    Transform child = objRandomiser.outputRoot.GetChild(i);
                    List<MeshCollider> meshColliders = new();
                    meshColliders.AddRange(child.GetComponents<MeshCollider>());
                    meshColliders.AddRange(child.GetComponentsInChildren<MeshCollider>());

                    for (int m = 0; m < meshColliders.Count; m++)
                    {
                        MeshCollider meshCollider = meshColliders[m];
                        if (meshCollider.gameObject.transform.position.y + meshCollider.bounds.min.y < currentDist)
                        {
                            currentDist = meshCollider.gameObject.transform.position.y + meshCollider.bounds.min.y;
                        }
                    }
                }
            }
            background.transform.position = new Vector3(0, currentDist, 0);
        }

        // setup nav mesh
        yield return LoadMapNav();

        AdjustSpawnPoints();
    }

    protected override IEnumerator LoadMapPrefab()
    {
        while (!AddressablesManager.Finished)
            yield return null;

        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(mapPrefab);

        while (handle.Status == AsyncOperationStatus.None)
        {
            yield return null;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded)
            Debug.LogError($"failed to load {mapPrefab}");
        else
        {
            mapPrefabObj = InstantiateObject(handle.Result);
        }
    }

    protected override void AdjustSpawnPoints()
    {
        GameObject[] loadedSpawns = GameObject.FindGameObjectsWithTag("PlayerSpawn");

        if (loadedSpawns.Length > 0)
        {
            for (int i = 0; i < loadedSpawns.Length; i++)
            {
                GameObject spawnPoint = loadedSpawns[i];
                spawnPoint.AddComponent<SpawnPoint>();
            }
        }

        List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        Runner.SimulationUnityScene.GetComponents(spawnPoints);

        // makes sure all spawns are at ground level
        PhysicsScene physics = Runner.GetPhysicsScene();

        foreach (SpawnPoint spawnPoint in spawnPoints)
        {
            if (physics.Raycast(new Vector3(spawnPoint.transform.position.x, spawnPoint.transform.position.y + 3, spawnPoint.transform.position.z)
            {

            }, Vector3.down, out RaycastHit hit))
            {
                spawnPoint.transform.position = hit.point;
            }
        }
    }

    protected override IEnumerator LoadMapNav()
    {
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("MapNavArena");

        while (handle.Status == AsyncOperationStatus.None)
        {
            yield return null;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded)
            Debug.LogError($"failed to load {"MapNav"}");
        else
        {
            GameObject instance = InstantiateObject(handle.Result);

            // adjust to cater for tile overlap
            surface = instance.GetComponent<NavMeshSurface>();
            surface.size = navMeshSize;
            surface.center = navMeshCenter;

            surface.BuildNavMesh();
        }
    }
}
