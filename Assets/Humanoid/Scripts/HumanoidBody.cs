using System.Collections.Generic;
using UnityEngine;

namespace Humanoid
{
    // 휴머노이드의 "몸". 키보드든 AI든 몸은 항상 이 컴포넌트를 거쳐 움직인다.
    public class HumanoidBody : MonoBehaviour
    {
        public BodyPart pelvis, torso, footL, footR;

        [Header("관절 모터 (0 = 힘없는 래그돌)")]
        public float spring;    // 목표 각도로 당기는 힘
        public float damper;    // 흔들림을 잡는 힘 (보통 spring의 1/10)

        BodyPart[] _parts;
        Vector3[] _buffer;
        float[] _masses;

        // 행동 i번이 움직이는 관절과 축. Hierarchy 위에서 아래 순서다.
        // (허리 X·Z, 왼 어깨 X·Z, 팔꿈치 X, 오른팔 …, 왼 엉덩이 X·Z, 무릎 X, 발목 X·Z, 오른다리 …)
        readonly List<BodyPart> _dofPart = new List<BodyPart>();
        readonly List<bool> _dofIsZ = new List<bool>();

        public int JointCount => _dofPart.Count;     // = 18

        void Awake()
        {
            _parts = GetComponentsInChildren<BodyPart>();    // 부모 → 자식 순서
            _buffer = new Vector3[_parts.Length];
            _masses = new float[_parts.Length];

            for (int i = 0; i < _parts.Length; i++)
            {
                var p = _parts[i];
                p.Init();
                _masses[i] = p.rb.mass;
                if (p.joint == null) continue;

                p.joint.rotationDriveMode = RotationDriveMode.Slerp;    // 모터가 X·Z를 한 번에 당기도록
                p.joint.slerpDrive = new JointDrive { positionSpring = spring, positionDamper = damper, maximumForce = 1e6f };
                _dofPart.Add(p); _dofIsZ.Add(false);
                if (p.joint.angularZMotion != ConfigurableJointMotion.Locked) { _dofPart.Add(p); _dofIsZ.Add(true); }
            }
        }

        public void ResetBody()
        {
            foreach (var p in _parts) p.ResetPose();     // 부모부터 되돌려야 자식 위치가 맞는다
            SetActions(new float[JointCount]);           // 목표도 차렷으로
        }

        // ── 무게중심 ───────────────────────────────────────────────

        public Vector3 CenterOfMass
        {
            get
            {
                for (int i = 0; i < _parts.Length; i++) _buffer[i] = _parts[i].rb.worldCenterOfMass;
                return HumanoidMath.CenterOfMass(_buffer, _masses);
            }
        }

        // 몸 전체의 속도. 무게중심과 같은 식(질량 가중 평균)을 속도에 쓴다.
        public Vector3 CenterOfMassVelocity
        {
            get
            {
                for (int i = 0; i < _parts.Length; i++) _buffer[i] = _parts[i].rb.linearVelocity;
                return HumanoidMath.CenterOfMass(_buffer, _masses);
            }
        }

        // 발이 아닌 곳이 바닥에 닿았다 = 넘어졌다.
        public bool HasFallen
        {
            get
            {
                foreach (var p in _parts) if (!p.isFoot && p.touchingGround) return true;
                return false;
            }
        }

        // ── 관절 ───────────────────────────────────────────────────

        // i번 축의 가동 범위(도). Z축(좌우)은 양쪽이 같다.
        public Vector2 Range(int i)
        {
            var j = _dofPart[i].joint;
            return _dofIsZ[i] ? new Vector2(-j.angularZLimit.limit, j.angularZLimit.limit)
                              : new Vector2(j.lowAngularXLimit.limit, j.highAngularXLimit.limit);
        }

        // 행동 18개(-1~1)를 관절 목표 각도로 바꿔 모터에 넣는다.
        public void SetActions(float[] actions)
        {
            float x = 0f, z = 0f;
            for (int i = 0; i < JointCount; i++)
            {
                Vector2 r = Range(i);
                float angle = HumanoidMath.ActionToAngle(actions[i], r.x, r.y);
                if (_dofIsZ[i]) z = angle; else x = angle;

                // 한 관절의 X·Z가 다 모이면 그 관절에 넣는다.
                if (i == JointCount - 1 || _dofPart[i + 1] != _dofPart[i])
                {
                    _dofPart[i].SetTarget(x, z);
                    x = z = 0f;
                }
            }
        }

        // i번 축의 지금 각도를 -1~1로.
        public float NormalizedAngle(int i)
        {
            Vector2 a = _dofPart[i].Angles, r = Range(i);
            return HumanoidMath.AngleToAction(_dofIsZ[i] ? a.y : a.x, r.x, r.y);
        }

        // i번 축의 지금 각도(도). 화면 표시용.
        public float Angle(int i) => _dofIsZ[i] ? _dofPart[i].Angles.y : _dofPart[i].Angles.x;

        // i번 축이 도는 속도(라디안/초).
        public float Rate(int i) => _dofIsZ[i] ? _dofPart[i].Rates.y : _dofPart[i].Rates.x;

        public string JointName(int i) => _dofPart[i].name + (_dofIsZ[i] ? " 좌우" : " 앞뒤");

        // Scene 뷰에 무게중심(노란 점)과 바닥에 비친 자리를 그린다.
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Vector3 com = CenterOfMass;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(com, 0.04f);
            Gizmos.DrawLine(com, new Vector3(com.x, transform.position.y, com.z));
        }
    }
}
