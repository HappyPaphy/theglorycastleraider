using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Iron bars in a Challenge Room doorway (placed by DunGen as the doorway's connector).
/// Raised (open, no collision) until the challenge starts, then it slams down and blocks the way.
/// </summary>
public class ChallengeGate : MonoBehaviour
{
    [SerializeField] private Transform bars;
    [SerializeField] private Collider blocker;
    [SerializeField] private NavMeshObstacle obstacle;
    [Tooltip("How far up the bars hide when open (into the wall above the doorway).")]
    [SerializeField] private float openHeight = 1.95f;
    [SerializeField] private float closeDuration = 0.25f;
    [SerializeField] private float openDuration = 0.8f;

    private DunGen.Door dunGenDoor;
    private Vector3 closedLocal;
    private bool isClosed;

    public bool IsClosed => isClosed;

    private void Awake()
    {
        dunGenDoor = GetComponent<DunGen.Door>();
        if (bars != null)
        {
            closedLocal = bars.localPosition;
            bars.localPosition = closedLocal + Vector3.up * openHeight;
        }
        SetBlocking(false);
    }

    private void Start()
    {
        dunGenDoor = GetComponent<DunGen.Door>(); // DunGen may add it after Awake
        if (dunGenDoor != null) { dunGenDoor.IsOpen = true; }
    }

    public void Close()
    {
        if (isClosed) { return; }
        isClosed = true;
        SetBlocking(true);

        if (bars != null)
        {
            bars.DOKill();
            bars.DOLocalMove(closedLocal, closeDuration).SetEase(Ease.InQuad).SetLink(gameObject)
                .OnComplete(() => bars.DOPunchPosition(Vector3.up * 0.06f, 0.2f, 6).SetLink(gameObject));
        }
        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Impact(transform.position + Vector3.up); }
    }

    public void Open()
    {
        if (!isClosed) { return; }
        isClosed = false;
        SetBlocking(false);

        if (bars != null)
        {
            bars.DOKill();
            bars.DOLocalMove(closedLocal + Vector3.up * openHeight, openDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
        }
        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Open(transform.position + Vector3.up); }
    }

    private void SetBlocking(bool on)
    {
        if (blocker != null) { blocker.enabled = on; }
        if (obstacle != null) { obstacle.enabled = on; }
        if (dunGenDoor != null) { dunGenDoor.IsOpen = !on; }
    }
}
