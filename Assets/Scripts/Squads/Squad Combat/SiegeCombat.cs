using UnityEngine;

/// -----------------------------------------------------------------------------
/// SiegeCombat
/// -----------------------------------------------------------------------------
///
/// Siege-family behavior module for SquadCombat.
///
/// This remains intentionally small until the artillery MVP is implemented. The
/// combat-family entry point already exists so siege can be added without changing
/// the coordinator/family model again.
///
public partial class SquadCombat
{
    #region Siege Combat Module

    private bool siegeNotImplementedWarningLogged = false;

    void TickSiegeCombat()
    {
        // No shipped SquadData used the Siege family before this enum value existed.
        // If one is authored before the artillery pass lands, fail safely rather than
        // accidentally routing siege through melee/ranged formed behavior.
        if (!siegeNotImplementedWarningLogged)
        {
            Debug.LogWarning(
                $"{name}: Siege combat is selected but its artillery behavior is not implemented yet.",
                this);
            siegeNotImplementedWarningLogged = true;
        }

        EndCombatAndReform();
    }

    #endregion
}