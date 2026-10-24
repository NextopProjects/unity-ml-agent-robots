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
            // 여기에 직접 구현한다.
        }

        // 관측 48개. 속도처럼 범위가 정해지지 않은 값은 yaml의 normalize: true 가 맞춰 준다.
        public override void CollectObservations(VectorSensor sensor)
        {
            // 여기에 직접 구현한다.
        } // = 48

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 주어진 코드에서 출발해 직접 완성한다.
            // TODO: 행동 18개를 몸에 보낸다

            // 판 도중에 몸통을 무작위로 민다 (주어진 코드)
            if (pushInterval > 0 && StepCount % pushInterval == pushInterval / 2)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                body.torso.rb.AddForce(new Vector3(dir.x, 0f, dir.y) * Random.Range(0f, maxPush), ForceMode.Impulse);
            }

            // TODO: 이번 스텝 보상을 더하고, 넘어졌으면 판을 끝낸다

            if (_keyboard != null) _keyboard.statusLine = Status();     // 화면에 보여줄 한 줄 (주어진 코드)
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
