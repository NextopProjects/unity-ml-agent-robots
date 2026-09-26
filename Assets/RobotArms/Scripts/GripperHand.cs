using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 집게를 팔 끝에 "따라가게" 한다. (5장)
    ///
    /// ── 왜 이런 게 필요한가 ─────────────────────────────────────────
    ///
    /// 5장에서 팔은 <b>그대로 둔다.</b> 1~2장에서 만든 대로
    /// <see cref="RobotArmController"/>가 Transform을 직접 돌린다.
    /// 바뀌는 것은 <b>집는 방식</b> 하나뿐이다.
    ///
    /// 그런데 이 둘은 서로 다른 세계에 산다.
    ///
    ///   팔     — Transform을 직접 대입한다. 물리 엔진은 팔의 존재를 모른다.
    ///   집게   — Rigidbody다. 물리 엔진이 위치를 정한다.
    ///
    /// 그래서 집게를 팔 끝(Tip)의 <b>자식으로 매달면 안 된다.</b>
    /// 부모의 Transform이 움직이면 자식도 같이 끌려가는데,
    /// 그 이동은 물리 엔진 입장에서 <b>순간이동</b>이다.
    /// 순간이동하는 손가락은 물체를 "누르지" 못한다. 그냥 통과했다 나타났다 한다.
    /// 마찰은 <b>맞닿은 채로 미는 힘</b>에서 나오므로, 그러면 절대 잡히지 않는다.
    ///
    /// 그래서 집게는 팔 <b>바깥</b>에 두고, 매 물리 스텝마다
    /// <see cref="Rigidbody.MovePosition"/>으로 팔 끝 자리를 쫓아가게 한다.
    /// MovePosition은 순간이동이 아니라 <b>"이번 스텝 동안 여기까지 움직여라"</b>는
    /// 주문이다. 물리 엔진이 속도를 계산해서 옮기므로,
    /// 손에 물린 물체도 같은 속도로 함께 딸려간다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GripperHand : MonoBehaviour
    {
        [Tooltip("따라갈 대상. 팔 끝(Tip)을 넣는다.")]
        public Transform follow;

        [Tooltip("좌우 방향의 기준. 받침대(Base)를 넣는다.")]
        public Transform yawReference;

        [Tooltip("이 손에 매달린 손가락들. 자세를 되돌릴 때 같이 옮긴다.")]
        public Rigidbody[] fingers = new Rigidbody[0];

        Rigidbody _body;

        // 손 기준으로 본 손가락들의 처음 자리.
        // 에피소드를 다시 시작할 때 이 자리로 되돌린다.
        Vector3[] _fingerLocalPositions;
        Quaternion[] _fingerLocalRotations;

        /// <summary>
        /// 손이 향할 방향 — <b>언제나 똑바로 아래.</b>
        ///
        /// ── 왜 팔 끝의 방향을 그대로 쓰지 않는가 ─────────────────────
        ///
        /// 이 팔에는 <b>손목 관절이 없다.</b> 어깨·팔꿈치·좌우 회전 셋뿐이다.
        /// 그래서 팔 끝의 기울기는 우리가 정하는 게 아니라
        /// 팔뚝이 어디를 향하느냐에 따라 <b>끌려서 정해진다.</b>
        ///
        /// 실제로 계산해보면 이렇다.
        ///   테이블 위(높이 0.1)의 물체에 닿으려면
        ///   팔뚝은 약 <b>62도 아래로 꽂히는</b> 자세밖에 없다.
        ///   그 방향으로 손가락(길이 0.22)을 뻗으면 끝이 테이블 <b>아래</b>로 들어간다.
        ///
        /// 즉 <b>지금 구조로는 옆에서 집는 것이 기하학적으로 불가능하다.</b>
        /// 관절을 하나 더 다는 대신, 손목을 항상 아래로 고정해서
        /// <b>위에서 내려 집는</b> 방식으로 바꾼다.
        /// (실제 로봇도 이 문제 때문에 손목 축을 둔다)
        ///
        /// 좌우 방향만 팔을 따라 돌려서, 집게가 늘 물체 쪽을 보게 한다.
        /// </summary>
        public static Quaternion AimRotation(Vector3 tipPosition, Vector3 basePosition)
        {
            Vector3 flat = tipPosition - basePosition;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;

            // 앞(forward)이 아래를 보고, 위(up)가 팔이 뻗은 수평 방향을 본다.
            return Quaternion.LookRotation(Vector3.down, flat.normalized);
        }

        Quaternion TargetRotation =>
            AimRotation(follow.position, yawReference != null ? yawReference.position : Vector3.zero);

        void Awake()
        {
            _body = GetComponent<Rigidbody>();

            // 손은 물리에 떠밀리지 않는다. 팔이 시키는 대로만 움직인다.
            _body.isKinematic = true;
            _body.useGravity = false;

            CacheFingerPoses();
        }

        void CacheFingerPoses()
        {
            _fingerLocalPositions = new Vector3[fingers.Length];
            _fingerLocalRotations = new Quaternion[fingers.Length];

            for (int i = 0; i < fingers.Length; i++)
            {
                if (fingers[i] == null) continue;

                _fingerLocalPositions[i] = transform.InverseTransformPoint(fingers[i].position);
                _fingerLocalRotations[i] = Quaternion.Inverse(transform.rotation) * fingers[i].rotation;
            }
        }

        void FixedUpdate()
        {
            if (follow == null) return;

            // 핵심 두 줄.
            // transform.position = ... 으로 대입하면 순간이동이 되어
            // 물린 물체가 손에서 빠진다. 반드시 MovePosition / MoveRotation을 쓴다.
            _body.MovePosition(follow.position);
            _body.MoveRotation(TargetRotation);
        }

        /// <summary>
        /// 에피소드를 다시 시작할 때, 손과 손가락을 팔 끝 자리로 즉시 옮긴다.
        ///
        /// 여기서만은 순간이동이 맞다. 새 에피소드는 "없던 일로 하고 다시"이므로
        /// 중간 과정이 물리적으로 그럴듯할 필요가 없다.
        /// </summary>
        public void Snap()
        {
            if (follow == null) return;

            // 손가락 목록이 바뀌었으면 캐시를 다시 만든다.
            // (길이만 보고 넘어가면 에디터에서 집게를 다시 만들 때 터진다)
            if (_fingerLocalPositions == null || _fingerLocalPositions.Length != fingers.Length)
                CacheFingerPoses();

            transform.SetPositionAndRotation(follow.position, TargetRotation);

            for (int i = 0; i < fingers.Length; i++)
            {
                var rb = fingers[i];
                if (rb == null) continue;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                rb.transform.SetPositionAndRotation(
                    transform.TransformPoint(_fingerLocalPositions[i]),
                    transform.rotation * _fingerLocalRotations[i]);
            }

            // Transform을 손으로 옮겼으니 물리 엔진에 알려준다.
            // 이걸 빼먹으면 겹침 검사가 한 박자 늦은 위치로 계산된다.
            Physics.SyncTransforms();
        }
    }
}
