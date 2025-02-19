using Blockborn.Networking;
using Fusion;
using Holdara.Environment;
using UnityEngine;

[System.Serializable]
public struct TrackedResource : INetworkStruct
{
    public int Id;

    [Networked, Capacity(20)]
    public string Action { get => default; set { } }

    [Networked, Capacity(20)]
    public string Resource { get => default; set { } }

    public int Stored;
}

[System.Serializable]
public struct TrackedResources : INetworkStruct
{
    [Networked, Capacity(64)]
    public NetworkLinkedList<TrackedResource> Resources => default;

    public static TrackedResources Defaults
    {
        get
        {
            var result = new TrackedResources();
            return result;
        }
    }
}

public class ResourceManager : ContextBehaviour
{
    [Networked]
    [UnitySerializeField]
    public ref TrackedResources TrackedResources => ref MakeRef<TrackedResources>();

    public int size;

    public void ModifyResourceStored(TrackedResource trackedResource, int index)
    {
        TrackedResources.Resources.Set(index, trackedResource);
    }

    public override void Spawned()
    {
        StartCoroutine(WaitToLink());
    }

    System.Collections.IEnumerator WaitToLink()
    {
        // wait for the service to be ready
        ResourceService resourceService = Context.GetService<ResourceService>();

        while (resourceService == null)
        {
            resourceService = Context.GetService<ResourceService>();
            yield return new WaitForSeconds(1f);
        }

        resourceService.resourceManagers.Add(this);
    }

    public TrackedResource GetResource(int uniqueID, string gatherID)
    {
        for (int i = 0; i < TrackedResources.Resources.Count; i++)
        {
            if (TrackedResources.Resources[i].Id == uniqueID && TrackedResources.Resources[i].Action == gatherID)
            {
                return TrackedResources.Resources[i];
            }
        }

        return new TrackedResource { Stored = 0 };
    }

    public TrackedResource GetResource(int uniqueID, string gatherID, out int index)
    {
        for (int i = 0; i < TrackedResources.Resources.Count; i++)
        {
            if (TrackedResources.Resources[i].Id == uniqueID && TrackedResources.Resources[i].Action == gatherID)
            {
                index = i;
                return TrackedResources.Resources[i];
            }
        }

        index = -1;
        return new TrackedResource { Stored = 0 };
    }

    public void AddToManager(DetailAsset detailAsset)
    {
        // add a resource point to be tracked
        if (detailAsset.resources.Count == 0)
            return;

        TrackedResources copy = TrackedResources;

        for (int i = 0; i < detailAsset.resources.Count; i++)
        {
            TrackedResource trackedSource = new()
            {
                Id = detailAsset.uniqueID,
                Action = detailAsset.resources[i].gatherType,
                Resource = detailAsset.resources[i].resourceID,
                Stored = detailAsset.resources[i].amount
            };

            copy.Resources.Add(trackedSource);
        }

        TrackedResources = copy;
        size = TrackedResources.Resources.Count;
    }
}

