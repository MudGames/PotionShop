using System;
using UnityEngine;

// 페이로드 없는 이벤트 채널 - 발행자(예: Match3Controller)는 이 에셋을 참조해 Raise()만 호출하고,
// 구독자(예: Witch, GameManager)도 같은 에셋을 참조해 OnRaised에 구독한다. 서로가 서로를 직접
// 참조하지 않는다 - CLAUDE.md 핵심 아키텍처 원칙 3번(이벤트 채널을 통한 결합) 참고.
[CreateAssetMenu(fileName = "VoidEventChannel", menuName = "Event Channels/Void Event Channel")]
public sealed class VoidEventChannel : ScriptableObject
{
    public event Action OnRaised;

    public void Raise()
    {
        OnRaised?.Invoke();
    }
}
