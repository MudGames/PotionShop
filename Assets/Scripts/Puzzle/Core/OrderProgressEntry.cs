namespace Puzzle.Core
{
    // 하나의 OrderRequirement 진행 상황에 대한 읽기 전용 스냅샷으로, GridController.OrderProgress를
    // 통해 노출되어 Presentation이 게임 상태를 변경하지 않고 렌더링(예: HUD 행)할 수 있게 한다.
    public readonly struct OrderProgressEntry
    {
        public int TypeIndex { get; }
        public int Collected { get; }
        public int Required { get; }

        public bool IsComplete => Collected >= Required;

        public OrderProgressEntry(int typeIndex, int collected, int required)
        {
            TypeIndex = typeIndex;
            Collected = collected;
            Required = required;
        }
    }
}
