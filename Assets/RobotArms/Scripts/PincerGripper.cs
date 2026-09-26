using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 집게 그리퍼. (5장)
    ///
    /// 손가락 2개가 경첩처럼 회전하며 물체를 물어 쥔다.
    /// <b>5장에서 물리로 바뀌는 것은 이 집게뿐이다.</b>
    /// 팔의 관절은 1~2장에서 만든 그대로, Transform을 직접 돌린다.
    ///
    /// 4장의 자석과 결정적으로 다른 점:
    ///
    ///   자석 — 가까이 가면 붙는다. 물체가 팔의 자식이 되어 딸려 다닌다.
    ///          "잡았다"는 것은 프로그램이 정해둔 상태값일 뿐이다.
    ///
    ///   집게 — <b>손가락이 실제로 물체를 눌러서 마찰로 버틴다.</b>
    ///          물체는 끝까지 독립된 물리 물체다.
    ///          붙들어 두는 코드가 없다. 세게 휘두르면 빠진다.
    ///
    /// 우리가 하는 일은 "얼마나 세게 쥐라고 할지" 정하는 것뿐이고,
    /// 잡히느냐 마느냐는 물리가 정한다.
    ///
    /// 손가락에 <see cref="HingeJoint"/>를 쓰는 이유는 <b>버티기 위해서</b>다.
    /// Transform으로 손가락을 직접 오므리면 물체를 뚫고 들어가 버린다.
    /// 경첩에 모터를 달면 물체에 막혀 멈추고, 멈춘 자리에서 계속 밀어준다.
    /// 그 미는 힘이 곧 마찰의 재료다.
    /// </summary>
    public class PincerGripper : MonoBehaviour
    {
        [Header("손가락")]
        public HingeJoint fingerL;
        public HingeJoint fingerR;

        [Tooltip("손가락이 물체와 닿았는지 알려준다.")]
        public FingerContact contactL;
        public FingerContact contactR;

        [Header("쥐는 힘")]
        [Tooltip("손가락이 여닫히는 속도(도/초). 너무 빠르면 물체를 때려서 날려버린다.")]
        public float gripSpeed = 150f;

        [Tooltip("쥐는 힘(N·m). 약하면 물체가 미끄러지고, 너무 세면 튕겨나간다.")]
        public float gripForce = 5f;

        [Header("오므리는 방향")]
        [Tooltip("두 손가락은 같은 축으로 돌지만 서로 마주 보므로 방향이 반대다.\n" +
                 "왼쪽은 각도가 +로 갈 때, 오른쪽은 -로 갈 때 오므라든다.")]
        public float closeSignL = 1f;
        public float closeSignR = -1f;

        [Tooltip("완전히 오므렸을 때의 각도(도). 쥔 정도(GripValue) 계산에 쓴다.")]
        public float gripRange = 45f;

        [Header("잡는 지점")]
        [Tooltip("손목에서 이만큼 내려온 곳이 손가락 사이 한가운데다.")]
        public float gripDepth = 0.16f;

        /// <summary>
        /// 손가락 사이 한가운데. <b>여기가 물체를 가져다 대야 하는 자리다.</b>
        ///
        /// 4장까지는 팔 끝(Tip)이 곧 잡는 지점이었다.
        /// 5장에서는 집게가 팔 끝보다 아래로 더 내려와 있으므로,
        /// 목표까지의 거리도 팔 끝이 아니라 이 점으로 재야 한다.
        /// 팔 끝을 물체에 맞추면 손가락은 물체보다 아래, 테이블 속에 있게 된다.
        /// </summary>
        public Vector3 GripCenter => transform.position + transform.forward * gripDepth;

        /// <summary>지금 오므리라고 명령하고 있는가.</summary>
        public bool Closing { get; private set; }

        /// <summary>
        /// 0 = 활짝 벌림, 1 = 완전히 오므림.
        ///
        /// 이 값이 관측에 들어간다. 왜 필요한가:
        /// 물체를 문 채로 오므리면 손가락이 중간에서 멈춘다.
        /// 즉 <b>"덜 오므라들었다" = "뭔가 물고 있다"</b>는 단서가 된다.
        /// </summary>
        public float GripValue
        {
            get
            {
                if (fingerL == null || fingerR == null) return 0f;
                return (Normalized(fingerL, closeSignL) + Normalized(fingerR, closeSignR)) * 0.5f;
            }
        }

        /// <summary>양쪽 손가락이 모두 물체에 닿았는가 = 물고 있는가.</summary>
        public bool BothTouching =>
            contactL != null && contactR != null && contactL.Touching && contactR.Touching;

        float Normalized(HingeJoint h, float closeSign)
        {
            float t = h.angle * closeSign / Mathf.Max(1f, gripRange);

            // 경첩 설정이 잘못되면 물리 엔진이 NaN을 돌려준다.
            // (회전축과 가동 범위가 서로 반대 방향일 때 실제로 겪었다)
            // 그대로 관측에 넣으면 학습 전체가 망가지므로 여기서 막는다.
            return float.IsNaN(t) ? 0f : Mathf.Clamp01(t);
        }

        /// <summary>집게를 여닫는다. 액션이 매 스텝 여기로 들어온다.</summary>
        public void Drive(bool close)
        {
            Closing = close;

            SetMotor(fingerL, close, closeSignL);
            SetMotor(fingerR, close, closeSignR);
        }

        void SetMotor(HingeJoint h, bool close, float closeSign)
        {
            if (h == null) return;

            // "각도를 이렇게 만들어라"가 아니라 "이 방향으로 이만큼 힘껏 돌아라"다.
            // 물체에 막히면 거기서 멈추고, 멈춘 채로 계속 민다. 그게 쥐는 힘이 된다.
            var motor = h.motor;
            motor.targetVelocity = (close ? 1f : -1f) * gripSpeed * closeSign;
            motor.force = gripForce;
            motor.freeSpin = false;

            h.motor = motor;
            h.useMotor = true;
        }

        /// <summary>에피소드 시작 시 활짝 벌린다.</summary>
        public void ResetOpen()
        {
            Drive(false);

            if (contactL != null) contactL.ResetContact();
            if (contactR != null) contactR.ResetContact();
        }

        /// <summary>손가락이 물체에 닿았는지 다시 계산한다.</summary>
        public void UpdateContacts()
        {
            if (contactL != null) contactL.UpdateContact();
            if (contactR != null) contactR.UpdateContact();
        }

        void OnDrawGizmosSelected()
        {
            if (fingerL != null)
            {
                Gizmos.color = (contactL != null && contactL.Touching) ? Color.green : Color.gray;
                Gizmos.DrawWireCube(fingerL.transform.position, Vector3.one * 0.06f);
            }
            if (fingerR != null)
            {
                Gizmos.color = (contactR != null && contactR.Touching) ? Color.green : Color.gray;
                Gizmos.DrawWireCube(fingerR.transform.position, Vector3.one * 0.06f);
            }
        }
    }
}
