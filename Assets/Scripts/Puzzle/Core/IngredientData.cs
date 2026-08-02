using UnityEngine;

namespace Puzzle.Core
{
    // 타일 종류 하나를 나타내는 데이터 에셋 (convention.md §9). 등급/승급 필드는 두지 않는다 -
    // 매치3는 동일 타일 매치이므로 불필요하다.
    [CreateAssetMenu(fileName = "IngredientData", menuName = "Puzzle/Ingredient Data")]
    public sealed class IngredientData : ScriptableObject
    {
        public Sprite sprite;
    }
}
