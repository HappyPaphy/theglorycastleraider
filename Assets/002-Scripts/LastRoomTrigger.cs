using UnityEngine;

public class LastRoomTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            RoguelikeManager.instance.isPlayerInTheLastRoom = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            RoguelikeManager.instance.isPlayerInTheLastRoom = false;
        }
    }
}
