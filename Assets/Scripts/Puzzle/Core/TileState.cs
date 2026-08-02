namespace Puzzle.Core
{
    // TypeIndex: -1 = empty (일시적 상태로, refill을 기다리는 중), -2 = blocked (영구적인 장애물로,
    // 절대 매치되거나 채워지지 않음), >=0 = 팔레트 색상 인덱스.
    // Special은 TypeIndex >= 0일 때만 의미가 있다 - 스페셜 타일도 여전히 자신의 색상을 기억한다
    // (예를 들어 ColorBomb은 터질 때 어떤 색상을 clear할지 알아야 한다).
    public readonly struct TileState
    {
        public const int Empty = -1;
        public const int Blocked = -2;

        public int TypeIndex { get; }
        public SpecialKind Special { get; }

        public bool IsEmpty => TypeIndex == Empty;
        public bool IsBlocked => TypeIndex == Blocked;
        public bool IsFilled => TypeIndex >= 0;

        public TileState(int typeIndex, SpecialKind special = SpecialKind.None)
        {
            TypeIndex = typeIndex;
            Special = special;
        }

        public static readonly TileState EmptyState = new TileState(Empty);
        public static readonly TileState BlockedState = new TileState(Blocked);

        public TileState WithSpecial(SpecialKind special)
        {
            return new TileState(TypeIndex, special);
        }
    }
}
