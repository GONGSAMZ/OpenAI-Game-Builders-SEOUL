using UnityEngine;

public interface ILocalSaveStore
{
    bool TryLoad(string scope, out SaveGameData data);
    void Save(string scope, SaveGameData data);
    void Backup(string sourceScope, string backupScope);
}

public sealed class PlayerPrefsLocalSaveStore : ILocalSaveStore
{
    private const string Prefix = "game_save_v2_";

    public bool TryLoad(string scope, out SaveGameData data)
    {
        string active = PlayerPrefs.GetString(Key(scope, "active"), "a");
        if (TryRead(scope, active, out data)) return true;
        return TryRead(scope, active == "a" ? "b" : "a", out data);
    }

    public void Save(string scope, SaveGameData data)
    {
        string active = PlayerPrefs.GetString(Key(scope, "active"), "a");
        string target = active == "a" ? "b" : "a";
        string payload = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(Key(scope, target), payload);
        PlayerPrefs.SetString(Key(scope, target + "_checksum"), Checksum(payload));
        PlayerPrefs.Save();
        PlayerPrefs.SetString(Key(scope, "active"), target);
        PlayerPrefs.Save();
    }

    public void Backup(string sourceScope, string backupScope)
    {
        if (TryLoad(sourceScope, out SaveGameData data)) Save(backupScope, data);
    }

    private static bool TryRead(string scope, string slot, out SaveGameData data)
    {
        data = null;
        string payload = PlayerPrefs.GetString(Key(scope, slot), string.Empty);
        string checksum = PlayerPrefs.GetString(Key(scope, slot + "_checksum"), string.Empty);
        if (string.IsNullOrEmpty(payload) || checksum != Checksum(payload)) return false;
        try
        {
            data = JsonUtility.FromJson<SaveGameData>(payload);
            if (data == null) return false;
            SaveDataFactory.Normalize(data);
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
