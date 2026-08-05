using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 화면의 UI를 런타임에 생성한다(배경/타이틀/시작/종료) - 퍼즐 UI 전반에서 쓰이는 것과
// 동일한 런타임 위젯 생성 패턴이다(PuzzleHud 등).
public sealed class MenuManager : MonoBehaviour
{
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

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        buttonObject.GetComponent<Button>().onClick.AddListener(onClick);
        ButtonHoverAnimator hoverAnimator = buttonObject.AddComponent<ButtonHoverAnimator>();
        hoverAnimator.SetButtonImage(buttonImage);

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
