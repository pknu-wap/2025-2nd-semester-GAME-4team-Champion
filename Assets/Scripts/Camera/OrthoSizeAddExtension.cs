using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine 파이프라인의 Finalize 단계에서 OrthographicSize에 '가산값'을 더해준다.
/// 다른 스크립트가 중간에 값을 바꿔도 최종 프레임에 덮어씌우기 때문에 안전.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public class OrthoSizeAddExtension : CinemachineExtension
{
    [Tooltip("프레임 최종 단계에서 추가로 더할 OrthographicSize 값")]
    public float add = 0f;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;

        // 직교 카메라만 가정 (탑다운 2D)
        var lens = state.Lens;
        lens.OrthographicSize += add;
        state.Lens = lens;
    }
}
