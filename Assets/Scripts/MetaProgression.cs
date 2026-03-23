using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Stub meta layer: XP / soft currency / rank label stored in PlayerPrefs (replace with server + real store later).
    /// </summary>
    public class MetaProgression : MonoBehaviour
    {
        public static MetaProgression Instance { get; private set; }

        const string KeyXp = "nexus_meta_xp_v1";
        const string KeyCurrency = "nexus_meta_currency_v1";

        public int XP { get; private set; }
        public int Currency { get; private set; }

        public string RankLabel
        {
            get
            {
                if (XP < 50) return "Recruit";
                if (XP < 200) return "Operator";
                if (XP < 500) return "Veteran";
                return "Elite";
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        void Load()
        {
            XP = PlayerPrefs.GetInt(KeyXp, 0);
            Currency = PlayerPrefs.GetInt(KeyCurrency, 0);
        }

        void Save()
        {
            PlayerPrefs.SetInt(KeyXp, XP);
            PlayerPrefs.SetInt(KeyCurrency, Currency);
            PlayerPrefs.Save();
        }

        public void AddXp(int amount)
        {
            if (amount <= 0)
                return;
            XP += amount;
            Save();
        }

        public void AddCurrency(int amount)
        {
            if (amount == 0)
                return;
            Currency = Mathf.Max(0, Currency + amount);
            Save();
        }

        /// <summary>Call when a battle win is scored (stub reward).</summary>
        public void OnBattleWinReward()
        {
            AddXp(15);
            AddCurrency(5);
        }
    }
}
