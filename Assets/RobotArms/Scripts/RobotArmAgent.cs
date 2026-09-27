using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 3장 — 팔 끝을 물체까지 가져가는 것을 학습한다. (아직 잡지 않는다)
    ///
    /// RollerBall의 RollerAgent와 <b>함수 구성이 똑같다.</b>
    ///   OnEpisodeBegin()      판을 새로 깐다
    ///   CollectObservations() 무엇을 보는가
    ///   OnActionReceived()    무엇을 하고, 언제 칭찬하는가
    ///   Heuristic()           사람이 AI 대신 조작한다
    ///
    /// 달라진 것은 공을 미는 대신 관절을 돌린다는 것뿐이다.
    ///   2장:  Angle += 키보드입력 × maxSpeed × dt
    ///   3장:  Angle += AI의 액션  × maxSpeed × dt     ← 숫자의 출처만 바뀐다
    ///
    /// 자세한 설명은 Docs/03-도달학습.md 를 본다.
    /// </summary>
    public class RobotArmAgent : Agent
    {
        [Header("연결")]
        public RobotArmController arm;
        public Transform targetObject;
        public ArmControlHud hud;       // 없어도 동작한다

        [Header("보상")]
        [Tooltip("끄면 RollerBall처럼 '도달하면 +1'만 준다.\n" +
                 "학습이 안 되는 것을 직접 보기 위한 스위치다. 먼저 꺼보고 켠다.")]
        public bool useDenseReward = true;

        public float successReward = 1.0f;

        // 팔 끝이 이 거리 안에 들어오면 도달로 본다.
        const float SuccessDistance = 0.25f;

        Rigidbody _objectBody;
        float _lastDistance;
        int _successCount;

        public override void Initialize() => _objectBody = targetObject.GetComponent<Rigidbody>();

        // ── 에피소드 시작 ────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            arm.ResetPose();
            PlaceObject();

            // "가까워진 만큼" 주려면 시작 거리를 기억해둬야 한다.
            _lastDistance = Distance();
        }

        /// <summary>
        /// 물체를 테이블 위 부채꼴 구역의 랜덤한 자리에 놓는다.
        ///
        /// 범위는 <b>팔이 들고 있다.</b> Scene 뷰의 초록 부채꼴이 곧 이 범위라서,
        /// 에이전트가 따로 갖고 있으면 둘이 어긋나도 알아챌 방법이 없다.
        /// </summary>
        void PlaceObject()
        {
            Vector3 spot = arm.SpawnPointLocal(
                Random.Range(arm.spawnDistanceRange.x, arm.spawnDistanceRange.y),
                Random.Range(arm.spawnYawRange.x, arm.spawnYawRange.y));

            targetObject.SetPositionAndRotation(
                arm.transform.TransformPoint(spot), Quaternion.identity);

            // 지난 판에 굴러가던 속도를 지운다.
            _objectBody.linearVelocity = Vector3.zero;
            _objectBody.angularVelocity = Vector3.zero;
        }

        // ── 관측 12개 ────────────────────────────────────────────────
        //
        // 전부 -1 ~ 1 근처로 맞춰서 넣는다(정규화).
        // 각도(±180)와 위치(±2.7)를 그대로 섞으면 큰 값이 학습을 지배한다.

        public override void CollectObservations(VectorSensor sensor)
        {
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedAngle(i));                    // 3 관절 각도

            // 각속도도 넣는다. "지금 얼마나 빨리 도는 중인지"를 알아야 목표를 지나치지 않는다.
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedVelocity(i));                 // 3 관절 각속도

            sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition));        // 3 팔 끝
            sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));  // 3 물체
        } // = 12

        // ── 행동 ─────────────────────────────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 관절마다 -1 ~ 1 짜리 회전 속도 명령이 하나씩.
            // 2장에서 키보드 입력을 넣던 자리에 AI의 숫자가 들어올 뿐이다.
            for (int i = 0; i < arm.JointCount; i++)
                arm.Drive(i, actions.ContinuousActions[i], Time.fixedDeltaTime);

            GiveReward();
            UpdateHud();
        }

        void GiveReward()
        {
            float distance = Distance();

            if (useDenseReward)
            {
                // 가까워진 만큼 칭찬한다. 멀어지면 저절로 음수가 된다.
                // <b>이 한 줄이 있고 없고가 이 장의 핵심이다.</b>
                AddReward((_lastDistance - distance) / arm.reach);

                // 시간 페널티. 한 판 내내 더하면 대략 -1 이 된다.
                AddReward(-1f / MaxStep);
            }

            _lastDistance = distance;

            if (distance < SuccessDistance)
            {
                AddReward(successReward);
                _successCount++;
                EndEpisode();
            }

            // 실패한 판은 MaxStep에 닿으면 ML-Agents가 알아서 끝낸다.
            // MaxStep을 0으로 두면 영원히 안 끝나서 학습 데이터가 쌓이지 않는다.
        }

        float Distance() => Vector3.Distance(arm.TipPosition, targetObject.position);

        void UpdateHud()
        {
            if (hud == null) return;

            hud.statusLine = string.Format("에피소드 {0}  성공 {1}  보상 {2:F2}",
                CompletedEpisodes, _successCount, GetCumulativeReward());
        }

        // ── 사람이 직접 조작하기 ─────────────────────────────────────

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            for (int i = 0; i < arm.JointCount && i < continuous.Length; i++)
                continuous[i] = RobotArmInput.Joint(i);
        }
    }
}
