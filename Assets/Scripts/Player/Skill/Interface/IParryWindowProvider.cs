using UnityEngine;

public interface IParryWindowProvider
{
    /// 지금이 패링 가능한 ‘유효 창’인지 (히트 프레임 기준)
    bool IsParryWindowActive { get; }

    /// 패링 성공이 확정되었을 때 불립니다(카운터/쿨타임 조정 등 스킬 내부 처리).
    void OnParrySuccess();
}
