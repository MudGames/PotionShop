using UnityEngine;

// 퍼즐 보드 하나에 한정된 TileView 인스턴스들을 풀링한다. 그리드를 다시 구성할 때(레벨 변경이나
// 재시작 시) 파괴하는 대신 모든 것을 풀로 반환하고, 새 그리드를 위해 그 GameObject들을 재사용한다 -
// 지금까지 본 것보다 더 큰 그리드가 필요할 때만 커진다(새 TileView를 생성). 인덱스 관리 자체는
// PoolManager<T>에 위임하고, 이 클래스는 "반환된 TileView를 비활성화한다"는 자기 도메인의 규칙만
// 책임진다.
public sealed class TileViewPool
{
    private readonly PoolManager<TileView> _pool;

    public TileViewPool(Transform parent, Sprite rowBombSprite, Sprite columnBombSprite, Sprite radiusBombSprite)
    {
        _pool = new PoolManager<TileView>(() => new TileView(parent, rowBombSprite, columnBombSprite, radiusBombSprite));
    }

    public void ReturnAll()
    {
        foreach (TileView view in _pool.All)
        {
            view.SetActive(false);
        }

        _pool.ResetRent();
    }

    public TileView Rent()
    {
        TileView view = _pool.Rent();
        view.SetActive(true);
        return view;
    }
}
