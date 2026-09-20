using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 3장 — 물체까지 팔 끝을 가져가는 것을 학습한다. (아직 잡지 않는다)
    ///
    /// RollerBall의 RollerAgent.cs와 함수 구성이 똑같다.
    ///   OnEpisodeBegin()     : 판을 새로 깐다
    ///   CollectObservations(): 에이전트가 "보는" 값을 넣는다
    ///   OnActionReceived()   : AI가 낸 숫자로 움직이고, 보상을 준다
    ///   Heuristic()          : 사람이 AI 대신 조작한다
    ///
    /// 달라진 점은 <b>공을 미는 대신 관절을 돌린다</b>는 것뿐이다.
    /// 관절을 돌리는 일은 2장의 RobotArmController가 이미 다 해두었으므로,
    /// 여기서는 "무엇을 보고 / 무엇을 하고 / 언제 칭찬할지"만 정하면 된다.
    ///
    ///   2장:  Angle += 키보드입력 × maxSpeed × dt
    ///   3장:  Angle += AI의액션   × maxSpeed × dt     ← 숫자의 출처만 바뀐다
    /// </summary>
    public class RobotArmAgent : Agent
    {
        [Header("참조")]
        [Tooltip("팔의 몸. 관절을 돌리는 일은 전부 이쪽이 한다.")]
        public RobotArmController arm;

        [Tooltip("도달해야 할 물체.")]
        public Transform targetObject;

        [Tooltip("화면 표시용. 없어도 동작한다.")]
        public ArmControlHud hud;

        [Header("성공 조건")]
        [Tooltip("팔 끝이 이 거리 안으로 들어오면 성공.")]
        public float successDistance = 0.25f;

        [Header("물체를 놓는 범위")]
        [Tooltip("받침대에서 떨어진 거리.")]
        public Vector2 spawnDistanceRange = new Vector2(1.2f, 2.4f);

        [Tooltip("물체가 나타나는 좌우 각도 범위(도). 0 = 정면.")]
        public Vector2 spawnYawRange = new Vector2(-60f, 60f);

        [Header("보상")]
        [Tooltip("끄면 RollerBall처럼 '도달하면 +1'만 준다. " +
                 "학습이 안 되는 것을 직접 보기 위한 스위치.")]
        public bool useDenseReward = true;

        [Tooltip("성공했을 때 주는 보상.")]
        public float successReward = 1.0f;

        Rigidbody _objectBody;
        float _previousDistance;
        int _successCount;

        public override void Initialize()
        {
            _objectBody = targetObject != null ? targetObject.GetComponent<Rigidbody>() : null;
        }

        // ── 에피소드 시작 ────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            // 팔을 시작 자세로 되돌린다.
            arm.ResetPose();

            // 물체를 팔이 닿는 범위 안의 임의의 위치로 옮긴다.
            // RollerBall이 목표를 바닥 위 임의의 위치로 옮긴 것과 같다.
            PlaceObjectRandomly();

            // "가까워진 만큼" 보상을 주려면 시작 거리를 기억해둬야 한다.
            _previousDistance = DistanceToObject();
        }

        void PlaceObjectRandomly()
        {
            // 물체를 공중에 놓지 않는 이유:
            //   물체에는 Rigidbody가 있어서 공중에 두면 곧바로 떨어진다.
            //   테이블 위에 놓으면 가만히 있어주고, 4장에서 집어 올릴 때도 그대로 쓴다.
            //
            // 팔이 좌우로도 돌 수 있으므로 물체는 테이블 위 "부채꼴" 구역에 나타난다.
            //   거리     = 받침대에서 얼마나 멀리
            //   좌우 각도 = 정면에서 얼마나 옆으로
            float distance = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
            float yaw = Random.Range(spawnYawRange.x, spawnYawRange.y);

            targetObject.position = arm.transform.TransformPoint(arm.SpawnPointLocal(distance, yaw));
            targetObject.rotation = Quaternion.identity;

            // 이전 에피소드에서 굴러가던 속도를 지운다.
            if (_objectBody != null)
            {
                _objectBody.linearVelocity = Vector3.zero;
                _objectBody.angularVelocity = Vector3.zero;
            }
        }

        // ── 관측: 에이전트가 "보는" 것 ───────────────────────────────

        public override void CollectObservations(VectorSensor sensor)
        {
            // 관절 각도 (정규화) — 관절 3개
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedAngle(i));                    // 3

            // 관절 각속도 (정규화)
            // "지금 얼마나 빨리 움직이는 중인지"를 알아야 목표를 지나치지 않는다.
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedVelocity(i));                 // 3

            // 팔 끝이 어디 있는지 (받침대 기준)
            sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition));        // 3

            // 물체가 어디 있는지 (받침대 기준)
            sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));  // 3
        } // = 12

        // ── 행동: 에이전트가 "하는" 것 ───────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 행동: 관절마다 -1 ~ 1 짜리 회전 속도 명령이 하나씩.
            // 2장에서 키보드 입력을 넣던 자리에 AI의 숫자가 들어올 뿐이다.
            var continuous = actions.ContinuousActions;

            for (int i = 0; i < arm.JointCount; i++)
            {
                arm.Drive(i, continuous[i], Time.fixedDeltaTime);
            }

            GiveReward();
            UpdateHud();
        }

        void GiveReward()
        {
            float distance = DistanceToObject();

            if (useDenseReward)
            {
                // 가까워진 만큼 칭찬한다. 멀어지면 자동으로 음수가 된다.
                // 이 한 줄이 있고 없고의 차이가 이 장의 핵심이다.
                AddReward((_previousDistance - distance) / arm.reach);

                // 시간 페널티. 에피소드 내내 더하면 대략 -1 이 된다.
                if (MaxStep > 0) AddReward(-1f / MaxStep);
            }

            _previousDistance = distance;

            if (distance < successDistance)
            {
                AddReward(successReward);
                _successCount++;
                EndEpisode();
            }

            // MaxStep에 도달하면 ML-Agents가 알아서 에피소드를 끝낸다.
            // MaxStep을 0(무제한)으로 두면 실패한 에피소드가 영원히 안 끝나서
            // 학습 데이터가 쌓이지 않는다. 반드시 값을 넣어둘 것.
        }

        float DistanceToObject() => Vector3.Distance(arm.TipPosition, targetObject.position);

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
            {
                continuous[i] = RobotArmInput.Joint(i);
            }
        }
    }
}
