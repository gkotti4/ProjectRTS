using System;
using System.IO;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// SaveManager
/// -----------------------------------------------------------------------------
///
/// Generic JSON file persistence service.
///
/// This class intentionally knows nothing about factions, squads, game modes, or
/// progression rules. GameSession/game-mode code decides what data is persisted;
/// SaveManager only writes and reads serializable DTOs.
/// -----------------------------------------------------------------------------
public sealed class SaveManager
{
    private const string saveFileExtension = ".json";
    private const string defaultSaveSlot = "save_01";

    public string GetSavePath(string saveSlot = defaultSaveSlot)
    {
        string safeSlot = SanitizeSaveSlot(saveSlot);

        return Path.Combine(
            Application.persistentDataPath,
            safeSlot + saveFileExtension);
    }

    public bool Save<T>(T saveData, string saveSlot = defaultSaveSlot)
    {
        if (saveData == null)
        {
            Debug.LogError("SaveManager.Save failed: save data is null.");
            return false;
        }

        try
        {
            string json = JsonUtility.ToJson(saveData, prettyPrint: true);
            File.WriteAllText(GetSavePath(saveSlot), json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SaveManager.Save failed for slot '{saveSlot}': {exception}");
            return false;
        }
    }

    public bool TryLoad<T>(
        out T saveData,
        string saveSlot = defaultSaveSlot)
        where T : class
    {
        saveData = null;
        string path = GetSavePath(saveSlot);

        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            saveData = JsonUtility.FromJson<T>(json);
            return saveData != null;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SaveManager.TryLoad failed for slot '{saveSlot}': {exception}");
            return false;
        }
    }

    public bool SaveExists(string saveSlot = defaultSaveSlot)
    {
        return File.Exists(GetSavePath(saveSlot));
    }

    public bool DeleteSave(string saveSlot = defaultSaveSlot)
    {
        string path = GetSavePath(saveSlot);

        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SaveManager.DeleteSave failed for slot '{saveSlot}': {exception}");
            return false;
        }
    }

    string SanitizeSaveSlot(string saveSlot)
    {
        if (string.IsNullOrWhiteSpace(saveSlot))
            saveSlot = defaultSaveSlot;

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            saveSlot = saveSlot.Replace(invalidCharacter, '_');

        return saveSlot.Trim();
    }
}
