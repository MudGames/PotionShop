using UnityEngine;

namespace Puzzle.Core
{
    // 데이터 기반 레벨 정의. 디자이너는 여기서 새 에셋을 만드는 것만으로 레벨을 추가/조정할 수 있으며,
    // 코드 변경이 필요 없다. 아래 필드들의 기본값은 예전에 Match3Controller에 하드코딩되어 있던 보드
    // 설정과 동일하므로, 새로 만든 에셋도 기존과 동일하게 동작한다.
    [CreateAssetMenu(fileName = "LevelData", menuName = "Puzzle/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        // 이 스테이지의 재료들이 만들어내는 물약의 이름(예: "붉은 열매 물약") - 이 스테이지를 클리어하면
        // 노트의 목차에 기록된다. 비워두면 스테이지는 정상적으로 플레이되지만 노트북에는 아무 흔적도
        // 남지 않는다.
        public string title = "";

        // 이 스테이지를 클리어하면 노트 페이지에서 물약 이름과 함께 표시되는 짧은 한 줄 설명.
        [TextArea]
        public string flavorText = "";

        [Min(3)]
        public int rows = 6;

        [Min(3)]
        public int columns = 6;

        // 0이면 이동 횟수 무제한.
        [Min(0)]
        public int moveLimit = 20;

        // 이 레벨에 등장하는 재료 종류 - 이 배열의 길이 자체가 보드 위에 존재하는 고유 타일 타입
        // 개수이다(GridController가 ingredients.Length를 타입 개수로 읽어 들인다).
        public IngredientData[] ingredients = new IngredientData[0];

        // 선택적인 장애물 셀: 영구적으로 막혀 있으며, 매치되지도 채워지지도 않는다.
        public GridCell[] blockedCells = new GridCell[0];
    }
}
