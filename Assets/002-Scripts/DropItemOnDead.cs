using UnityEngine;

public class DropItemOnDead : MonoBehaviour
{
    [SerializeField] private GameObject obj_DropItem;
    [SerializeField] private int dropChance_Min;
    [SerializeField] private int dropChance_Max;

    public void DropItem()
    {
        int dropChance = Random.Range(0, dropChance_Max);

        if(dropChance <= dropChance_Min)
        {
            GameObject itemObj = Instantiate(obj_DropItem, transform.position, transform.rotation);
        }
    }
}
