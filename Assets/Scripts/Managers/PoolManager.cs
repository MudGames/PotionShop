using System;
using System.Collections.Generic;

// TileViewPool/BoardSnapshotPool처럼 "필요한 만큼 rent하고, 다음 사이클에 처음부터 다시 rent하는"
// 풀들이 공통으로 반복하던 인덱스 관리(모자라면 factory로 새로 만들고, 있으면 재사용) 로직만
// 뽑아낸 범용 유틸리티다. 각 도메인 풀은 이 클래스를 내부적으로 소유(composition)할 뿐, 여러
// 도메인이 이 클래스 하나를 공유하는 것은 아니다 - CLAUDE.md의 "범용 단일 풀 매니저를 두지 않고,
// 각 풀이 자기 도메인의 rent/return만 책임짐" 원칙은 그대로 유지된다. 유휴 상태가 된 항목을
// 비활성화하는 등 도메인별 뒷정리는 각 풀 클래스 자신이 담당한다 - 이 유틸은 "몇 번째 슬롯을
// 돌려줄지"만 알 뿐, 그 항목으로 무엇을 할지는 전혀 모른다.
public sealed class PoolManager<T>
{
    private readonly List<T> _all = new List<T>();
    private readonly Func<T> _factory;
    private int _nextFreeIndex;

    public PoolManager(Func<T> factory)
    {
        _factory = factory;
    }

    // 지금까지 생성된 항목 전체 - 유휴 상태가 된 항목을 정리해야 하는 풀(RentedCount 이후 구간)이
    // 참고한다.
    public IReadOnlyList<T> All => _all;

    // 이번 사이클에 실제로 rent된 개수 - All의 [0, RentedCount) 구간이 "사용 중"이다.
    public int RentedCount => _nextFreeIndex;

    // 다음 Rent() 호출이 처음부터(인덱스 0부터) 다시 시작하도록 되돌린다.
    public void ResetRent()
    {
        _nextFreeIndex = 0;
    }

    public T Rent()
    {
        T item;
        if (_nextFreeIndex < _all.Count)
        {
            item = _all[_nextFreeIndex];
        }
        else
        {
            item = _factory();
            _all.Add(item);
        }

        _nextFreeIndex++;
        return item;
    }
}
