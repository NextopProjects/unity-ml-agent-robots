using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Humanoid
{
    // 목표를 향해 걷기. StandAgent에서 바뀌는 것은 셋이다.
    //   관측 +3 : 목표가 어느 쪽인가(2) · 얼마나 빨리 가야 하나(1)
    //   보상    : 서 있기 → "목표 쪽을 보며 목표 속도로 가기"
    //   판 끝   : 넘어짐 + 제한시간 안에 목표에 못 닿음. 닿으면 다음 목표와 새 제한시간
    // 판마다 성적(평균 속도 · 속도 오차 · 넘어짐 · 도달 수)을 TensorBoard에 남긴다.
    public class WalkAgent : StandAgent
    {
        public Transform target;
        public float targetSpeed = 1f;      // m/s. 커리큘럼(yaml)이 있으면 그 값으로 바뀐다
        [Tooltip("목표를 놓는 방향. 몸 정면에서 ±몇 도 안에. 180 = 사방")]
        public float targetAngle = 180f;    // 커리큘럼(yaml)이 있으면 그 값으로 바뀐다

        // 이번 판의 성적
        readonly SpeedStats _speed = new SpeedStats();
        protected readonly GaitStats gait = new GaitStats();     // 걸음걸이 (발 끌기 · 좌우 균형 · 몸통 기울기 …)
        int _reached;
        bool _fell;
        int _deadline;      // 이 스텝까지 목표에 닿아야 한다

        public override void OnEpisodeBegin()
        {
            RecordStats();          // 지난 판 성적을 남긴다
            _speed.Clear();
            gait.Clear();
            _reached = 0;
            _fell = false;

            ReadCurriculum();       // 이번 판의 목표 속도
            base.OnEpisodeBegin();
            MoveTarget();
        }

        // 지난 판 성적을 TensorBoard 에 남긴다 (Walk/…). 첫 판처럼 기록이 없으면 남기지 않는다.
        void RecordStats()
        {
            if (_speed.Count == 0) return;
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Walk/Speed", _speed.Average);
            stats.Add("Walk/SpeedError", _speed.Error(targetSpeed));
            stats.Add("Walk/FallRate", _fell ? 1f : 0f);    // 판마다 0/1 → 평균이 곧 넘어짐 비율
            stats.Add("Walk/Targets", _reached);
            stats.Add("Gait/Slip", gait.Slip);                  // 발이 미끄러지는 속도 — 발 끌기
            stats.Add("Gait/StepBalance", gait.StepBalance);    // 왼발 · 오른발 걸음 수 비 (1 = 같음)
            stats.Add("Gait/Tilt", gait.Tilt);                  // 몸통 기울기 (도)
            stats.Add("Gait/Jitter", gait.Jitter);              // 관절 목표 변화 (도/초) — 떨림
        }

        // 커리큘럼(yaml)의 target_speed · target_angle 을 읽는다. 커리큘럼이 없으면 Inspector 값을 그대로 쓴다.
        void ReadCurriculum()
        {
            targetSpeed = Academy.Instance.EnvironmentParameters.GetWithDefault("target_speed", targetSpeed);
            targetAngle = Academy.Instance.EnvironmentParameters.GetWithDefault("target_angle", targetAngle);
        }

        // 몸 주변 4~8m, 몸 정면에서 ±targetAngle 안의 무작위 방향. 바닥(40×40) 밖으로 나가지 않게 ±15 안에 둔다.
        // 사방에 두면 매번 크게 돌아야 하는데, 엉덩이에 비틀기 축이 없어서 디딘 발을 미끄러뜨려야만 돈다.
        void MoveTarget()
        {
            Vector3 d = Quaternion.Euler(0f, Random.Range(-targetAngle, targetAngle), 0f)
                      * (HumanoidMath.HeadingRotation(body.pelvis.transform.forward) * Vector3.forward) * Random.Range(4f, 8f);
            Vector3 p = body.pelvis.transform.position - transform.parent.position;
            target.position = transform.parent.position + new Vector3(
                Mathf.Clamp(p.x + d.x, -15f, 15f), 0.25f, Mathf.Clamp(p.z + d.z, -15f, 15f));

            // 제한시간 = 목표 속도로 갈 때 걸리는 시간의 2배 + 5초 (돌아서기 · 출발할 여유).
            // 거리와 목표 속도로 정해서, 느린 목표(커리큘럼 0.5 m/s)나 먼 목표도 공평하다.
            Vector3 to = target.position - body.pelvis.transform.position;
            to.y = 0f;
            float seconds = to.magnitude / Mathf.Max(targetSpeed, 0.1f) * 2f + 5f;
            _deadline = StepCount + Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
        }

        // 몸에서 목표로 가는 수평 방향 (길이 1).
        Vector3 DirectionToTarget()
        {
            Vector3 d = target.position - body.pelvis.transform.position;
            d.y = 0f;
            return d.normalized;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            base.CollectObservations(sensor);                                                   // 48
            // 목표 방향을 "내가 보는 방향" 기준으로. 어느 쪽을 보든 같은 상황이면 같은 숫자가 나와야 한다.
            Vector3 local = Quaternion.Inverse(HumanoidMath.HeadingRotation(body.pelvis.transform.forward)) * DirectionToTarget();
            sensor.AddObservation(local.x);                                                     // 1
            sensor.AddObservation(local.z);                                                     // 1
            sensor.AddObservation(targetSpeed);                                                 // 1
        } // = 51

        protected override float StepReward()
        {
            Vector3 dir = DirectionToTarget();
            Vector3 velocity = body.CenterOfMassVelocity;

            // 목표 쪽으로 실제로 얼마나 빨리 가고 있나 → 판 평균
            _speed.Add(Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), dir));
            _fell |= body.HasFallen;
            gait.Step(body, dir, Time.fixedDeltaTime);

            // 목표에 닿으면 +1 하고 새 자리로 옮긴다 (제한시간도 새로). 판은 끝내지 않는다 (계속 걷게)
            Vector3 toTarget = target.position - body.pelvis.transform.position;
            toTarget.y = 0f;
            float bonus = 0f;
            if (toTarget.magnitude < 0.6f) { bonus = 1f; _reached++; MoveTarget(); }

            // 더하지 않고 곱한다. 엉뚱한 쪽을 보고 달리거나, 목표를 보고 서 있기만 하면 0점에 가깝다.
            return bonus + HumanoidMath.SpeedReward(velocity, dir, targetSpeed)
                         * HumanoidMath.FacingReward(body.torso.transform.forward, dir);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            base.OnActionReceived(actions);     // 행동 · 보상 · 넘어지면 끝내기 (넘어져서 끝났으면 새 판이 이미 시작됐다)

            // 제한시간 안에 목표에 못 닿으면 판을 끝낸다. 벌점은 없다 — 넘어짐과 같이, 앞으로 받을 보상이 사라지는 것이 벌이다.
            if (StepCount > _deadline) EndEpisode();
        }

        protected override string Status() =>
            base.Status() + string.Format("\n평균 속도 {0:F2} / 목표 {1:F1} m/s  도달 {2}  남은 시간 {3:F0}초",
                _speed.Average, targetSpeed, _reached, Mathf.Max(0, _deadline - StepCount) * Time.fixedDeltaTime);
    }
}
