using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Puzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스테이지의 주문이 클리어될 때 퍼즐 보드 위에 표시되는 오버레이 패널: 어두운 배경 위에 화면 밖에서
// 슬라이드해 올라오는 페이지(제목/플레이버 텍스트/누적 목차/진행 버튼)로 구성된다. 전적으로 퍼즐
// 씬 내부에 존재한다 - Match3Controller가 인스턴스 하나를 소유하며 주문이 완료되면 Show()를
// 호출하고, 플레이어가 버튼으로 닫으면 AdvanceRequested가 발생한다.
public sealed class NoteController
{
    private const float SlideDuration = 0.35f;
    private const float OffScreenOffset = 2000.0f;

    private readonly MonoBehaviour _coroutineHost;
    private readonly GameObject _root;
    private readonly RectTransform _pageRect;
    private readonly TextMeshProUGUI _titleLabel;
    private readonly TextMeshProUGUI _flavorLabel;
    private readonly TextMeshProUGUI _tableOfContentsLabel;

    private Coroutine _slideRoutine;

    public event Action AdvanceRequested;

    // parent: 퍼즐 Canvas. 이렇게 해야 퍼즐 패널 자체가 어디 있든 상관없이 어두운 배경이 화면
    // 전체를 덮는다. panelBackgroundSprite: PuzzlePanel이 쓰는 것과 같은 장식용 테두리
    // (Menu.png) - 선택 사항이며, 테두리 없는 밋밋한 페이지를 원하면 null로 둘 것.
    public NoteController(MonoBehaviour coroutineHost, Transform parent, Sprite panelBackgroundSprite)
    {
        _coroutineHost = coroutineHost;

        _root = CreateRoot(parent);
        CreateDimBackground(_root.transform);
        _pageRect = CreatePagePanel(_root.transform, panelBackgroundSprite);

        _titleLabel = CreateLabel(_pageRect, "TitleLabel", new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.92f), 44.0f, FontStyles.Bold);
        _flavorLabel = CreateLabel(_pageRect, "FlavorLabel", new Vector2(0.12f, 0.6f), new Vector2(0.88f, 0.78f), 28.0f, FontStyles.Italic);
        _tableOfContentsLabel = CreateLabel(_pageRect, "TableOfContentsLabel", new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.58f), 24.0f, FontStyles.Normal);
        _tableOfContentsLabel.alignment = TextAlignmentOptions.TopLeft;

        CreateAdvanceButton(_pageRect);

        _root.SetActive(false);
    }

    public void Show(LevelData completed, IReadOnlyList<string> history)
    {
        _titleLabel.text = BuildTitle(completed);
        _flavorLabel.text = completed != null ? completed.flavorText : "";
        _tableOfContentsLabel.text = BuildTableOfContents(history);

        // 활성화하기 전에 화면 밖에 위치시켜서, 최종 위치에서 한 프레임 동안 번쩍이는 현상을 막는다.
        _pageRect.anchoredPosition = new Vector2(0.0f, OffScreenOffset);
        _root.SetActive(true);
        RestartSlide(OffScreenOffset, 0.0f, null);
    }

    private void Hide()
    {
        RestartSlide(0.0f, OffScreenOffset, () => _root.SetActive(false));
    }

    private void RestartSlide(float fromOffset, float toOffset, Action onComplete)
    {
        if (_slideRoutine != null)
        {
            _coroutineHost.StopCoroutine(_slideRoutine);
        }

        _slideRoutine = _coroutineHost.StartCoroutine(SlideRoutine(fromOffset, toOffset, onComplete));
    }

    private IEnumerator SlideRoutine(float fromOffset, float toOffset, Action onComplete)
    {
        float elapsed = 0.0f;
        while (elapsed < SlideDuration)
        {
            elapsed += Time.deltaTime;
            float y = Mathf.SmoothStep(fromOffset, toOffset, elapsed / SlideDuration);
            _pageRect.anchoredPosition = new Vector2(0.0f, y);
            yield return null;
        }

        _pageRect.anchoredPosition = new Vector2(0.0f, toOffset);
        onComplete?.Invoke();
    }

    private static GameObject CreateRoot(Transform parent)
    {
        GameObject rootObject = new GameObject("NotePanel", typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);

        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rootObject;
    }

    private static void CreateDimBackground(Transform parent)
    {
        GameObject dimObject = new GameObject("DimBackground", typeof(RectTransform), typeof(Image));
        dimObject.transform.SetParent(parent, false);

        RectTransform rect = dimObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        dimObject.GetComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    }

    private static RectTransform CreatePagePanel(Transform parent, Sprite panelBackgroundSprite)
    {
        GameObject pageObject = new GameObject("PagePanel", typeof(RectTransform));
        pageObject.transform.SetParent(parent, false);

        RectTransform rect = pageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0.1f);
        rect.anchorMax = new Vector2(0.85f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (panelBackgroundSprite != null)
        {
            GameObject backgroundObject = new GameObject("PageBackground", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(pageObject.transform, false);

            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image image = backgroundObject.GetComponent<Image>();
            image.sprite = panelBackgroundSprite;
            image.type = Image.Type.Sliced;
        }

        return rect;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float fontSize, FontStyles style)
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
        label.fontStyle = style;
        label.color = Color.black;
        return label;
    }

    private void CreateAdvanceButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("AdvanceButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.35f, 0.03f);
        rect.anchorMax = new Vector2(0.65f, 0.13f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        buttonObject.GetComponent<Image>().color = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        buttonObject.GetComponent<Button>().onClick.AddListener(OnAdvanceButtonClicked);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "다음 스테이지로";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28.0f;
        label.color = Color.black;
    }

    private void OnAdvanceButtonClicked()
    {
        Hide();
        AdvanceRequested?.Invoke();
    }

    private static string BuildTitle(LevelData completed)
    {
        return !string.IsNullOrEmpty(completed?.title) ? completed.title : "물약이 완성되었습니다!";
    }

    // 노트의 목차: 이번 세션에서 지금까지 클리어한 모든 스테이지 제목을 오래된 순서대로 나열한다.
    // 이렇게 하면 항상 최신 결과만 보여주는 대신, 플레이할수록 페이지가 눈에 띄게 채워진다.
    private static string BuildTableOfContents(IReadOnlyList<string> history)
    {
        if (history == null || history.Count == 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < history.Count; i++)
        {
            builder.AppendLine($"- {history[i]}");
        }

        return builder.ToString();
    }
}
