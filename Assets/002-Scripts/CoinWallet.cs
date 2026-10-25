using System.Runtime.CompilerServices;
using UnityEngine;

public class CoinWallet : MonoBehaviour
{
    [SerializeField] private int coins = 100;
    public int Coins => coins;

    private void Start()
    {
        if(PlayerController.instance.coinWallet == null)
        {
            PlayerController.instance.coinWallet = this;
        }
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0)
            return false;

        if (coins < amount)
            return false;

        coins -= amount;
        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        coins += amount;
    }
}