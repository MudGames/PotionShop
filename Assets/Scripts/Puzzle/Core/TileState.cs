namespace Puzzle.Core
{
    // TypeIndex: -1 = empty (일시적 상태로, refill을 기다리는 중), -2 = blocked (영구적인 장애물로,
    // 절대 매치되거나 채워지지 않음), >=0 = 팔레트 색상 인덱스.
    // Special은 TypeIndex >= 0일 때만 의미가 있다 - 매치(MatchDetector)와 주문(Order) 수집
    // (GridController.ResolveOneStep) 양쪽 다 Special이 설정된 타일은 별개의 타일로 취급해, 밑에
    // 남아있는 TypeIndex를 매치/수집 판정에 쓰지 않는다(2026-08-04 확정). TypeIndex 자체는 스왑으로
    // 다른 칸으로 옮겨갈 때 함께 이동하는 값이라 구조상 계속 들고 있을 뿐이다.
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
