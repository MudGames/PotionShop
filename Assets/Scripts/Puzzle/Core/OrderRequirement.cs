using System;

namespace Puzzle.Core
{
    // 스테이지 주문의 한 줄: "typeIndex 타일을 requiredCount개 모으기". GridController가 매 스테이지
    // 시작 시 무작위로 생성하는 목록의 항목 하나이며(GenerateRandomRequirements 참고), 모든
    // requirement의 수집 개수가 목표치에 도달하면 스테이지가 클리어된다.
    [Serializable]
    public struct OrderRequirement
    {
        public int typeIndex;
        public int requiredCount;
    }
}
