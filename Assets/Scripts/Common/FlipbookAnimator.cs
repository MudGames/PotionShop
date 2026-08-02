using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FlipbookAnimator : MonoBehaviour
{
    [SerializeField]
    private Sprite[] frames;
    [SerializeField]
    private float frameRate = 8.0f;

    private SpriteRenderer _spriteRenderer;
    private float _timer;
    private int _frameIndex;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // 다른 프레임 세트(예: 걷기 vs 앉기)로 전환하고 첫 프레임부터 재생을 다시 시작한다 -
    // 하나의 SpriteFrameAnimator로 여러 애니메이션을 보여주는 호출부에서 사용한다(BlackCat이
    // 걷기와 멈춤 사이를 전환하는 부분 참고).
    public void SetFrames(Sprite[] newFrames)
    {
        if (frames == newFrames)
        {
            return;
        }

        frames = newFrames;
        _frameIndex = 0;
        _timer = 0.0f;

        if (frames != null && frames.Length > 0)
        {
            _spriteRenderer.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        _timer += Time.deltaTime;
        float frameDuration = 1.0f / frameRate;
        if (_timer < frameDuration)
        {
            return;
        }

        _timer -= frameDuration;
        _frameIndex = (_frameIndex + 1) % frames.Length;
        _spriteRenderer.sprite = frames[_frameIndex];
    }
}
