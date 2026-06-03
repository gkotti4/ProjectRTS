using UnityEngine;

[CreateAssetMenu(fileName = "CommandData", menuName = "Scriptable Objects/CommandData")]
public class CommandData : ScriptableObject
{
    public string commandName = "Command";
    public int commandId = 0;
    public CommandType commandType;
    public CommandScope commandScope = CommandScope.PerUnit;
    public Sprite icon;
    public HotkeySlot hotkey = HotkeySlot.None;
    public bool showButton = true; // false = hotkey only, no UI button
}
    // public CommandSubmenuType commandSubmenuType = CommandSubmenuType.None;
