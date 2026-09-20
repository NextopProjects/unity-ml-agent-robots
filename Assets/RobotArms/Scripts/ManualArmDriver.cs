using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 키보드로 팔을 움직인다. (챕터 2 전용)
    ///
    /// 챕터 3에서 AI가 들어오면 이 스크립트는 **꺼도 된다.**
    /// 하는 일이 <see cref="RobotArmAgent.OnActionReceived"/>와 똑같기 때문이다.
    /// 다른 점은 숫자를 키보드에서 읽느냐, 신경망에서 읽느냐 하나뿐이다.
    ///
    /// 이 단계의 목적은 딱 하나다:
    ///   <b>사람이 직접 물체까지 팔을 가져갈 수 있는지 확인하는 것.</b>
    ///   사람이 못 하면 AI도 못 한다.
    /// </summary>
    [RequireComponent(typeof(RobotArmController))]
    public class ManualArmDriver : MonoBehaviour
    {
        RobotArmController _arm;

        [Header("확인용")]
        [Tooltip("팔 끝과 이 오브젝트 사이의 거리를 Console에 찍는다. 비워두면 안 찍는다.")]
        public Transform distanceTo;

        [Tooltip("이 거리 안으로 들어오면 '도달!' 이라고 알려준다.")]
        public float successDistance = 0.25f;

        bool _reported;

        void Awake()
        {
            _arm = GetComponent<RobotArmController>();
        }

        // 물리와 같은 주기로 돌린다.
        // ML-Agents도 FixedUpdate 주기로 판단하므로, 챕터 3과 감각이 같아진다.
        void Update()
        {
            // 자세 초기화는 누른 "순간"을 잡아야 해서 Update에서 읽는다.
            // FixedUpdate는 프레임당 여러 번 돌 수도, 한 번도 안 돌 수도 있다.
            if (RobotArmInput.ResetPressed) _arm.ResetPose();
        }

        void FixedUpdate()
        {
            for (int i = 0; i < _arm.JointCount; i++)
            {
                _arm.Drive(i, RobotArmInput.Joint(i), Time.fixedDeltaTime);
            }

            ReportDistance();
        }

        void ReportDistance()
        {
            if (distanceTo == null) return;

            float d = Vector3.Distance(_arm.TipPosition, distanceTo.position);

            if (d < successDistance && !_reported)
            {
                Debug.Log($"[RobotArms] 도달! 거리 {d:F3}");
                _reported = true;
            }
            else if (d >= successDistance)
            {
                _reported = false;
            }
        }
    }
}
