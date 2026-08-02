using UnityEngine;

// 씬을 넘나들며 유지되는(DontDestroyOnLoad) 싱글톤 오디오 매니저 - GameManager와 동일한 싱글톤
// 패턴이다. 메뉴 씬에 하나만 배치해두면 BGM과 퍼즐 SFX를 전부 여기서 재생하며, 메뉴에서 고른
// BGM 트랙은 퍼즐 씬으로 넘어가도 끊기거나 다시 시작하지 않는다(07-sound.md 참고). 게임 규칙은
// 전혀 모르고, 호출자(Match3Controller/PuzzleEffectController)가 "이 소리를 지금 틀어라"라고
// 알려주는 시점만 그대로 따른다.
[RequireComponent(typeof(AudioSource))]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField]
    private AudioClip[] bgmClips = new AudioClip[0];
    [SerializeField]
    [Range(0f, 1f)]
    private float bgmVolume = 0.5f;

    // SFX (07-sound.md 트리거 표 참고). AudioSource.PlayOneShot 수준의 단순 재생.
    [Header("SFX")]
    [SerializeField]
    private AudioClip tileSelectClip;
    [SerializeField]
    private AudioClip matchClip;
    [SerializeField]
    private AudioClip swapFailClip;
    [SerializeField]
    private AudioClip cascadeClip;
    [SerializeField]
    private AudioClip roundEndClip;

    private AudioSource _bgmSource;
    private AudioSource _sfxOneShotSource;

    // 캐스케이드 전용 소스 - "겹치면 최근 것만 들리게"(07-sound.md)를 만족시키려면 PlayOneShot이
    // 아니라 Stop() 후 Play()로 이전 재생을 끊어야 하므로, 다른 SFX와는 별도 소스가 필요하다.
    private AudioSource _cascadeSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = GetComponent<AudioSource>();
        _sfxOneShotSource = gameObject.AddComponent<AudioSource>();
        _sfxOneShotSource.playOnAwake = false;
        _cascadeSource = gameObject.AddComponent<AudioSource>();
        _cascadeSource.playOnAwake = false;

        if (bgmClips.Length == 0)
        {
            return;
        }

        _bgmSource.clip = bgmClips[Random.Range(0, bgmClips.Length)];
        _bgmSource.loop = true;
        _bgmSource.volume = bgmVolume;
        _bgmSource.playOnAwake = false;
        _bgmSource.Play();
    }

    public void PlayTileSelect()
    {
        PlayOneShot(tileSelectClip);
    }

    // 캐스케이드 첫 스텝(스왑 자체가 만든 매치) - PuzzleEffectController.PlayCascadeRoutine 참고.
    public void PlayMatch()
    {
        PlayOneShot(matchClip);
    }

    public void PlaySwapFail()
    {
        PlayOneShot(swapFailClip);
    }

    public void PlayRoundEnd()
    {
        PlayOneShot(roundEndClip);
    }

    // 캐스케이드 두 번째 스텝부터(연쇄 매치) 매번 호출된다. 이미 재생 중이면 끊고 다시 재생해
    // "겹치면 최근 것만" 요건을 만족한다.
    public void PlayCascade()
    {
        if (cascadeClip == null)
        {
            return;
        }

        _cascadeSource.Stop();
        _cascadeSource.clip = cascadeClip;
        _cascadeSource.Play();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        _sfxOneShotSource.PlayOneShot(clip);
    }
}
