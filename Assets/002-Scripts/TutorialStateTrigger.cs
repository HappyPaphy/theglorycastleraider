using UnityEngine;

public enum TutorialTriggerType
{
    FallToDeath,
    ChangeScene
}

public class TutorialStateTrigger : MonoBehaviour
{
    [SerializeField] private TutorialTriggerType triggerType;

    private void TriggerTutorial()
    {
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
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            TriggerTutorial();
        }    
    }
}
