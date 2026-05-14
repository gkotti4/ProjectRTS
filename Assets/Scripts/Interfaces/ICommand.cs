using UnityEngine;

public interface ICommand // ONLY NEEDED FOR COMPONENT COMMANDS (COULD DELETE)
{
    string CommandName { get; }
    Sprite Icon { get; }
    HotkeySlot Hotkey { get; }
    bool ShowButton { get; }
    bool IsContextCommand { get; } // right click triggers it

    bool CanExecute(RaycastHit hit);
    void Execute(RaycastHit hit);
    void Tick();
    void Cancel();
}