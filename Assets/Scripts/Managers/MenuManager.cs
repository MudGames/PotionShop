using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 화면의 UI를 런타임에 생성한다(배경/타이틀/시작/종료) - 퍼즐 UI 전반에서 쓰이는 것과
// 동일한 런타임 위젯 생성 패턴이다(PuzzleHud 등).
public sealed class MenuManager : MonoBehaviour
{
    // PuzzlePanel/노트 페이지가 쓰는 것과 동일한 장식용 9-슬라이스 테두리(Menu.png) - 타이틀
    // 뒤에 배치해서 배경 위에 텍스트만 떠 있는 게 아니라 노트북의 한 페이지처럼 보이게 한다.
    // 선택 사항.
    [SerializeField]
    private Sprite frameSprite;

    [SerializeField]
    private string mainSceneName = "Main";

    private void Start()
    {
        CreateButton("StartButton", new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.44f), "시작하기", OnStartButtonClicked);
        CreateButton("QuitButton", new Vector2(0.35f, 0.16f), new Vector2(0.65f, 0.28f), "종료", OnQuitButtonClicked);
    }

    private void CreateButton(string name, Vector2 anchorMin, Vector2 anchorMax, string text, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        buttonObject.GetComponent<Image>().color = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        buttonObject.GetComponent<Button>().onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 32.0f;
        label.color = Color.black;
    }

    private void OnStartButtonClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnQuitButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
