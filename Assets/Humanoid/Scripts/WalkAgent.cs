using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Humanoid
{
    /// <summary>
    /// 4장 — 목표를 향해 걷기. 3장(StandAgent)에서 바뀌는 것은 둘뿐이다.
    ///   관측 +3 : 목표가 어느 쪽인가(2) · 얼마나 빨리 가야 하나(1)
    ///   보상    : 서 있기 → "목표 쪽을 보며(4-3) 목표 속도로 가기(4-2)"
    /// 5장 — 판마다 성적(평균 속도 · 속도 오차 · 넘어짐 · 도달 수)을 TensorBoard에 남긴다.
    /// </summary>
    public class WalkAgent : StandAgent
    {
        public Transform target;
        public float targetSpeed = 1f;      // m/s. 5장에서는 커리큘럼(yaml)이 바꾼다

        // 5장: 이번 판의 성적
        float _avgSpeed;
        int _samples, _reached;
        bool _fell;

        public override void OnEpisodeBegin()
        {
            if (_samples > 0)   // 지난 판 성적을 남긴다 (첫 판은 없다)
            {
                var stats = Academy.Instance.StatsRecorder;
                stats.Add("Walk/Speed", _avgSpeed);
                stats.Add("Walk/SpeedError", HumanoidMath.SpeedError(_avgSpeed, targetSpeed));   // [빈칸 5-2]
                stats.Add("Walk/FallRate", _fell ? 1f : 0f);    // 판마다 0/1 → 평균이 곧 넘어짐 비율
                stats.Add("Walk/Targets", _reached);
            }
            _avgSpeed = 0f;
            _samples = _reached = 0;
            _fell = false;

            // yaml에 커리큘럼이 없으면 Inspector 값을 그대로 쓴다.
            targetSpeed = Academy.Instance.EnvironmentParameters.GetWithDefault("target_speed", targetSpeed);

            base.OnEpisodeBegin();
            MoveTarget();
        }

        // 몸 주변 4~8m의 무작위 방향. 바닥(40×40) 밖으로 나가지 않게 ±15 안에 둔다.
        void MoveTarget()
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
            Vector3 p = body.pelvis.transform.position - transform.parent.position;
            target.position = transform.parent.position + new Vector3(
                Mathf.Clamp(p.x + d.x, -15f, 15f), 0.25f, Mathf.Clamp(p.z + d.y, -15f, 15f));
        }

        /// <summary>몸에서 목표로 가는 수평 방향 (길이 1).</summary>
        Vector3 DirectionToTarget()
        {
            Vector3 d = target.position - body.pelvis.transform.position;
            d.y = 0f;
            return d.normalized;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            base.CollectObservations(sensor);                                                   // 36
            // 목표 방향을 "내가 보는 방향" 기준으로. 어느 쪽을 보든 같은 상황이면 같은 숫자가 나와야 한다. [빈칸 4-1]
            Vector3 local = Quaternion.Inverse(HumanoidMath.HeadingRotation(body.pelvis.transform.forward)) * DirectionToTarget();
            sensor.AddObservation(local.x);                                                     // 1
            sensor.AddObservation(local.z);                                                     // 1
            sensor.AddObservation(targetSpeed);                                                 // 1
        } // = 39

        protected override float StepReward()
        {
            Vector3 dir = DirectionToTarget();
            Vector3 velocity = body.CenterOfMassVelocity;

            // 5장: 목표 쪽으로 실제로 얼마나 빨리 가고 있나 → 판 평균  [빈칸 5-1]
            _avgSpeed = HumanoidMath.RunningAverage(_avgSpeed, ++_samples, Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), dir));
            _fell |= body.HasFallen;

            // 목표에 닿으면 +1 하고 새 자리로 옮긴다. 판은 끝내지 않는다 (계속 걷게)
            Vector3 toTarget = target.position - body.pelvis.transform.position;
            toTarget.y = 0f;
            float bonus = 0f;
            if (toTarget.magnitude < 0.6f) { bonus = 1f; _reached++; MoveTarget(); }

            // 더하지 않고 곱한다. 엉뚱한 쪽을 보고 달리거나, 목표를 보고 서 있기만 하면 0점에 가깝다.
            return bonus + HumanoidMath.SpeedReward(velocity, dir, targetSpeed)                 // [빈칸 4-2]
                         * HumanoidMath.FacingReward(body.torso.transform.forward, dir);      // [빈칸 4-3]
        }

        protected override string Status() =>
            base.Status() + string.Format("\n평균 속도 {0:F2} / 목표 {1:F1} m/s  도달 {2}", _avgSpeed, targetSpeed, _reached);
    }
}
