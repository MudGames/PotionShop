using UnityEngine;

[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class MovementRigidbody : MonoBehaviour
{
    private CapsuleCollider2D _collider;
    public Rigidbody2D Rigidbody { get; private set; }

    // 인스펙터에 노출하지 않음 - 이 값은 소유자(예: Witch의 wanderSpeed)가 MoveSpeed로 설정한다.
    // 여기 직접 노출하면 편집 가능해 보이지만 매 Awake마다 덮어써져 무시되므로 오히려 혼란스럽다.
    private float moveSpeed;
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

    private void Awake()
    {
        _collider = GetComponent<CapsuleCollider2D>();
        Rigidbody = GetComponent<Rigidbody2D>();
    }

    public void MoveTo(Vector2 direction)
    {
        // 불필요한 물리 계산 방지
        if (direction.sqrMagnitude < Mathf.Epsilon)
        {
            return;
        }

        // 이동량 계산
        direction.Normalize(); // 안전하게 이동 방향 정규화
        float moveDistance = moveSpeed * Time.fixedDeltaTime;
        Vector2 offset = direction * moveDistance;
            
        // 위치 이동
        Rigidbody.MovePosition(Rigidbody.position + offset);
    }
    
    public void EnableCollision()
    {
        _collider.enabled = true;
    }
        
    public void DisableCollision()
    {
        _collider.enabled = false;
    }
        
    public void DisableMovement()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }
}
