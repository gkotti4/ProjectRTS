using UnityEngine;

/// <summary>
/// Scene marker used by BattleGameModeController to place an army.
///
/// Local right controls squad-to-squad spacing.
/// Local forward controls the army's initial facing.
/// </summary>
public class BattleArmySpawnZone : MonoBehaviour
{
    [Header("Formation")]
    [Min(0.1f)]
    [SerializeField] private float squadSpacing = 12f;

    [Min(1)]
    [SerializeField] private int squadsPerRow = 4;

    [Min(0.1f)]
    [SerializeField] private float rowSpacing = 10f;

    public Vector3 GetSquadPosition(int squadIndex, int totalSquads)
    {
        int resolvedPerRow = Mathf.Max(1, squadsPerRow);
        int row = squadIndex / resolvedPerRow;
        int column = squadIndex % resolvedPerRow;

        int squadsInRow = Mathf.Min(
            resolvedPerRow,
            totalSquads - row * resolvedPerRow);

        float rowWidth = Mathf.Max(0, squadsInRow - 1) * squadSpacing;
        float lateralOffset = column * squadSpacing - rowWidth * 0.5f;
        float depthOffset = -row * rowSpacing;

        return transform.position +
               transform.right * lateralOffset +
               transform.forward * depthOffset;
    }

    public Quaternion GetSquadRotation()
    {
        Vector3 facing = transform.forward;
        facing.y = 0f;

        return facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : Quaternion.identity;
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 0.75f);
        Gizmos.DrawLine(
            transform.position,
            transform.position + transform.forward * 4f);
    }
}