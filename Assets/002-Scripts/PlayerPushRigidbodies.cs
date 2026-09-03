using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPushRigidbodies : MonoBehaviour
{
    [Tooltip("How much force the player applies when walking into rigidbodies")]
    public float pushForce = 5f;

    // This is a built-in Unity function that fires whenever a CharacterController bumps into a Collider
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // If the object we hit doesn't have a Rigidbody, or if it's frozen (kinematic), do nothing
        if (body == null || body.isKinematic)
        {
            return;
        }

        // We don't want to push objects that are directly underneath the player (like a physics floor)
        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        // Calculate the push direction based on which way the player is moving (horizontal only)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Apply the force to the fragment! (ForceMode.Impulse feels more responsive for bumping)
        body.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}
