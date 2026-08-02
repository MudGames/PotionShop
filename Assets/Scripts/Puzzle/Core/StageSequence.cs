using UnityEngine;

namespace Puzzle.Core
{
    // 캠페인을 위한 스테이지의 순서 목록. GameFlowManager가 이 목록을 순회하며 각 LevelData를
    // 차례로 Match3Controller에 전달하고, 스테이지 사이에 note scene으로 전환한다.
    [CreateAssetMenu(fileName = "StageSequence", menuName = "Puzzle/Stage Sequence")]
    public sealed class StageSequence : ScriptableObject
    {
        public LevelData[] stages = new LevelData[0];
    }
}
