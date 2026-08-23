using UnityEngine;

[CreateAssetMenu(fileName = "CommandData", menuName = "Scriptable Objects/CommandData")]
public class CommandData : ScriptableObject
{
    public string commandName = "Command";
    [Tooltip("Optional future stable ID. Current command routing uses commandType.")]
    public int commandId = 0;
    
    public CommandType commandType;
    public Sprite icon;
    public HotkeySlot hotkey = HotkeySlot.None;
    public bool showButton = true; // false = hotkey only, no UI button
}
    // public CommandSubmenuType commandSubmenuType = CommandSubmenuType.None;
