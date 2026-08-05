using UnityEngine;

// 매치 버스트 파편(BurstShardView) 전용 풀 - BurstEffectPool/TileViewPool과 같은 모양이다. 한
// 캐스케이드 스텝 동안 필요한 만큼 Rent()로 빌려 쓰고, 그 스텝의 애니메이션이 전부 끝나면(각
// 인스턴스는 Animate() 안에서 이미 비활성화됨) ResetRent()로 인덱스만 되돌려 다음 스텝에 같은
// 인스턴스를 재사용한다 - new/Destroy 없음.
public sealed class BurstShardPool
{
    private readonly PoolManager<BurstShardView> _pool;

    public BurstShardPool(Transform parent, Sprite shardSprite)
    {
        _pool = new PoolManager<BurstShardView>(() => new BurstShardView(parent, shardSprite));
    }

    public BurstShardView Rent()
    {
        BurstShardView view = _pool.Rent();
        view.SetActive(true);
        return view;
    }

    public void ResetRent()
    {
        _pool.ResetRent();
    }
}
