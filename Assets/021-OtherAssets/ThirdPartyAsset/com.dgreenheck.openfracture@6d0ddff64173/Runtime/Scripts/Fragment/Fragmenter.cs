using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class Fragmenter
{
    /// <summary>
    /// Generates the mesh fragments based on the provided options. The generated fragment objects are
    /// stored as children of `fragmentParent`
    /// </summary>
    /// <param name="sourceObject">The source object to fragment. This object must have a MeshFilter, a RigidBody and a Collider.</param>
    /// <param name="options">Options for the fragmenter</param>
    /// <param name="fragmentTemplate">The template GameObject that each fragment will clone</param>
    /// <param name="parent">The parent transform for the fragment objects</param>
    /// <param name="saveToDisk">If true, the generated fragment meshes will be saved to disk so they can be re-used in prefabs.</param>
    /// <param name="saveFolderPath">The save location for the fragments.</param>
    /// <returns></returns>
    public static void Fracture(GameObject sourceObject,
                                FractureOptions options,
                                GameObject fragmentTemplate,
                                Transform parent,
                                bool saveToDisk = false,
                                string saveFolderPath = "")
    {
        // Define our source mesh data for the fracturing
        FragmentData sourceMesh = new FragmentData(sourceObject.GetComponent<MeshFilter>().sharedMesh);
 
        // We begin by fragmenting the source mesh, then process each fragment in a FIFO queue
        // until we achieve the target fragment count.
        var fragments = new Queue<FragmentData>();
        fragments.Enqueue(sourceMesh);

        // Subdivide the mesh into multiple fragments until we reach the fragment limit
        FragmentData topSlice, bottomSlice;
        while (fragments.Count < options.fragmentCount)
        {
            FragmentData meshData = fragments.Dequeue();
            meshData.CalculateBounds();

            // Select an arbitrary fracture plane normal
            Vector3 normal = new Vector3(
                options.xAxis ? Random.Range(-1f, 1f) : 0f,
                options.yAxis ? Random.Range(-1f, 1f) : 0f,
                options.zAxis ? Random.Range(-1f, 1f) : 0f);

            // Slice and dice!
            MeshSlicer.Slice(meshData,
                             normal,
                             meshData.Bounds.center,
                             options.textureScale,
                             options.textureOffset,
                             out topSlice,
                             out bottomSlice);

            fragments.Enqueue(topSlice);
            fragments.Enqueue(bottomSlice);
        }

        int i = 0;
        foreach(FragmentData meshData in fragments)
        {
            CreateFragment(meshData, 
                           sourceObject,
                           fragmentTemplate, 
                           parent,
                           saveToDisk,
                           saveFolderPath,
                           options.detectFloatingFragments,
                           ref i);
        }
    }

    /// <summary>
    /// Asynchronously generates the mesh fragments based on the provided options. The generated fragment objects are
    /// stored as children of `fragmentParent`
    /// </summary>
    /// <param name="sourceObject">The source object to fragment. This object must have a MeshFilter, a RigidBody and a Collider.</param>
    /// <param name="options">Options for the fragmenter</param>
    /// <param name="fragmentTemplate">The template GameObject that each fragment will clone</param>
    /// <param name="parent">The parent transform for the fragment objects</param>
    /// <returns></returns>
    public static IEnumerator FractureAsync(GameObject sourceObject,
                                            FractureOptions options,
                                            GameObject fragmentTemplate,
                                            Transform parent,
                                            Action onCompletion)
    {
        // Define our source mesh data for the fracturing
        FragmentData sourceMesh = new FragmentData(sourceObject.GetComponent<MeshFilter>().sharedMesh);
 
        // We begin by fragmenting the source mesh, then process each fragment in a FIFO queue
        // until we achieve the target fragment count.
        var fragments = new Queue<FragmentData>();
        fragments.Enqueue(sourceMesh);

        // Subdivide the mesh into multiple fragments until we reach the fragment limit
        FragmentData topSlice, bottomSlice;
        while (fragments.Count < options.fragmentCount)
        {
            FragmentData meshData = fragments.Dequeue();
            meshData.CalculateBounds();

            // Select an arbitrary fracture plane normal
            Vector3 normal = new Vector3(
                options.xAxis ? Random.Range(-1f, 1f) : 0f,
                options.yAxis ? Random.Range(-1f, 1f) : 0f,
                options.zAxis ? Random.Range(-1f, 1f) : 0f);

            // Slice and dice!
            MeshSlicer.Slice(meshData,
                             normal,
                             meshData.Bounds.center,
                             options.textureScale,
                             options.textureOffset,
                             out topSlice,
                             out bottomSlice);

            // Perform next slice on the next frame
            yield return null;

            fragments.Enqueue(topSlice);
            fragments.Enqueue(bottomSlice);
        }

        int i = 0;
        foreach(FragmentData meshData in fragments)
        {
            CreateFragment(meshData, 
                           sourceObject,
                           fragmentTemplate, 
                           parent,
                           false,
                           "",
                           options.detectFloatingFragments,
                           ref i);
        }

        onCompletion?.Invoke();
    }

    /// <summary>
    /// Generates the mesh fragments based on the provided options. The generated fragment objects are
    /// stored as children of `fragmentParent`
    /// </summary>
    /// <param name="sourceObject">The source object to slice. This object must have a MeshFilter, a RigidBody and a Collider.</param>
    /// <param name="sliceNormal">The normal of the cut plane in the local frame of sourceObject.</param>
    /// <param name="sliceOrigin">The origin of the cut plane in the local frame of sourceObject.</param>
    /// <param name="options">Options for the slicer</param>
    /// <param name="fragmentTemplate">The template GameObject that each slice will clone</param>
    /// <param name="parent">The parent transform for the fragment objects</param>
    /// <returns></returns>
    public static void Slice(GameObject sourceObject,
                             Vector3 sliceNormal,
                             Vector3 sliceOrigin,
                             SliceOptions options,
                             GameObject fragmentTemplate,
                             Transform parent)
    {
        // Define our source mesh data for the fracturing
        FragmentData sourceMesh = new FragmentData(sourceObject.GetComponent<MeshFilter>().sharedMesh);
        // Subdivide the mesh into multiple fragments until we reach the fragment limit
        FragmentData topSlice, bottomSlice;

        // Slice and dice!
        MeshSlicer.Slice(sourceMesh,
                         sliceNormal,
                         sliceOrigin,
                         options.textureScale,
                         options.textureOffset,
                         out topSlice,
                         out bottomSlice);

        int i = 0;
        CreateFragment(topSlice,
                       sourceObject,
                       fragmentTemplate,
                       parent,
                       false,
                       "",
                       options.detectFloatingFragments,
                       ref i);

        CreateFragment(bottomSlice,
                       sourceObject,
                       fragmentTemplate,
                       parent,
                       false,
                       "",
                       options.detectFloatingFragments,
                       ref i);
    }

    /// <summary>
    /// Creates a new GameObject from the fragment data
    /// </summary>
    /// <param name="fragmentMeshData">Geometry of the fragment produced by the slicer</param>
    /// <param name="sourceObject">The source object to fragment. This object must have a MeshFilter, a RigidBody and a Collider.</param>
    /// <param name="fragmentTemplate">The template GameObject that each fragment will clone</param>
    /// <param name="parent">The parent transform for the fragment objects</param>
    /// <param name="i">Fragment counter</param>
    private static void CreateFragment(FragmentData fragmentMeshData,
                                       GameObject sourceObject,
                                       GameObject fragmentTemplate,
                                       Transform parent,
                                       bool saveToDisk,
                                       string saveFolderPath,
                                       bool detectFloatingFragments,
                                       ref int i)
    {
        // If there is no mesh data, don't create an object
        // (Triangles.Length is the submesh count, which is never 0: check the actual triangles)
        if (fragmentMeshData.triangleCount < 3)
        {
            return;
        }

        Mesh[] meshes;
        Mesh fragmentMesh = fragmentMeshData.ToMesh();

        // If the "Detect Floating Fragments" option is enabled, take the fragment mesh and
        // identify disconnected sets of geometry within it, treating each of these as a
        // separate physical object
        if (detectFloatingFragments)
        {
            meshes = MeshUtils.FindDisconnectedMeshes(fragmentMesh);
        }
        else
        {
            meshes = new Mesh[] { fragmentMesh };
        }

        var parentSize = sourceObject.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        var parentMass = sourceObject.GetComponent<Rigidbody>().mass;

        // Mass is split by volume. Guard against zero-size / zero-mass sources (would give 0 or NaN mass).
        float parentVolume = Mathf.Max(parentSize.x * parentSize.y * parentSize.z, 1e-6f);
        float density = parentVolume / Mathf.Max(parentMass, 0.01f);

        // The whole-fragment mesh is only kept if it is itself one of the pieces
        bool keepFragmentMesh = false;

        for (int k = 0; k < meshes.Length; k++)
        {
            // Skip slivers: a convex hull needs real volume. Cooking near-flat / tiny meshes is slow and is a
            // known PhysX crash source, and pieces this small aren't visible anyway.
            if (!IsUsableFragmentMesh(meshes[k]))
            {
                if (meshes[k] != fragmentMesh) { UnityEngine.Object.Destroy(meshes[k]); }
                continue;
            }
            if (meshes[k] == fragmentMesh) { keepFragmentMesh = true; }

            // --- REMOVED STANDARD INSTANTIATE ---
            // GameObject fragment = GameObject.Instantiate(fragmentTemplate, parent); 

            // --- ADDED OBJECT POOLING ---
            GameObject fragment = ObjectPoolingManager.instance.SpawnObject("Fragment", Vector3.zero);
            if (fragment == null)
            {
                // The whole-fragment mesh is freed after the loop; island meshes are freed here
                if (meshes[k] == fragmentMesh) { keepFragmentMesh = false; }
                else { UnityEngine.Object.Destroy(meshes[k]); }
                continue;
            }
            fragment.transform.SetParent(parent);
            

            fragment.name = $"Fragment{i}";
            fragment.transform.localPosition = Vector3.zero;
            fragment.transform.localRotation = Quaternion.identity;
            fragment.transform.localScale = sourceObject.transform.localScale;

            fragment.layer = LayerMask.NameToLayer("Destructed");
            fragment.tag = sourceObject.tag;

            meshes[k].name = System.Guid.NewGuid().ToString();

            var meshFilter = fragment.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = fragment.AddComponent<MeshFilter>();

            // The pooled piece owns the mesh and frees it when it goes back to the pool
            if (fragment.TryGetComponent<PooledFragment>(out var pooled)) { pooled.SetMesh(meshes[k]); }
            else { meshFilter.sharedMesh = meshes[k]; }

            var meshRenderer = fragment.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = fragment.AddComponent<MeshRenderer>();
            }
            else
            {
                if(meshRenderer.enabled == false)
                    meshRenderer.enabled = true;
            }

            var sourceRenderer = sourceObject.GetComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                Material insideMat = null;
                if (sourceObject.TryGetComponent<Fracture>(out var sourceFracture))
                {
                    insideMat = sourceFracture.fractureOptions.insideMaterial;
                }

                // Submeshes 0..N-1 = the source's own materials, last submesh = inside (cut) material
                meshRenderer.sharedMaterials = global::Fracture.BuildFragmentMaterials(
                    sourceRenderer.sharedMaterials,
                    sourceObject.GetComponent<MeshFilter>().sharedMesh,
                    insideMat);
            }

            var collider = fragment.GetComponent<MeshCollider>();

            // If fragment collisions are disabled, collider will be null
            if (collider != null)
            {
                collider.sharedMesh = meshes[k];
                collider.convex = true;
                collider.sharedMaterial = fragment.GetComponent<Collider>().sharedMaterial;
            }

            // Compute mass of the sliced object by dividing mesh bounds by density
            var parentRigidBody = sourceObject.GetComponent<Rigidbody>();
            var rigidBody = fragment.GetComponent<Rigidbody>();

            var size = meshes[k].bounds.size;
            rigidBody.mass = Mathf.Max(0.01f, (size.x * size.y * size.z) / density);

            // This code only compiles for the editor
#if UNITY_EDITOR
            if (saveToDisk)
            {
                string path = $"{saveFolderPath}/{meshes[k].name}.asset";
                AssetDatabase.CreateAsset(meshes[k], path);
            }
#endif

            i++;
        }

        // "Detect floating fragments" splits the mesh into new island meshes: the original isn't used then
        if (!keepFragmentMesh && fragmentMesh != null)
        {
            UnityEngine.Object.Destroy(fragmentMesh);
        }
    }

    // Minimum size of a piece on every axis (local units) and enough vertices for a 3D convex hull
    private const float MinFragmentExtent = 0.01f;

    private static bool IsUsableFragmentMesh(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount < 4) { return false; }
        Vector3 size = mesh.bounds.size;
        // NaN fails the >= tests below; infinity / huge values mean a broken cut
        if (!(size.x < 1e4f && size.y < 1e4f && size.z < 1e4f)) { return false; }
        return size.x >= MinFragmentExtent && size.y >= MinFragmentExtent && size.z >= MinFragmentExtent;
    }
}