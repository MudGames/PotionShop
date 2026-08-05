using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 화면의 UI를 런타임에 생성한다(배경/타이틀/시작/종료) - 퍼즐 UI 전반에서 쓰이는 것과
// 동일한 런타임 위젯 생성 패턴이다(PuzzleHud 등).
public sealed class MenuManager : MonoBehaviour
{
    private string mainSceneName = "Main";

    // Assets/Art/Sprites/UI/Menu.png를 3분할(Menu_Logo/Menu_Start/Menu_Quit)한 서브스프라이트
    // 중 버튼 2종(2026-08-05 추가, "시작하기와 종료 버튼 이미지가 있습니다. 각각 슬라이스해서
    // 기존 버튼에 이미지를 적용해주세요" 요청) - "시작하기"/"종료" 글자가 이미 그림에 그려져
    // 있어서, 기존에 코드로 따로 얹던 TextMeshProUGUI 라벨은 제거했다(안 그러면 글자가 겹쳐 보임).
    [SerializeField]
    private Sprite startButtonSprite;
    [SerializeField]
    private Sprite quitButtonSprite;

    private void Start()
    {
        // 앵커 높이는 임의 비율이 아니라 Menu_Start/Menu_Quit 서브스프라이트의 원본 가로세로
        // 비율(각각 564x149, 564x147px)에 맞춰 계산한 값이다(2026-08-05) - 가로 비율(0.30)은
        // 그대로 두고 세로만 맞췄더니 이전엔 세로가 약 13% 납작하게 눌려 보였다("눌림을
        // 없애주세요" 피드백).
        CreateButton("StartButton", new Vector2(0.35f, 0.3096f), new Vector2(0.65f, 0.4504f), startButtonSprite, OnStartButtonClicked);
        CreateButton("QuitButton", new Vector2(0.35f, 0.1505f), new Vector2(0.65f, 0.2895f), quitButtonSprite, OnQuitButtonClicked);
    }

    private void CreateButton(string name, Vector2 anchorMin, Vector2 anchorMax, Sprite sprite, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = sprite;
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = Color.white;
        buttonObject.GetComponent<Button>().onClick.AddListener(onClick);
        ButtonHoverAnimator hoverAnimator = buttonObject.AddComponent<ButtonHoverAnimator>();
        hoverAnimator.SetButtonImage(buttonImage);
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
