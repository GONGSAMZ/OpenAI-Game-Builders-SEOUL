using UnityEngine;

public interface ILocalSaveStore
{
    bool TryLoad(string scope, out SaveGameData data);
    bool TryLoad(string scope, out SaveGameData data, out bool pendingRemote);
    void Save(string scope, SaveGameData data);
    void Save(string scope, SaveGameData data, bool pendingRemote);
    void Backup(string sourceScope, string backupScope);
}

public sealed class PlayerPrefsLocalSaveStore : ILocalSaveStore
{
    private const string Prefix = "game_save_v2_";

    public bool TryLoad(string scope, out SaveGameData data)
    {
        return TryLoad(scope, out data, out _);
    }

    public bool TryLoad(string scope, out SaveGameData data, out bool pendingRemote)
    {
        string active = PlayerPrefs.GetString(Key(scope, "active"), "a");
        if (TryRead(scope, active, out data, out pendingRemote)) return true;
        return TryRead(scope, active == "a" ? "b" : "a", out data, out pendingRemote);
    }

    public void Save(string scope, SaveGameData data)
    {
        Save(scope, data, false);
    }

    public void Save(string scope, SaveGameData data, bool pendingRemote)
    {
        string active = PlayerPrefs.GetString(Key(scope, "active"), "a");
        string target = active == "a" ? "b" : "a";
        string payload = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(Key(scope, target), payload);
        PlayerPrefs.SetString(Key(scope, target + "_checksum"), Checksum(payload));
        PlayerPrefs.SetInt(Key(scope, target + "_pending_remote"), pendingRemote ? 1 : 0);
        PlayerPrefs.Save();
        PlayerPrefs.SetString(Key(scope, "active"), target);
        PlayerPrefs.Save();
    }

    public void Backup(string sourceScope, string backupScope)
    {
        if (TryLoad(sourceScope, out SaveGameData data)) Save(backupScope, data);
    }

    private static bool TryRead(
        string scope,
        string slot,
        out SaveGameData data,
        out bool pendingRemote)
    {
        data = null;
        pendingRemote = false;
        string payload = PlayerPrefs.GetString(Key(scope, slot), string.Empty);
        string checksum = PlayerPrefs.GetString(Key(scope, slot + "_checksum"), string.Empty);
        if (string.IsNullOrEmpty(payload) || checksum != Checksum(payload)) return false;
        try
        {
            data = JsonUtility.FromJson<SaveGameData>(payload);
            if (data == null) return false;
            SaveDataFactory.Normalize(data);
            pendingRemote = PlayerPrefs.GetInt(Key(scope, slot + "_pending_remote"), 0) == 1;
            return true;
        }
        catch
        {
            data = null;
            return false;
        }
    }

    private static string Key(string scope, string suffix) => Prefix + scope + "_" + suffix;

    private static string Checksum(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }
}
