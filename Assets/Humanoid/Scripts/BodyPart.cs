using UnityEngine;

namespace Humanoid
{
    // 몸의 부품 하나 (골반·몸통·팔·허벅지·정강이·발).
    // 처음 자세 기억 · 바닥 접촉 기록 · 관절 각도 읽기/쓰기만 한다.
    [RequireComponent(typeof(Rigidbody))]
    public class BodyPart : MonoBehaviour
    {
        public bool isFoot;                         // 발은 바닥에 닿아도 넘어진 게 아니다

        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public ConfigurableJoint joint;   // 골반에는 없다
        [HideInInspector] public bool touchingGround;

        Vector3 _startPosition;
        Quaternion _startRotation;

        public void Init()
        {
            rb = GetComponent<Rigidbody>();
            joint = GetComponent<ConfigurableJoint>();
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;

            // 물리 반복 횟수. 씬에 저장되지 않는 값이라 실행할 때 넣는다.
            // 기본값(6)이면 관절이 하중을 못 버텨 가만히 서 있어도 2초 만에 넘어진다. (실측)
            rb.solverIterations = 30;
            rb.solverVelocityIterations = 30;
        }

        public void ResetPose()
        {
            transform.localPosition = _startPosition;
            transform.localRotation = _startRotation;
            rb.linearVelocity = Vector3.zero;      // 속도를 안 지우면 넘어지던 기세 그대로 다시 넘어진다
            rb.angularVelocity = Vector3.zero;
            touchingGround = false;
        }

        // 바닥에 닿은 점이 수평으로 미끄러지는 속도(m/s). 닿아 있지 않으면 0.
        // 닿은 점 = 상자 바닥 네 모서리 중 가장 낮은 것. 부품 중심 속도로 재면 뒤꿈치→발끝으로 구르는 정상 동작도 미끄러짐으로 잡힌다.
        public float SlipSpeed
        {
            get
            {
                if (!touchingGround) return 0f;
                var box = GetComponent<BoxCollider>();
                Vector3 lowest = transform.position;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f, new Vector3(i % 2 * 2 - 1, -1, i / 2 * 2 - 1)));
                    if (i == 0 || corner.y < lowest.y) lowest = corner;
                }
                Vector3 v = rb.GetPointVelocity(lowest);
                return new Vector2(v.x, v.z).magnitude;
            }
        }

        // Rigidbody가 없는 것(움직이지 않는 것) = 바닥으로 본다.
        void OnCollisionStay(Collision c) { if (c.rigidbody == null) touchingGround = true; }
        void OnCollisionExit(Collision c) { if (c.rigidbody == null) touchingGround = false; }

        // ── 관절 ───────────────────────────────────────────────────
        // X = 앞뒤로 굽히기, Z = 좌우로 기울이기 (Y 비틀기는 잠갔다)

        // 목표 각도(도)를 관절 모터에 넣는다. 모터가 이 각도로 당긴다.
        public void SetTarget(float x, float z)
        {
            joint.targetRotation = Quaternion.Euler(x, 0f, z);
        }

        // 지금 관절 각도(도). 오일러각은 무릎을 90도 가까이 접으면 뒤집히므로 쓰지 않고,
        // "내 위쪽이 부모 기준으로 어디를 가리키나"로 계산한다.
        // targetRotation 은 Transform 과 부호가 반대라서(실측) 여기서도 부호를 뒤집어 맞춘다.
        public Vector2 Angles
        {
            get
            {
                Vector3 up = Quaternion.Inverse(joint.connectedBody.rotation) * (rb.rotation * Vector3.up);
                return new Vector2(-Mathf.Atan2(up.z, up.y), Mathf.Asin(Mathf.Clamp(up.x, -1f, 1f))) * Mathf.Rad2Deg;
            }
        }

        // 관절이 도는 속도(라디안/초). 부모에 대한 상대 속도.
        public Vector2 Rates
        {
            get
            {
                var parent = joint.connectedBody;
                Vector3 w = Quaternion.Inverse(parent.rotation) * (rb.angularVelocity - parent.angularVelocity);
                return new Vector2(-w.x, -w.z);
            }
        }
    }
}
