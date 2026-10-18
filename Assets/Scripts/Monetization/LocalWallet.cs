using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// A temporary wallet saved on the phone (PlayerPrefs) so match rewards have somewhere to go. It is a stand-in: when the profile
    /// and backend exist, the profile service takes over (listen to MatchResultsFlow.RewardsClaimed and add to the profile instead).
    /// </summary>
    public static class LocalWallet
    {
        const string CoinsKey = "VehicleSystem.Wallet.Coins";
        const string CashKey = "VehicleSystem.Wallet.Cash";

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
        public static int Cash => PlayerPrefs.GetInt(CashKey, 0);

        public static void Add(int coins, int cash)
        {
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + coins));
            PlayerPrefs.SetInt(CashKey, Mathf.Max(0, Cash + cash));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
