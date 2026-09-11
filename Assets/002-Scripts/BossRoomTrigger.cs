using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class BossRoomTrigger : MonoBehaviour
{
    [SerializeField] private EnemyEntity enemyBoss;
    [SerializeField] private List<GameObject> doorWays;
    [SerializeField] private List<Fracture> doors;

    private bool isGetDoorsOnStartOnce = false;
    private bool isBossDiedOnce = false;
    private bool isRoomTriggered = false;

    protected void OnEnable()
    {
        if (RoguelikeManager.instance != null)
        {
            if (RoguelikeManager.IsDungeonReady)
            {
                GetDoors();
            }
            else
            {
                RoguelikeManager.OnDungeonReady += GetDoors;
            }
        }
    }

    private void OnDisable()
    {
        if (RoguelikeManager.instance != null)
            RoguelikeManager.OnDungeonReady -= GetDoors;
    }

    private void GetDoors()
    {
        if (!isGetDoorsOnStartOnce)
        {
            isGetDoorsOnStartOnce = true;

            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i] == null) { break; }

                if (doors[i].gameObject.activeInHierarchy)
                {
                    doors[i].isCanBeDamage = false;
                    doors[i].gameObject.SetActive(false);
                }
                else
                {
                    doors.Remove(doors[i]);
                    continue;
                }
            }
        }
    }

    private void Update()
    {
        if (RoguelikeManager.IsDungeonReady && !isGetDoorsOnStartOnce)
        {
            GetDoors();
        }

        if (enemyBoss.CharacterHealthComponent.CurrentHP <= 0f && !isBossDiedOnce)
        {
            isBossDiedOnce = true;

            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].isCanBeDamage = true;
                doors[i].TakeDamage(99999);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isRoomTriggered) { return; }

            if (enemyBoss != null)
            {
                foreach (Fracture door in doors)
                {
                    door.gameObject.SetActive(true);
                }

                enemyBoss.PlayerDetected();
                isRoomTriggered = true;
            }
        }
    }
}
