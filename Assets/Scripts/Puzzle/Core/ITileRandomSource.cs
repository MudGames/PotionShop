namespace Puzzle.Core
{
    // 타일 색상의 무작위성을 추상화하여, 실제 게임플레이에서는 UnityRandomTileSource를 사용하는 동안
    // EditMode 테스트에서는 (시드 고정된/고정값 fake를 통해) Core 로직을 결정론적으로 구동할 수 있게 한다.
    public interface ITileRandomSource
    {
        int NextTypeIndex(int typeCount);

        // minInclusive/maxExclusive 관례는 NextTypeIndex와 동일하다 (UnityEngine.Random.Range와 맞춤).
        int NextInRange(int minInclusive, int maxExclusive);
    }

    public sealed class UnityRandomTileSource : ITileRandomSource
    {
        public int NextTypeIndex(int typeCount)
        {
            return UnityEngine.Random.Range(0, typeCount);
        }

        public int NextInRange(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }
}
