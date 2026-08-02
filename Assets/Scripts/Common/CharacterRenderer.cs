using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class CharacterRenderer : MonoBehaviour
{
    private Animator _animator;
    private float _baseScaleX; // 원래(에디터에서 지정된) X 스케일의 절대값
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");

    [SerializeField]
    private ParticleSystem footStepEffect;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        // 에디터에서 지정된 원래 X 스케일 크기를 저장 (부호는 항상 양수로 캐싱)
        _baseScaleX = Mathf.Abs(transform.localScale.x);
    }

    public void OnMovement(float speed)
    {
        _animator.SetFloat(SpeedHash, speed);
    }

    // 0 = 좌우/정면(South), 1 = 후면(North)
    public void SetDirection(int direction)
    {
        _animator.SetInteger(DirectionHash, direction);
    }

    public void SetSitting(bool isSitting)
    {
        _animator.SetBool(IsSittingHash, isSitting);
    }

    // SpriteRenderer 컴포넌트의 Flip을 이용해 이미지를 반전했을 때
    // 화면에 출력되는 이미지 자체는 반전되기 때문에
    // 플레이어의 전방 특정 위치에서 발사체를 생성하는 것과 같이
    // 방향 전환이 필요할 때는 Transform.Scale.x를 -1, 1과 같이 설정
    public void SpriteFlipX(bool isFlipped)
    {
        Vector3 currentScale = transform.localScale;

        // 크기(magnitude)는 원래 값을 유지하고 부호만 바꿔서 좌우 반전
        currentScale.x = isFlipped ? -_baseScaleX : _baseScaleX;
        transform.localScale = currentScale;

        // 파티클 시스템은 부모의 음수 스케일(반전)을 방향 계산에 반영하지 않으므로
        // footStepEffect 자체를 Y축으로 180도 회전시켜 방향을 직접 뒤집어줌
        if (footStepEffect)
        {
            footStepEffect.transform.localRotation = Quaternion.Euler(0.0f, isFlipped ? 180.0f : 0.0f, 0.0f);
        }
    }

    public void OnFootStepEffect(bool isMoved)
    {
        if (!footStepEffect)
        {
            return;
        }
    
        ParticleSystem.EmissionModule emission = footStepEffect.emission;
        emission.rateOverTime = isMoved ? 20.0f : 0.0f;
    }
}
