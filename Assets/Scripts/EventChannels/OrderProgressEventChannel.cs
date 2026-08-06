using System;
using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

// 주문 진행도 스냅샷(IReadOnlyList<OrderProgressEntry>)을 싣는 이벤트 채널 - GridController가
// 캐스케이드 스텝마다 최신 진행도로 Raise하고, PuzzleSidePanel이 구독해 주문 재료 UI를 갱신한다.
[CreateAssetMenu(fileName = "OrderProgressEventChannel", menuName = "Event Channels/Order Progress Event Channel")]
public sealed class OrderProgressEventChannel : ScriptableObject
{
    public event Action<IReadOnlyList<OrderProgressEntry>> OnRaised;

    public void Raise(IReadOnlyList<OrderProgressEntry> progress)
    {
        OnRaised?.Invoke(progress);
    }
}
