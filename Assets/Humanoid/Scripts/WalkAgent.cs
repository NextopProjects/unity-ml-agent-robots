using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Humanoid
{
    /// <summary>
    /// 4장 — 목표를 향해 걷기. 3장(StandAgent)에서 바뀌는 것은 둘뿐이다.
    ///   관측 +3 : 목표가 어느 쪽인가(2) · 얼마나 빨리 가야 하나(1)
    ///   보상    : 서 있기 → "목표 쪽을 보며(4-3) 목표 속도로 가기(4-2)"
    /// </summary>
    public class WalkAgent : StandAgent
    {
        public Transform target;
        public float targetSpeed = 1f;      // m/s

        public override void OnEpisodeBegin()
        {
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


            // 목표에 닿으면 +1 하고 새 자리로 옮긴다. 판은 끝내지 않는다 (계속 걷게)
            Vector3 toTarget = target.position - body.pelvis.transform.position;
            toTarget.y = 0f;
            float bonus = 0f;
            if (toTarget.magnitude < 0.6f) { bonus = 1f; MoveTarget(); }

            // 더하지 않고 곱한다. 엉뚱한 쪽을 보고 달리거나, 목표를 보고 서 있기만 하면 0점에 가깝다.
            return bonus + HumanoidMath.SpeedReward(velocity, dir, targetSpeed)                 // [빈칸 4-2]
                         * HumanoidMath.FacingReward(body.torso.transform.forward, dir);      // [빈칸 4-3]
        }

    }
}
