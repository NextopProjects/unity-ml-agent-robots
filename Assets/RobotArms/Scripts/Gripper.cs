using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 집게. (5장) 하는 일은 셋뿐이다.
    ///   ① 팔 끝을 따라간다   ② 손가락을 여닫는다   ③ 닿았는지 알려준다
    ///
    /// 4장의 자석과 결정적으로 다른 점:
    ///   자석 — 가까이 가면 붙는다. "잡았다"는 프로그램이 정해둔 상태값이다.
    ///   집게 — <b>손가락이 실제로 눌러서 마찰로 버틴다.</b> 붙드는 코드가 없다.
    ///
    /// 왜 이렇게 만드는지는 Docs/05-그리퍼로-교체.md 를 본다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Gripper : MonoBehaviour
    {
        [Header("연결")]
        public Transform follow;        // 팔 끝 (Tip)
        public Transform yawReference;  // 받침대 (Base)
        public Transform target;        // 옮길 물체 (Object)
        public HingeJoint fingerL;
        public HingeJoint fingerR;

        [Header("쥐는 힘")]
        public float gripSpeed = 90f;   // 여닫는 속도(도/초). 빠르면 물체를 날려버린다
        public float gripForce = 4f;    // 쥐는 힘(N·m). 세면 튕겨나간다
        public float gripRange = 65f;   // 활짝 벌림 ~ 완전히 오므림 각도
        public float gripDepth = 0.22f; // 손목에서 손가락 사이까지

        [Header("쉬는 자세")]
        // 에피소드마다 손가락을 이 자리로 되돌린다.
        // ★ "지금 자세를 기억해 두었다가 되돌리기"로 만들면 안 된다.
        //   씬을 저장할 때 손가락이 오므려져 있었다면 그 자세가 쉬는 자세가 된다.
        public float fingerOffset = 0.17f;  // 손목 중심 ~ 경첩 (좌우)
        public float openAngle = 50f;       // 이만큼 벌어진 상태가 쉬는 자세다

        const float ContactMargin = 0.01f;  // 표면이 맞닿은 상태를 잡기 위한 여유

        Rigidbody _body, _bodyL, _bodyR;
        BoxCollider[] _boxL, _boxR;
        Collider _targetCollider;
        readonly Collider[] _hits = new Collider[8];

        /// <summary>손가락 사이 한가운데. 물체를 가져다 대야 하는 자리다.</summary>
        public Vector3 GripCenter => transform.position + transform.forward * gripDepth;

        /// <summary>0 = 활짝 벌림, 1 = 완전히 오므림. 물고 있으면 중간에서 멈춘다.</summary>
        public float GripValue => (Angle(fingerL, 1f) + Angle(fingerR, -1f)) * 0.5f;

        public bool TouchingL { get; private set; }
        public bool TouchingR { get; private set; }

        /// <summary>양쪽이 모두 닿았다 = 물고 있다.</summary>
        public bool BothTouching => TouchingL && TouchingR;

        /// <summary>손목은 <b>언제나 아래</b>를 본다. 손목 관절이 없어서 옆으로는 집을 수 없다.</summary>
        public static Quaternion AimRotation(Vector3 tip, Vector3 basePos)
        {
            Vector3 flat = tip - basePos;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            return Quaternion.LookRotation(Vector3.down, flat.normalized);
        }

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            Cache();
        }

        // ① 팔 끝을 따라간다.
        // 집게는 팔의 자식이 아니다. 자식으로 매달면 손가락이 순간이동해서
        // 물체를 누르지 못하고, 누르지 못하면 마찰도 안 생긴다.
        void FixedUpdate()
        {
            if (follow == null) return;
            _body.MovePosition(follow.position);
            _body.MoveRotation(AimRotation(follow.position, yawReference.position));
        }

        // ② 손가락을 여닫는다. "각도를 맞춰라"가 아니라 "그 방향으로 계속 밀어라".
        //    물체에 막히면 멈추고, 멈춘 채로 미는 힘이 곧 쥐는 힘이 된다.
        public void Drive(bool close)
        {
            Motor(fingerL, close ? 1f : -1f);
            Motor(fingerR, close ? -1f : 1f);   // 마주 보므로 방향이 반대다
        }

        void Motor(HingeJoint h, float direction)
        {
            if (h == null) return;
            var m = h.motor;
            m.targetVelocity = direction * gripSpeed;
            m.force = gripForce;
            h.motor = m;
            h.useMotor = true;
        }

        // ③ 닿았는지 알려준다.
        //    잠든 Rigidbody는 충돌 이벤트를 보내지 않으므로 매 스텝 직접 검사한다.
        public void UpdateContacts()
        {
            if (_targetCollider == null && target != null)
                _targetCollider = target.GetComponent<Collider>();

            TouchingL = Overlaps(_boxL);
            TouchingR = Overlaps(_boxR);
        }

        bool Overlaps(BoxCollider[] boxes)
        {
            if (_targetCollider == null || boxes == null) return false;

            foreach (var box in boxes)
            {
                var t = box.transform;
                Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f
                               + Vector3.one * ContactMargin;

                int n = Physics.OverlapBoxNonAlloc(t.TransformPoint(box.center), half,
                    _hits, t.rotation, ~0, QueryTriggerInteraction.Collide);

                for (int i = 0; i < n; i++)
                    if (_hits[i] == _targetCollider) return true;
            }
            return false;
        }

        /// <summary>에피소드 시작 — 팔 끝 자리로 되돌리고 활짝 벌린다.</summary>
        public void ResetPose()
        {
            if (follow == null) return;
            if (_bodyL == null) Cache();    // 에디터에서 집게를 다시 만들었을 때

            transform.SetPositionAndRotation(
                follow.position, AimRotation(follow.position, yawReference.position));

            Restore(_bodyL, -fingerOffset, -openAngle);
            Restore(_bodyR, +fingerOffset, +openAngle);

            Drive(false);
            TouchingL = TouchingR = false;
            Physics.SyncTransforms();   // 손으로 옮겼으니 물리 엔진에 알려준다
        }

        void Cache()
        {
            if (fingerL == null || fingerR == null) return;

            _bodyL = fingerL.GetComponent<Rigidbody>();
            _bodyR = fingerR.GetComponent<Rigidbody>();
            _boxL = fingerL.GetComponentsInChildren<BoxCollider>();
            _boxR = fingerR.GetComponentsInChildren<BoxCollider>();
        }

        /// <summary>손가락 하나를 손목 기준 쉬는 자리로 옮긴다.</summary>
        void Restore(Rigidbody rb, float sideOffset, float angle)
        {
            if (rb == null) return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.transform.SetPositionAndRotation(
                transform.TransformPoint(new Vector3(sideOffset, 0f, 0f)),
                transform.rotation * Quaternion.AngleAxis(angle, Vector3.up));
        }

        // 경첩 설정이 잘못되면 물리 엔진이 NaN을 돌려준다. 관측에 넣기 전에 막는다.
        float Angle(HingeJoint h, float sign)
        {
            if (h == null) return 0f;
            float t = h.angle * sign / Mathf.Max(1f, gripRange);
            return float.IsNaN(t) ? 0f : Mathf.Clamp01(t);
        }
    }
}
