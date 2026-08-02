using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점수 HUD 바와 Clear!/Game Over 배너를 담당한다 - 순수한 UI 상태와 위젯 생성만 하며 게임 규칙은
// 없다. 미션(주문 진행도)과 이동 제한 표시는 이 상단 바가 아니라 퍼즐 보드 옆에 위치한 자체
// PuzzleSidePanel에 있다. 어두운 배경 패널과 텍스트 외곽선 덕분에 뒤에 무엇이 보이든 가독성이
// 유지된다.
//
// Match3Controller/GameManager를 직접 참조하지 않는다 - ScoreChangedChannel/OrderClearedChannel/
// GameOverChannel을 구독해 스스로 갱신하고, Clear! 배너의 버튼은 AdvanceRequestedChannel을
// Raise할 뿐이다(누가 듣는지는 알 필요 없다) - CLAUDE.md 이벤트 채널 아키텍처 원칙 참고.
public sealed class PuzzleHud
{
    private static readonly Color PanelBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);

    private readonly TextMeshProUGUI _scoreLabel;
    private readonly GameObject _completeBanner;
    private readonly GameObject _gameOverBanner;
    private readonly VoidEventChannel _advanceRequestedChannel;

    // hudParent: HUD 바가 붙을 곳. 퍼즐 패널이 아닌 퍼즐 Canvas를 전달해야, 패널 자체의 경계와
    // 무관하게 상단에서 화면 너비 전체를 가로지른다.
    // bannerParent: Clear!/Game Over 배너가 붙을 곳 - 퍼즐 패널 자체이며, 그래야 그 패널만
    // 덮게 된다.
    public PuzzleHud(
        Transform hudParent,
        Transform bannerParent,
        IntEventChannel scoreChangedChannel,
        VoidEventChannel orderClearedChannel,
        VoidEventChannel gameOverChannel,
        VoidEventChannel advanceRequestedChannel)
    {
        _advanceRequestedChannel = advanceRequestedChannel;

        RectTransform hudRect = CreateHudBar(hudParent);
        CreateFillBackground(hudRect);

        _scoreLabel = CreateHudLabel(hudRect, "ScoreLabel", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f), TextAlignmentOptions.Midline);

        _completeBanner = CreateCompleteBanner(bannerParent);
        _gameOverBanner = CreateBanner(bannerParent, "GameOverBanner", "게임 오버");

        scoreChangedChannel.OnRaised += UpdateScore;
        orderClearedChannel.OnRaised += ShowComplete;
        gameOverChannel.OnRaised += ShowGameOver;
        advanceRequestedChannel.OnRaised += HideComplete;
    }

    private void UpdateScore(int score)
    {
        _scoreLabel.text = $"점수: {score}";
    }

    private void ShowComplete()
    {
        _completeBanner.SetActive(true);
    }

    private void HideComplete()
    {
        _completeBanner.SetActive(false);
    }

    private void ShowGameOver()
    {
        _gameOverBanner.SetActive(true);
    }

    private static RectTransform CreateHudBar(Transform parent)
    {
        GameObject hudObject = new GameObject("HudBar", typeof(RectTransform));
        hudObject.transform.SetParent(parent, false);

        RectTransform hudRect = hudObject.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0.0f, 1.0f);
        hudRect.anchorMax = new Vector2(1.0f, 1.0f);
        hudRect.pivot = new Vector2(0.5f, 1.0f);
        hudRect.sizeDelta = new Vector2(0.0f, 60.0f);
        hudRect.anchoredPosition = Vector2.zero;
        return hudRect;
    }

    // targetRect를 채우는 어둡고 반투명한 패널 - 정확히 맞춰 늘어난 직계 자식이다. 자식들이
    // 파괴/재생성되지 않는 HUD 바에 사용된다.
    private static void CreateFillBackground(RectTransform targetRect)
    {
        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(targetRect, false);
        backgroundObject.transform.SetAsFirstSibling();

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        backgroundObject.GetComponent<Image>().color = PanelBackgroundColor;
    }

    private static TextMeshProUGUI CreateHudLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(16.0f, 0.0f);
        rect.offsetMax = new Vector2(-16.0f, 0.0f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = alignment;
        label.fontSize = 32.0f;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        return label;
    }

    // Clear! 배너와 그 아래의 "다음 스테이지로" 버튼 - 아직 별도의 노트/요약 화면이 없으므로
    // (Match3Controller 참고) 현재로서는 이것이 진행하는 유일한 방법이다.
    private GameObject CreateCompleteBanner(Transform parent)
    {
        GameObject bannerObject = new GameObject("CompleteBanner", typeof(RectTransform));
        bannerObject.transform.SetParent(parent, false);

        RectTransform rect = bannerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimObject.transform.SetParent(bannerObject.transform, false);
        RectTransform dimRect = dimObject.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        dimObject.GetComponent<Image>().color = PanelBackgroundColor;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(bannerObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.0f, 0.55f);
        labelRect.anchorMax = new Vector2(1.0f, 0.85f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "클리어!";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 48.0f;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;

        GameObject buttonObject = new GameObject("AdvanceButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(bannerObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, 0.3f);
        buttonRect.anchorMax = new Vector2(0.7f, 0.45f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        buttonObject.GetComponent<Image>().color = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        buttonObject.GetComponent<Button>().onClick.AddListener(() => _advanceRequestedChannel.Raise());

        GameObject buttonLabelObject = new GameObject("Label", typeof(RectTransform));
        buttonLabelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform buttonLabelRect = buttonLabelObject.GetComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonLabel = buttonLabelObject.AddComponent<TextMeshProUGUI>();
        buttonLabel.text = "다음 스테이지로";
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.fontSize = 26.0f;
        buttonLabel.color = Color.black;

        bannerObject.SetActive(false);
        return bannerObject;
    }

    private static GameObject CreateBanner(Transform parent, string name, string text)
    {
        GameObject bannerObject = new GameObject(name, typeof(RectTransform));
        bannerObject.transform.SetParent(parent, false);

        RectTransform rect = bannerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = bannerObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 48.0f;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;

        bannerObject.SetActive(false);
        return bannerObject;
    }
}
