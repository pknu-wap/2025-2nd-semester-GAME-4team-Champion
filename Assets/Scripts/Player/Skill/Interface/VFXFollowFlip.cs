using UnityEngine;

/// <summary>
/// 플레이어의 좌우 플립(SpriteRenderer.flipX)을 따라
/// VFX의 위치(x 오프셋)와 스케일을 자동으로 반전시켜 준다.
/// - attach=true면 부모(플레이어) 아래 localPosition으로, false면 worldPosition으로 추적
/// - offset.x는 "양수"만 넘기면 좌/우 자동 반전
/// </summary>
public class VFXFollowFlip : MonoBehaviour
{
    private Transform _player;
    private SpriteRenderer _sprite;
    private Vector3 _baseLocalOffset; // ( +|x|, y, 0 )
    private bool _attachToPlayer;
    private Vector3 _baseScale;

    public void Init(Transform player, SpriteRenderer sprite, Vector2 offset, bool attachToPlayer)
    {
        _player = player;
        _sprite = sprite;
        _attachToPlayer = attachToPlayer;

        // x는 절대값으로 저장(좌/우는 flipX로 결정)
        _baseLocalOffset = new Vector3(Mathf.Abs(offset.x), offset.y, 0f);
        _baseScale = transform.localScale;

        if (_attachToPlayer && transform.parent != _player)
            transform.SetParent(_player, worldPositionStays: true);

        SyncNow();
    }

    private void LateUpdate() => SyncNow();

    private void SyncNow()
    {
        if (_player == null) return;

        bool flip = _sprite != null ? _sprite.flipX : (_player.localScale.x < 0f);
        float signedX = flip ? -_baseLocalOffset.x : _baseLocalOffset.x;

        Vector3 worldPos = _player.position + new Vector3(signedX, _baseLocalOffset.y, 0f);

        if (_attachToPlayer)
            transform.localPosition = new Vector3(signedX, _baseLocalOffset.y, 0f);
        else
            transform.position = worldPos;

        var s = _baseScale;
        s.x = Mathf.Abs(_baseScale.x) * (flip ? -1f : 1f);
        transform.localScale = s;
    }
}
