using UnityEngine;
using UnityEngine.Tilemaps;

public enum FieldEffectType
{
    None = 0,
    SpeedPad,      // 속도 장판 (Trigger Zone)
    JJangStone,  // 넘어짐 장애물 (Trigger Zone)
    SolidWall,     // 막는 장애물 (Solid)
}

[CreateAssetMenu(fileName = "FieldTile", menuName = "Game/Field/Field Tile")]
public class FieldTile : Tile
{
    [Header("Field Effect")]
    public FieldEffectType effectType = FieldEffectType.None;

    [Tooltip("이 타일을 Trigger Zone(통과)로 쓸지, Solid(막힘)로 쓸지")]
    public bool isTriggerZone = true; // SpeedPad, TripObstacle 기본 true / SolidWall은 false 권장

    [Header("Optional Settings")]
    public SpeedPadSettings speedPadSettings; // SpeedPad일 때 사용
    public float tripPushForce = 8f;          // TripObstacle일 때 사용
    public float tripStopDelay = 0.15f;       // TripObstacle일 때 사용
}
