using UnityEngine;
using UnityEngine.Tilemaps;

public enum FieldEffectType
{
    None = 0,
    SpeedPad,      // �ӵ� ���� (Trigger Zone)
    JJangStone,  // �Ѿ��� ��ֹ� (Trigger Zone)
    SolidWall,     // ���� ��ֹ� (Solid)
}

[CreateAssetMenu(fileName = "FieldTile", menuName = "Game/Field/Field Tile")]
public class FieldTile : Tile
{
    [Header("Field Effect")]
    public FieldEffectType effectType = FieldEffectType.None;

    [Tooltip("�� Ÿ���� Trigger Zone(���)�� ����, Solid(����)�� ����")]
    public bool isTriggerZone = true; // SpeedPad, TripObstacle �⺻ true / SolidWall�� false ����

    [Header("Optional Settings")]
    public SpeedPadSettings speedPadSettings; // SpeedPad�� �� ���
    public float tripPushForce = 8f;          // TripObstacle�� �� ���
    public float tripStopDelay = 0.15f;       // TripObstacle�� �� ���
}
