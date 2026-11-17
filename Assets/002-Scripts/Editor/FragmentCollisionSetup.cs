using UnityEditor;
using UnityEngine;

/// <summary>
/// Lets broken fracture pieces (layer "Destructed") bounce off the rest of the scene:
/// level geometry (Obstacle + Default), intact furniture/doors (Destructable) and each other.
/// They still ignore the Player and Enemies so debris never blocks movement.
/// Runs once automatically (saves Project Settings > Physics > Layer Collision Matrix), or via Tools > Physics.
/// </summary>
public static class FragmentCollisionSetup
{
    private static string AutoKey => "FragmentCollisionSetup.Done.v1." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey, true);
            Apply();
        };
    }

    [MenuItem("Tools/Physics/Let Fragments Collide With Scene")]
    public static void Apply()
    {
        int fragments = LayerMask.NameToLayer("Destructed");
        if (fragments < 0)
        {
            Debug.LogWarning("[FragmentCollisionSetup] No 'Destructed' layer found.");
            return;
        }

        string[] collideWith = { "Default", "Obstacle", "Destructable", "Destructed" };
        foreach (string layerName in collideWith)
        {
            int other = LayerMask.NameToLayer(layerName);
            if (other >= 0) { Physics.IgnoreLayerCollision(fragments, other, false); }
        }

        // Persist the change to ProjectSettings/DynamicsManager.asset
        Object[] dynamics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
        if (dynamics.Length > 0) { EditorUtility.SetDirty(dynamics[0]); }
        AssetDatabase.SaveAssets();

        Debug.Log("[FragmentCollisionSetup] 'Destructed' fragments now collide with Default, Obstacle, Destructable and each other.");
    }
}
