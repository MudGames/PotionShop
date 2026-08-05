using UnityEngine;
using UnityEngine.UI;

// 특수 타일 배지 뒤의 후광(TileView._specialGlow)에 붙어, 활성화돼 있는 동안 반짝반짝 빛나는 느낌을
// 준다(2026-08-05, "왜 밝은 느낌이 안나나요"/"반짝반짝 느낌이 나야 합니다" 피드백으로 재설계).
// 배지 자신의 알파를 낮추던 이전 방식은 배지 뒤가 어두운 고정 배경(BoardView.TileBackgroundColor)
// 이라 알파를 낮출수록 그 어두운 배경이 비쳐 보여 오히려 "어두워지는" 것처럼 보였다. 대신 배지 뒤에
// 별도의 밝은 후광 스프라이트를 얹고, 그 후광이 알파 0(안 보임)에서 밝은 값으로 반짝 나타났다가
// 사그라드는 식으로 반복하게 한다 - 뭔가 추가로 밝은 게 나타나는 것이므로 진짜 빛나는 느낌이 난다.
// ButtonHoverAnimator와 같은 패턴으로, 호스트 코루틴 연결 없이 자기 Update()로 완결되는 독립
// 컴포넌트라서 후광이 SetActive(false)/true)로 재사용(풀링)돼도 Unity가 알아서 Update 호출을
// 멈추고 재개해준다.
public sealed class SpecialBadgePulse : MonoBehaviour
{
    // 가장 어두울 때(트로프)도 테두리가 잘 보여야 한다(2026-08-05, "가장 작을 때가 너무 어둡다"
    // 피드백) - 트로프 알파를 너무 낮게 잡으면 반짝일 때만 잠깐 보이고 나머지 시간엔 안 보이는
    // 것처럼 느껴진다. "밝기도 좀 더 밝으면 좋겠습니다" 후속 피드백으로 트로프를 한 번 더 올렸다
    // (0.5 -> 0.6) - 애디티브 블렌드 셰이더로 알파 한계를 넘어보려 했으나(UI Image가 셰이더가
    // 기대하는 텍스처 채널과 안 맞아 모양이 깨지고 엉뚱한 색으로 나옴) 위험 대비 이득이 낮아
    // 포기하고, 안전한 방법(따뜻한 색조 + 트로프 상향 + 밝은 구간을 넓히는 커브)으로 밝기를 올렸다.
    private const float MinAlpha = 0.6f;
    private const float MaxAlpha = 1f;
    private const float MinScale = 0.95f;
    private const float MaxScale = 1.2f;
    private const float FlashPeriod = 1.0f;

    // 흰색보다 따뜻한 금빛 색조가 어두운 보라색 배경과 대비돼 더 밝고 화사하게 읽힌다.
    private static readonly Color WarmTint = new Color(1f, 0.86f, 0.5f);

    // 사인파를 그대로 쓰면 은은하게 숨쉬는 느낌이라 "반짝"이 아니라 "은은함"으로 읽힌다. 0 밑을
    // 잘라내고 거듭제곱으로 뾰족하게 만들면 짧게 확 밝아졌다가 금방 가라앉는 스파클 모양이 나온다.
    // 너무 뾰족하면(3) 밝은 구간이 짧아 전체적으로 어두워 보이므로 2로 완화해 밝은 구간을 넓혔다.
    private const float FlashSharpness = 2f;

    private Image _image;
    private Transform _badgeTransform;
    private float _t;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    // 배지는 생성자 시점에 아직 없을 수 있어(TileView 참고) 나중에 주입받는다. 테두리만 혼자
    // 커졌다 작아지면 안의 물약이 따로 노는 것처럼 보여서(2026-08-05, "물약도 같이 커지는건
    // 이상할까요?" 논의), 배지도 같이 커지게 한다. 처음엔 배지 폭을 테두리보다 좁게 뒀는데,
    // "같은 비율로 커지면 더 동적일 것 같다"는 피드백으로 테두리와 동일한 폭으로 맞췄다(Apply
    // 참고).
    public void SetBadgeTransform(Transform badgeTransform)
    {
        _badgeTransform = badgeTransform;
    }

    // 풀 재사용으로 반복 활성화될 때마다 항상 같은 지점(안 보임)에서 시작해, 이전 상태가
    // 이어져 드리프트되지 않게 한다.
    private void OnEnable()
    {
        _t = 0f;
        Apply(0f);
    }

    private void OnDisable()
    {
        Apply(0f);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float phase = (_t % FlashPeriod) / FlashPeriod;
        float wave = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f)), FlashSharpness);
        Apply(wave);
    }

    private void Apply(float wave)
    {
        float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, wave);
        _image.color = new Color(WarmTint.r, WarmTint.g, WarmTint.b, alpha);

        float scale = Mathf.Lerp(MinScale, MaxScale, wave);
        transform.localScale = new Vector3(scale, scale, 1f);

        if (_badgeTransform != null)
        {
            _badgeTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
