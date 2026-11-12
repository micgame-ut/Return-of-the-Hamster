using System;
using System.Collections.Generic;
using UnityEngine;

// The high-score list, saved on this machine. Keeps the best MaxEntries scores, best first.
public static class LeaderboardStore
{
    public const int MaxEntries = 10;
    private const string Key = "Leaderboard";

    [Serializable]
    public class Entry
    {
        public string name;
        public int score;
    }

    // JsonUtility can't save a bare list, so the list sits inside this small wrapper.
    [Serializable]
    private class Data
    {
        public List<Entry> entries = new List<Entry>();
    }

    private static Data data;   // loaded the first time it's needed, then kept

    public static IReadOnlyList<Entry> Entries => Load().entries;

    private static Data Load()
    {
        if (data != null) return data;

        string json = PlayerPrefs.GetString(Key, "");
        try { data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json); }
        catch (ArgumentException) { data = new Data(); }   // a damaged save starts a fresh list
        return data;
    }

    private static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // True if this score would make the list: above 0, and either there is room or it beats the lowest entry.
    public static bool Qualifies(int score)
    {
        List<Entry> list = Load().entries;
        return score > 0 && (list.Count < MaxEntries || score > list[list.Count - 1].score);
    }

    // Adds a score and returns its place (0 = first), or -1 if it didn't make the list.
    public static int Submit(string name, int score)
    {
        if (!Qualifies(score)) return -1;

        List<Entry> list = Load().entries;

        // Goes below every entry with the same or a higher score, so a tie keeps the earlier one on top.
        int rank = 0;
        while (rank < list.Count && list[rank].score >= score) rank++;

        list.Insert(rank, new Entry { name = name, score = score });
        if (list.Count > MaxEntries) list.RemoveAt(list.Count - 1);
        Save();
        return rank;
    }

    // Empties the list and its save. For testing.
    public static void Clear()
    {
        data = new Data();
        PlayerPrefs.DeleteKey(Key);
    }
}
