using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점수 HUD 바와 Clear!/결과(게임 오버) 배너를 담당한다 - 순수한 UI 상태와 위젯 생성만 하며 게임
// 규칙은 없다. 미션(주문 진행도)과 이동 제한 표시는 이 상단 바가 아니라 퍼즐 보드 옆에 위치한
// 자체 PuzzleSidePanel에 있다. 어두운 배경 패널과 텍스트 외곽선 덕분에 뒤에 무엇이 보이든
// 가독성이 유지된다.
//
// Match3Controller/GameManager를 직접 참조하지 않는다 - ScoreChangedChannel/OrderClearedChannel을
// 구독해 스스로 갱신하고, Clear! 배너/결과 화면의 버튼은 각각 AdvanceRequestedChannel/
// RestartRequestedChannel을 Raise할 뿐이다(누가 듣는지는 알 필요 없다) - CLAUDE.md 이벤트 채널
// 아키텍처 원칙 참고. 단, 결과 화면에 표시할 최종 점수/최고 콤보는 "게임 오버 시점의 최종값"이라는
// 프레젠테이션 타이밍이 걸린 데이터라 채널이 아니라 Match3Controller가 ShowGameOver를 직접
// 호출해서 넘겨준다(클리어/게임오버 신호 자체의 타이밍 처리와 같은 이유 - Match3Controller 참고).
public sealed class PuzzleHud
{
    private static readonly Color PanelBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);

    private readonly TextMeshProUGUI _scoreLabel;
    private readonly TextMeshProUGUI _stageLabel;
    private readonly GameObject _completeBanner;
    private readonly GameObject _gameOverBanner;
    private readonly GameObject _reshuffleBanner;
    private readonly GameObject _comboPopup;
    private readonly TextMeshProUGUI _comboPopupLabel;
    private readonly TextMeshProUGUI _gameOverScoreLabel;
    private readonly TextMeshProUGUI _gameOverComboLabel;
    private readonly VoidEventChannel _advanceRequestedChannel;
    private readonly VoidEventChannel _restartRequestedChannel;

    // hudParent: HUD 바가 붙을 곳. 퍼즐 패널이 아닌 퍼즐 Canvas를 전달해야, 패널 자체의 경계와
    // 무관하게 상단에서 화면 너비 전체를 가로지른다.
    // bannerParent: Clear!/결과 화면 배너가 붙을 곳 - 퍼즐 패널 자체이며, 그래야 그 패널만
    // 덮게 된다.
    public PuzzleHud(
        Transform hudParent,
        Transform bannerParent,
        IntEventChannel scoreChangedChannel,
        VoidEventChannel orderClearedChannel,
        VoidEventChannel advanceRequestedChannel,
        VoidEventChannel restartRequestedChannel)
    {
        _advanceRequestedChannel = advanceRequestedChannel;
        _restartRequestedChannel = restartRequestedChannel;

        RectTransform hudRect = CreateHudBar(hudParent);
        CreateFillBackground(hudRect);

        _scoreLabel = CreateHudLabel(hudRect, "ScoreLabel", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f), TextAlignmentOptions.Midline);

        // 화면 중앙에 뜨는 점수 라벨과 안 겹치도록 왼쪽 구석에만 배치한다 - HudBar가 화면 폭 전체를
        // 가로지르는데(0-1), 점수 텍스트는 중앙 정렬이라 왼쪽 끝은 항상 비어있다.
        _stageLabel = CreateHudLabel(hudRect, "StageLabel", new Vector2(0.02f, 0.0f), new Vector2(0.3f, 1.0f), TextAlignmentOptions.MidlineLeft);
        _stageLabel.fontSize = 24.0f;

        _completeBanner = CreateCompleteBanner(bannerParent);
        _gameOverBanner = CreateGameOverBanner(bannerParent, out _gameOverScoreLabel, out _gameOverComboLabel);
        _reshuffleBanner = CreateReshuffleBanner(bannerParent);
        _comboPopup = CreateComboPopup(bannerParent, out _comboPopupLabel);

        scoreChangedChannel.OnRaised += UpdateScore;
        orderClearedChannel.OnRaised += ShowComplete;
        advanceRequestedChannel.OnRaised += HideComplete;
        restartRequestedChannel.OnRaised += HideGameOver;
    }

    private void UpdateScore(int score)
    {
        _scoreLabel.text = $"점수: {score}";
    }

    // 채널 대신 Match3Controller가 SetupLevel마다 직접 호출한다(레벨 설정값과 같은 이유로 -
    // PuzzleSidePanel.SetIngredientSprites 참고) - GameManager가 없는 상태(단독 씬 테스트)에서는
    // 아예 호출되지 않는다. 전체 스테이지 수와 무관하게 클리어할 때마다 계속 늘어나는 번호이므로
    // "N/전체" 형태가 아니라 숫자만 표시한다(GameManager.CurrentStageNumber 참고).
    public void UpdateStageLabel(int currentStageNumber)
    {
        _stageLabel.text = $"스테이지 {currentStageNumber}";
    }

    private void ShowComplete()
    {
        _completeBanner.SetActive(true);
    }

    private void HideComplete()
    {
        _completeBanner.SetActive(false);
    }

    // Match3Controller가 게임 오버 확정 애니메이션이 끝난 시점에 직접 호출한다(캠페인 전체 누적
    // 최종 점수 + 최고 콤보) - 결과 화면(06-ui.md §결과 화면) 요건.
    public void ShowGameOver(int finalScore, int maxCombo)
    {
        _gameOverScoreLabel.text = $"최종 점수: {finalScore}";
        _gameOverComboLabel.text = $"최고 콤보: {maxCombo}";
        _gameOverBanner.SetActive(true);
    }

    private void HideGameOver()
    {
        _gameOverBanner.SetActive(false);
    }

    // 데드락(교환 가능한 매치 없음) 감지로 GridController가 보드를 자동으로 섞었을 때
    // Match3Controller가 호출한다 - 배너들과 달리 게임을 멈추지 않는 순수 안내용이라 어두운 배경
    // 오버레이 없이 짧은 문구만 보드 위쪽에 잠깐 띄운다. 자동으로 숨기는 타이밍은
    // Match3Controller의 코루틴이 담당한다(이 클래스는 게임 규칙/타이밍을 모른다).
    public void ShowReshuffleNotice()
    {
        _reshuffleBanner.SetActive(true);
    }

    public void HideReshuffleNotice()
    {
        _reshuffleBanner.SetActive(false);
    }

    // 액션(교환/스페셜 활성화) 하나가 끝난 뒤, 그 안에서 발생한 콤보 스텝 수가 2 이상일 때만
    // Match3Controller가 호출한다(1은 그냥 매치 한 번일 뿐 "콤보"라 부를 게 없음) - 04-score-combo.md
    // 콤보 정의 참고. 자동으로 숨기는 타이밍은 ReshuffleNotice와 마찬가지로 Match3Controller의
    // 코루틴이 담당한다.
    public void ShowComboPopup(int comboCount)
    {
        _comboPopupLabel.text = $"Combo x{comboCount}!";
        _comboPopup.SetActive(true);
    }

    public void HideComboPopup()
    {
        _comboPopup.SetActive(false);
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

    // 결과 화면(게임 오버 배너): 제목 + 최종 점수 + 최고 콤보 + "다시 시작" 버튼. CreateCompleteBanner와
    // 구조는 같지만, 라벨이 하나가 아니라 세 줄(제목/점수/콤보)이고 버튼이 AdvanceRequestedChannel
    // 대신 RestartRequestedChannel을 Raise한다 - 06-ui.md "결과 화면" 요건.
    private GameObject CreateGameOverBanner(Transform parent, out TextMeshProUGUI scoreLabel, out TextMeshProUGUI comboLabel)
    {
        GameObject bannerObject = new GameObject("GameOverBanner", typeof(RectTransform));
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

        TextMeshProUGUI titleLabel = CreateGameOverLabel(bannerObject.transform, "TitleLabel", new Vector2(0.0f, 0.72f), new Vector2(1.0f, 0.9f), 48.0f);
        titleLabel.text = "게임 오버";

        scoreLabel = CreateGameOverLabel(bannerObject.transform, "ScoreLabel", new Vector2(0.0f, 0.56f), new Vector2(1.0f, 0.72f), 30.0f);
        comboLabel = CreateGameOverLabel(bannerObject.transform, "ComboLabel", new Vector2(0.0f, 0.42f), new Vector2(1.0f, 0.56f), 30.0f);

        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(bannerObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, 0.2f);
        buttonRect.anchorMax = new Vector2(0.7f, 0.35f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        buttonObject.GetComponent<Image>().color = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        buttonObject.GetComponent<Button>().onClick.AddListener(() => _restartRequestedChannel.Raise());

        GameObject buttonLabelObject = new GameObject("Label", typeof(RectTransform));
        buttonLabelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform buttonLabelRect = buttonLabelObject.GetComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonLabel = buttonLabelObject.AddComponent<TextMeshProUGUI>();
        buttonLabel.text = "다시 시작";
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.fontSize = 26.0f;
        buttonLabel.color = Color.black;

        bannerObject.SetActive(false);
        return bannerObject;
    }

    // 보드 위쪽에 짧게 뜨는 알림 배너 - Complete/GameOver 배너와 달리 어두운 배경(Dim)으로 전체를
    // 덮지 않는다. 게임이 멈춘 게 아니라 조용히 계속 진행 중이라는 걸 알려야 하기 때문이다.
    private static GameObject CreateReshuffleBanner(Transform parent)
    {
        GameObject bannerObject = new GameObject("ReshuffleBanner", typeof(RectTransform));
        bannerObject.transform.SetParent(parent, false);

        RectTransform rect = bannerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.85f);
        rect.anchorMax = new Vector2(0.95f, 0.98f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(bannerObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundObject.GetComponent<Image>().color = PanelBackgroundColor;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(bannerObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "매치 가능한 조합이 없어 보드를 섞었어요!";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24.0f;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;

        bannerObject.SetActive(false);
        return bannerObject;
    }

    // 보드 중앙에 잠깐 뜨는 콤보 알림 - ReshuffleBanner와 마찬가지로 게임을 멈추지 않으므로 어두운
    // 배경(Dim)이 없다. 축하하는 느낌을 주기 위해 금색 계열의 큰 글씨를 쓴다.
    private static GameObject CreateComboPopup(Transform parent, out TextMeshProUGUI label)
    {
        GameObject popupObject = new GameObject("ComboPopup", typeof(RectTransform));
        popupObject.transform.SetParent(parent, false);

        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.4f);
        rect.anchorMax = new Vector2(0.9f, 0.55f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(popupObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 44.0f;
        label.color = new Color(1.0f, 0.84f, 0.2f);
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;

        popupObject.SetActive(false);
        return popupObject;
    }

    private static TextMeshProUGUI CreateGameOverLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float fontSize)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        return label;
    }
}
