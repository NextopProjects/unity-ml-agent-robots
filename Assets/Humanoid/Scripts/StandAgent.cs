using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Humanoid
{
    // 넘어지지 않고 버티기. 판 도중에 몸통을 무작위로 밀어서 "밀려도 버티는 법"을 배우게 한다.
    // 함수 구성은 RollerBall · RobotArms와 같다: OnEpisodeBegin / CollectObservations / OnActionReceived / Heuristic
    public class StandAgent : Agent
    {
        [Tooltip("몇 스텝마다 몸통을 밀지. 0이면 밀지 않는다.")]
        public int pushInterval = 150;
        public float maxPush = 20f;     // N·s. 실측: 모터만으로는 20에 넘어진다

        protected HumanoidBody body;
        KeyboardControl _keyboard;
        readonly float[] _actions = new float[18];

        public override void Initialize()
        {
            body = GetComponent<HumanoidBody>();
            _keyboard = GetComponent<KeyboardControl>();
        }

        public override void OnEpisodeBegin()
        {
            body.ResetBody();
            if (_keyboard != null) _keyboard.ResetActions();    // Heuristic일 때 이전 판의 키보드 자세가 남지 않게
        }

        // 관측 48개. 속도처럼 범위가 정해지지 않은 값은 yaml의 normalize: true 가 맞춰 준다.
        public override void CollectObservations(VectorSensor sensor)
        {
            Transform pelvis = body.pelvis.transform;
            sensor.AddObservation(pelvis.position.y - transform.position.y);                    // 1 골반 높이
            sensor.AddObservation(HumanoidMath.DownInBody(pelvis.rotation));                    // 3 기울기
            sensor.AddObservation(pelvis.InverseTransformDirection(body.pelvis.rb.linearVelocity));  // 3 속도
            sensor.AddObservation(pelvis.InverseTransformDirection(body.pelvis.rb.angularVelocity)); // 3 회전 속도
            for (int i = 0; i < body.JointCount; i++)
            {
                sensor.AddObservation(body.NormalizedAngle(i));                                 // 18 관절 각도
                sensor.AddObservation(body.Rate(i) / 10f);                                      // 18 관절 속도
            }
            sensor.AddObservation(body.footL.touchingGround);                                   // 1 왼발 접촉
            sensor.AddObservation(body.footR.touchingGround);                                   // 1 오른발 접촉
        } // = 48

        public override void OnActionReceived(ActionBuffers actions)
        {
            for (int i = 0; i < _actions.Length; i++) _actions[i] = actions.ContinuousActions[i];
            body.SetActions(_actions);

            if (pushInterval > 0 && StepCount % pushInterval == pushInterval / 2)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                body.torso.rb.AddForce(new Vector3(dir.x, 0f, dir.y) * Random.Range(0f, maxPush), ForceMode.Impulse);
            }

            AddReward(StepReward());

            // 넘어지면 판을 끝낸다. 벌점은 없다. 앞으로 받을 보상이 사라지는 것 자체가 벌이다.
            if (body.HasFallen) EndEpisode();

            if (_keyboard != null) _keyboard.statusLine = Status();
        }

        // 화면에 보여줄 한 줄.
        protected virtual string Status() =>
            string.Format("에피소드 {0}  스텝 {1}  보상 {2:F1}", CompletedEpisodes, StepCount, GetCumulativeReward());

        // 한 스텝 보상 (0~1). 서 있는 동안 매 스텝 받는다.
        protected virtual float StepReward()
        {
            Transform torso = body.torso.transform;
            return HumanoidMath.StandReward(torso.up, body.pelvis.transform.position.y - transform.position.y, 0.96f);
        }

        // 키보드가 만든 행동을 그대로 쓴다. 숫자의 출처만 바뀐다.
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var c = actionsOut.ContinuousActions;
            for (int i = 0; i < c.Length && _keyboard != null; i++) c[i] = _keyboard.actions[i];
        }
    }
}
