using System;
using UnityEngine;

// int 값 하나를 싣는 이벤트 채널 - 점수/남은 이동 횟수처럼 단순 정수 페이로드를 전달할 때 재사용한다.
[CreateAssetMenu(fileName = "IntEventChannel", menuName = "Event Channels/Int Event Channel")]
public sealed class IntEventChannel : ScriptableObject
{
    public event Action<int> OnRaised;

    public void Raise(int value)
    {
        OnRaised?.Invoke(value);
    }
}
