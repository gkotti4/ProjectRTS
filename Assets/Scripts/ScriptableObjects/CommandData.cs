using UnityEngine;

[CreateAssetMenu(fileName = "CommandData", menuName = "Scriptable Objects/CommandData")]
public class CommandData : ScriptableObject
{
    public string commandName;
    public int commandId;
    public CommandType commandType;
    public Sprite icon;
    public HotkeySlot hotkey;
    public bool showButton = true; // false = hotkey only, no UI button
}
