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

        // 이번 판의 성적
        readonly SpeedStats _speed = new SpeedStats();
        int _reached;
        bool _fell;
        int _deadline;      // 이 스텝까지 목표에 닿아야 한다

        public override void OnEpisodeBegin()
        {
            RecordStats();          // 지난 판 성적을 남긴다
            _speed.Clear();
            _reached = 0;
            _fell = false;

            ReadCurriculum();       // 이번 판의 목표 속도
            base.OnEpisodeBegin();
            MoveTarget();
        }

        // 지난 판 성적을 TensorBoard 에 남긴다 (Walk/…). 첫 판처럼 기록이 없으면 남기지 않는다.
        void RecordStats()
        {
            // 여기에 직접 구현한다.
        }

        // 커리큘럼(yaml)의 target_speed 를 읽는다. 커리큘럼이 없으면 Inspector 값을 그대로 쓴다.
        void ReadCurriculum()
        {
            // 여기에 직접 구현한다.
        }

        // 몸 주변 4~8m의 무작위 방향. 바닥(40×40) 밖으로 나가지 않게 ±15 안에 둔다.
        void MoveTarget()
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
            Vector3 p = body.pelvis.transform.position - transform.parent.position;
            target.position = transform.parent.position + new Vector3(
                Mathf.Clamp(p.x + d.x, -15f, 15f), 0.25f, Mathf.Clamp(p.z + d.y, -15f, 15f));

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
            // 주어진 코드에서 출발해 직접 완성한다.
            base.CollectObservations(sensor);     // 서기의 관측 48개 (주어진 코드)
        } // = 51

        protected override float StepReward()
        {
            // 주어진 코드에서 출발해 직접 완성한다.
            Vector3 dir = DirectionToTarget();
            Vector3 velocity = body.CenterOfMassVelocity;

            // TensorBoard 성적표에 쓸 기록 (주어진 코드)
            _speed.Add(Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), dir));
            _fell |= body.HasFallen;

            // TODO: 목표에 닿았으면 보너스 1 · 도달 수 +1 · 새 목표.  그리고 속도 점수 × 방향 점수를 더해 돌려준다
            return 0f;
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 주어진 코드에서 출발해 직접 완성한다.
            base.OnActionReceived(actions);     // 서기의 행동 · 보상 · 넘어지면 끝내기 (주어진 코드)
        }

        protected override string Status() =>
            base.Status() + string.Format("\n평균 속도 {0:F2} / 목표 {1:F1} m/s  도달 {2}  남은 시간 {3:F0}초",
                _speed.Average, targetSpeed, _reached, Mathf.Max(0, _deadline - StepCount) * Time.fixedDeltaTime);
    }
}
