using UnityEngine;

public enum TutorialTriggerType
{
    FallToDeath,
    ChangeScene,
    PickUpWeapon,
    ShowPopup,
}

[RequireComponent(typeof(Collider))]
public class TutorialStateTrigger : MonoBehaviour
{
    [SerializeField] private TutorialTriggerType triggerType;

    [Header("ShowPopup only")]
    [Tooltip("The pages to show (header, description, GIF frames)")]
    [SerializeField] private TutorialData tutorialData;
    [SerializeField] private bool disableAfterTrigger = true;

    [Header("Optional Blockers")]
    [Tooltip("Optional. Objects (e.g. invisible walls with colliders) that stop the player until they press the hint's " +
             "dismiss action. They get deactivated once it's pressed. Don't make them children of this trigger.")]
    [SerializeField] private GameObject[] blockers;

    private void TriggerTutorial()
    {
        if (TutorialManager.instance == null) { return; }

        switch(triggerType)
        {
            case TutorialTriggerType.FallToDeath:
                {
                    StartCoroutine(TutorialManager.instance.TriggerFallToDeath());
                }
                break;

            case TutorialTriggerType.ChangeScene:
                {
                    StartCoroutine(TutorialManager.instance.TriggerChangeScene());
                }
                break;

            case TutorialTriggerType.ShowPopup:
                {
                    bool hasBlockers = blockers != null && blockers.Length > 0;
                    bool isShown = TutorialManager.instance.ShowTutorial(tutorialData, hasBlockers ? new System.Action(ReleaseBlockers) : null);

                    // Already seen (Show Only Once) or invalid data: don't leave the path blocked
                    if (!isShown && hasBlockers) { ReleaseBlockers(); }

                    if (disableAfterTrigger)
                    {
                        gameObject.SetActive(false);
                    }
                }
                break;
        }
    }

    private void ReleaseBlockers()
    {
        if (blockers == null) { return; }

        foreach (GameObject blocker in blockers)
        {
            if (blocker != null) { blocker.SetActive(false); }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (blockers == null || !disableAfterTrigger) { return; }

        foreach (GameObject blocker in blockers)
        {
            if (blocker != null && blocker.transform.IsChildOf(transform))
            {
                Debug.LogWarning($"[TutorialStateTrigger] Blocker '{blocker.name}' is a child of trigger '{name}'. " +
                                 "It will be disabled together with the trigger. Move it outside the trigger.", this);
            }
        }
    }
#endif

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            TriggerTutorial();
        }
    }
}
