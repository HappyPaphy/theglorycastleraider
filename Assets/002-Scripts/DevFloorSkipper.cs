using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shortcut to move between floors (testing / cheat). Available in the Editor and in every build.
///   F9          next floor
///   F8          previous floor
///   Shift + F9  this world's boss floor (1-3, 2-3, 3-3)
///   Shift + F8  first floor of the next world (1-1 -> 2-1 -> 3-1 -> 1-1)
/// Creates itself; no scene setup needed.
/// </summary>
public class DevFloorSkipper : MonoBehaviour
{
    private const int FloorsPerWorld = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("DevFloorSkipper");
        DontDestroyOnLoad(go);
        go.AddComponent<DevFloorSkipper>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        RoguelikeManager manager = RoguelikeManager.instance;
        if (keyboard == null || manager == null || manager.FloorCount == 0) { return; }

        bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        int current = manager.dungeonIndex;
        int target = -1;

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            // Shift: boss floor of this world, otherwise the next floor
            target = shift ? (current / FloorsPerWorld) * FloorsPerWorld + FloorsPerWorld - 1 : current + 1;
        }
        else if (keyboard.f8Key.wasPressedThisFrame)
        {
            // Shift: first floor of the next world (wraps), otherwise the previous floor
            target = shift ? ((current / FloorsPerWorld + 1) * FloorsPerWorld) % manager.FloorCount : current - 1;
        }

        if (target < 0 || target >= manager.FloorCount || target == current) { return; }

        manager.SkipToFloor(target); // the floor name shows on screen as usual
    }
}
