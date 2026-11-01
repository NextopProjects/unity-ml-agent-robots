using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;

namespace Humanoid
{
    // 자연스럽게 걷기. WalkAgent 의 보상("목표 쪽으로 목표 속도")은 어떻게 걷든 같은 점수라서,
    // 몸을 숙이고 발을 끌며 덜덜 떠는 걸음이 학습됐다 (실측: 몸통 33° · 1m 갈 때 발이 1.35m 미끄러짐).
    // 여기서는 보상에 "어떻게 걷는가"를 더한다. 관측 · 행동은 WalkAgent 와 같다 (51 · 18).
    //   곱하기 : 몸통 세우기 · 발 미끄러짐 · 행동 떨림 — 나쁜 만큼 점수를 깎는다. 0 밑으로는 안 내려간다
    //   더하기 : 발을 들었다 내린 순간 (떠 있던 시간)
    // 벌점을 빼기로 주면 보상이 음수가 되어 "빨리 넘어져서 판을 끝내기"가 이득이 될 수 있다. 그래서 깎기(곱하기)로 준다.
    // 판 끝은 넘어짐 · 목표 제한시간뿐이다 (Max Step 0 = 상한 없음). 목표를 맞히는 한 판이 끝나지 않는다 — 잘 걸을수록 오래 산다.
    public class GaitAgent : WalkAgent
    {
        [Header("걸음 보상 (0 = 끄기). yaml 의 environment_parameters 에 같은 이름이 있으면 그 값")]
        public float uprightWeight = 1f;        // upright_weight : 1 = 몸통 기울기만큼 그대로 깎는다
        public float slipWeight = 1f;           // slip_weight : 발 미끄러짐 1 m/s 에 점수 절반
        public float actionRateWeight = 1f;     // action_rate_weight : 행동 변화(평균 제곱) 1 에 점수 절반
        public float airTimeWeight = 5f;        // air_time_weight : 걸음마다 ±0.25 × 이 값

        [Header("물리 (걷기에서만)")]
        public float maxJointTorque = 300f;     // max_joint_torque : 관절 모터 힘 상한 N·m. 0 = 1~4장 그대로 (사실상 무제한)

        readonly float[] _air = new float[2];          // 왼발 · 오른발이 떠 있은 시간
        readonly float[] _lastActions = new float[18];
        float _actionChange;

        public override void OnEpisodeBegin()
        {
            base.OnEpisodeBegin();
            ReadSettings();
            System.Array.Clear(_lastActions, 0, _lastActions.Length);    // 몸을 되돌리면 목표도 차렷(0)이다
            _air[0] = _air[1] = 0f;
            _actionChange = 0f;
        }

        // 실험 조건을 yaml 에서 읽는다. 빌드를 다시 하지 않고도 보상 항목을 끄고 켜 볼 수 있다.
        void ReadSettings()
        {
            var p = Academy.Instance.EnvironmentParameters;
            uprightWeight = p.GetWithDefault("upright_weight", uprightWeight);
            slipWeight = p.GetWithDefault("slip_weight", slipWeight);
            actionRateWeight = p.GetWithDefault("action_rate_weight", actionRateWeight);
            airTimeWeight = p.GetWithDefault("air_time_weight", airTimeWeight);
            maxJointTorque = p.GetWithDefault("max_joint_torque", maxJointTorque);

            foreach (var part in body.GetComponentsInChildren<BodyPart>())
            {
                if (part.joint == null) continue;
                var drive = part.joint.slerpDrive;
                drive.maximumForce = maxJointTorque > 0f ? maxJointTorque : 1e6f;
                part.joint.slerpDrive = drive;
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 새 판단이 나온 스텝에만 행동이 바뀐다 (판단 사이에는 같은 행동). 바뀐 크기를 다음 판단까지 들고 간다.
            var a = actions.ContinuousActions;
            float change = 0f;
            for (int i = 0; i < _lastActions.Length; i++)
            {
                float v = Mathf.Clamp(a[i], -1f, 1f);
                change += (v - _lastActions[i]) * (v - _lastActions[i]);
                _lastActions[i] = v;
            }
            if (change > 0f) _actionChange = change / _lastActions.Length;

            base.OnActionReceived(actions);
        }

        protected override float StepReward()
        {
            float r = base.StepReward();     // 목표 쪽으로 목표 속도 (0~1) + 도달 보너스

            r *= Mathf.Lerp(1f, HumanoidMath.Upright(body.torso.transform.up), uprightWeight);
            r /= 1f + slipWeight * (body.footL.SlipSpeed + body.footR.SlipSpeed);
            r /= 1f + actionRateWeight * _actionChange;

            r += airTimeWeight * (Touchdown(0, body.footL) + Touchdown(1, body.footR));
            return r;
        }

        // 발이 떠 있는 동안 시간을 재고, 땅에 닿는 순간 점수를 준다.
        // 0.1초 안 되게 뜬 것은 걸음으로 치지 않는다 (판 시작 때 몸을 되돌린 직후 · 접촉이 한 스텝 끊긴 것).
        float Touchdown(int i, BodyPart foot)
        {
            if (!foot.touchingGround) { _air[i] += Time.fixedDeltaTime; return 0f; }
            float air = _air[i];
            _air[i] = 0f;
            return air >= 0.1f ? HumanoidMath.AirTimeScore(air) : 0f;
        }
    }
}
