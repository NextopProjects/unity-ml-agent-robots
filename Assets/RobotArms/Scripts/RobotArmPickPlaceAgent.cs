using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 4장 — 물체를 집어서 놓을 자리로 옮긴다. <b>이 프로젝트의 최종 과제가 여기서 완성된다.</b>
    /// 5장부터는 이 과제를 그대로 두고 집는 방식만 바꾼다.
    ///
    /// 3장과 비교하면 바뀐 곳이 보인다.
    ///   관측 +4 : 잡고 있는가 1 + <b>지금 가야 할 점</b> 3   (12 → 16)
    ///   액션 +1 : 이산 액션 "잡기 / 놓기"
    ///   보상 +3 : 집기 +0.5, 놓기 +1.0, 잘못 놓기 -0.3
    ///
    /// 핵심은 <b>목표가 상태에 따라 바뀐다</b>는 것이다.
    ///   잡기 전 → 가야 할 점 = 물체
    ///   잡은 후 → 가야 할 점 = 놓을 자리
    /// 그래도 보상 공식은 "가까워진 만큼" 하나로 유지된다.
    ///
    /// 자세한 설명은 Docs/04-집어서-옮겨놓기.md 를 본다.
    /// </summary>
    public class RobotArmPickPlaceAgent : Agent
    {
        [Header("연결")]
        public RobotArmController arm;
        public Transform targetObject;
        public Transform placeTarget;
        public ArmControlHud hud;       // 없어도 동작한다

        [Header("보상 — 여기만 바꿔가며 실험한다")]
        public float grabReward = 0.5f;       // ① 집었다
        public float placeReward = 1.0f;      // ② 놓았다
        public float dropPenalty = 0.3f;      // 엉뚱한 곳에서 놓았다
        public float lostPenalty = 0.5f;      // 작업 범위 밖으로 보냈다

        // 아래는 실측으로 정한 값이다. 바꾸면 팔이 물체에 닿지 못한다.
        const float GrabDistance = 0.25f;     // 이 안에서 잡기를 누르면 붙는다
        const float PlaceRadius = 0.25f;      // 이 안에 놓으면 성공
        const float RestSpeed = 0.2f;         // 이보다 느려야 "다 놓였다"
        const float MinSeparation = 0.6f;     // 물체와 놓을 자리를 띄우는 거리
        const float MaxStepReward = 0.05f;    // 한 스텝 진전 보상 상한
        // 실측: 팔 끝을 물체에 0.25 안으로 가져갈 수 있는 거리는 0.95~2.90,
        //       좌우는 ±120도까지. 그 안쪽으로 여유를 두고 잡는다.
        const float WorkMin = 1.00f;
        const float WorkMax = 2.80f;
        const float WorkYaw = 115f;

        bool _holding;      // 지금 들고 있는가. 이 하나가 "가야 할 점"을 가른다
        bool _grabbed;      // 이번 사이클에 한 번이라도 들었는가
        Rigidbody _objectBody;
        Transform _objectHome;      // 물체가 원래 매달려 있던 부모
        float _lastDistance;
        int _cycles;        // 이번 판에 몇 번 옮겼는가

        public override void Initialize()
        {
            _objectBody = targetObject.GetComponent<Rigidbody>();
            _objectHome = targetObject.parent;
        }

        // ── 에피소드 시작 ────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            // 성공해도 판이 끝나지 않으므로 에피소드 길이로는 실력을 알 수 없다.
            // "한 판에 몇 번 옮겼나"가 성적표다. (TensorBoard의 Task/Cycles)
            if (CompletedEpisodes > 0) Academy.Instance.StatsRecorder.Add("Task/Cycles", _cycles);
            _cycles = 0;

            Release(penalty: false);    // 들고 있던 것이 있으면 놓는다
            arm.ResetPose();

            // 물체도 놓을 자리도 매번 랜덤이다. 고정하면 자리를 외워버린다.
            Vector3 spot = RandomSpot();
            targetObject.SetPositionAndRotation(
                arm.transform.TransformPoint(spot), Quaternion.identity);
            _objectBody.linearVelocity = Vector3.zero;
            _objectBody.angularVelocity = Vector3.zero;

            MovePlaceTarget(spot);

            // 옮긴 결과를 물리 엔진에 알려준다. 안 하면 질의가 옛 위치로 계산된다.
            Physics.SyncTransforms();
            ResetCycle();
        }

        /// <summary>물체가 나타나는 부채꼴 안의 한 점. 범위는 팔이 들고 있다.</summary>
        Vector3 RandomSpot() => arm.SpawnPointLocal(
            Random.Range(arm.spawnDistanceRange.x, arm.spawnDistanceRange.y),
            Random.Range(arm.spawnYawRange.x, arm.spawnYawRange.y));

        /// <summary>놓을 자리를 랜덤하게. 물체와 겹치면 가만히 있어도 성공해버린다.</summary>
        void MovePlaceTarget(Vector3 objLocal)
        {
            Vector3 spot;
            int guard = 0;
            do { spot = RandomSpot(); }
            while (Flat(objLocal, spot) < MinSeparation && ++guard < 50);

            spot.y = 0.01f;     // 납작한 원판이라 테이블에 붙여 둔다
            placeTarget.position = arm.transform.TransformPoint(spot);
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // ── 관측 16개 ────────────────────────────────────────────────

        public override void CollectObservations(VectorSensor sensor)
        {
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedAngle(i));                    // 3
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedVelocity(i));                 // 3

            sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition));        // 3 팔 끝
            sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));  // 3 물체

            // ★ 4장에서 추가하는 4개.
            //   "지금 들고 있는가"가 없으면 학습이 불가능하다. 팔 자세가 똑같아도
            //   "가지러 가는 중"과 "들고 가는 중"은 해야 할 행동이 정반대다.
            //   그 짝이 "지금 가야 할 점"이다. 단계가 바뀌면 이 점이 바뀐다.
            sensor.AddObservation(_holding ? 1f : 0f);                            // 1
            sensor.AddObservation(arm.ToNormalizedLocal(CurrentGoal()));          // 3
        } // = 16

        // ── 행동 ─────────────────────────────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            for (int i = 0; i < arm.JointCount; i++)
                arm.Drive(i, actions.ContinuousActions[i], Time.fixedDeltaTime);

            // 이산 액션: 잡기 / 놓기.
            // 관절 각도는 "조금 더 / 조금 덜"이 의미 있는 연속량이지만,
            // 잡기는 잡거나 안 잡거나 둘 중 하나다. 중간값이 의미가 없다.
            UpdateGrip(actions.DiscreteActions[0] == 1);

            GiveReward();
            UpdateHud();
        }

        void UpdateGrip(bool wantGrip)
        {
            if (!_holding && wantGrip)
            {
                // 자석 방식 — 가까이서 누르면 붙는다.
                // 5장에서 이 조건이 "양쪽 손가락이 닿았는가"로 바뀐다.
                if (Vector3.Distance(arm.TipPosition, targetObject.position) < GrabDistance)
                    Grab();
            }
            else if (_holding && !wantGrip)
            {
                Release(penalty: true);
            }
        }

        void Grab()
        {
            _holding = true;

            // 드는 동안은 물리를 꺼둔다.
            // 안 그러면 중력이 물체를 끌어내리고 팔이 붙잡으면서 서로 싸운다.
            _objectBody.isKinematic = true;
            targetObject.SetParent(arm.tip);

            // 이 보상이 없으면 "잡기"를 시도할 이유가 없어서 물체 근처만 맴돈다.
            // 한 사이클에 한 번만 준다. 안 그러면 잡았다 놨다만 반복한다.
            if (!_grabbed) AddReward(grabReward);
            _grabbed = true;

            _lastDistance = DistanceToGoal();   // 목표가 바뀌었으니 기준을 새로 잡는다
        }

        void Release(bool penalty)
        {
            if (!_holding) return;

            _holding = false;
            targetObject.SetParent(_objectHome);
            _objectBody.isKinematic = false;
            _objectBody.linearVelocity = Vector3.zero;
            _objectBody.angularVelocity = Vector3.zero;

            // 엉뚱한 곳에 놓았다. 벌점만 주고 판은 계속한다.
            // 여기서 끝내면 "놓으면 큰일 난다"만 배우고
            // "잘못 놓았으면 다시 주우면 된다"는 배우지 못한다.
            if (penalty && !OverPlaceTarget()) AddReward(-dropPenalty);

            _lastDistance = DistanceToGoal();
        }

        // ── 단계: 가야 할 점이 상태에 따라 바뀐다 ────────────────────

        Vector3 Mover => _holding ? targetObject.position : arm.TipPosition;

        Vector3 CurrentGoal()
        {
            if (!_holding) return targetObject.position;        // ① 가지러 간다

            Vector3 local = arm.ToLocal(placeTarget.position);  // ② 놓을 자리로 옮긴다
            local.y = arm.spawnHeight;                          //    물체가 놓일 높이
            return arm.transform.TransformPoint(local);
        }

        float DistanceToGoal() => Vector3.Distance(Mover, CurrentGoal());

        bool OverPlaceTarget() => Flat(arm.ToLocal(targetObject.position),
                                       arm.ToLocal(placeTarget.position)) < PlaceRadius;

        /// <summary>물체가 팔이 갈 수 없는 곳으로 갔는가.
        /// 없으면 갈 수 없는 곳을 향해 판이 끝날 때까지 헛돈다.</summary>
        bool ObjectLost()
        {
            if (_holding) return false;     // 들고 있는 동안은 팔 안에 있다

            Vector3 p = arm.ToLocal(targetObject.position);
            if (p.y < -0.3f) return true;   // 테이블 아래로 떨어졌다

            float d = new Vector2(p.x, p.z).magnitude;
            if (d < WorkMin || d > WorkMax) return true;

            return Mathf.Abs(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg) > WorkYaw;
        }

        // ── 보상 ─────────────────────────────────────────────────────

        void GiveReward()
        {
            if (ObjectLost()) { AddReward(-lostPenalty); EndEpisode(); return; }

            // 진전 보상 — 3장의 "가까워진 만큼" 공식 그대로.
            // ★ 상한이 없으면 한 스텝이 판 전체를 덮는다. 물체를 놓친 순간
            //   거리가 한 번에 몇 미터씩 벌어지기 때문이다.
            float distance = DistanceToGoal();
            AddReward(Mathf.Clamp((_lastDistance - distance) / arm.reach, -MaxStepReward, MaxStepReward));
            AddReward(-1f / MaxStep);           // 시간 페널티
            _lastDistance = distance;

            // 놓기 성공 — 네 가지가 모두 맞아야 한다.
            //   ① 한 번이라도 들었다 ② 지금은 손을 뗐다
            //   ③ 놓을 자리 위에 있다 ④ 멈춰 있다 (떨어지는 중에 성공 처리되는 것을 막는다)
            if (_grabbed && !_holding && OverPlaceTarget()
                && _objectBody.linearVelocity.magnitude < RestSpeed)
            {
                AddReward(placeReward);
                _cycles++;

                // 판을 끝내지 않고 놓을 자리만 새 랜덤 자리로 옮긴다.
                MovePlaceTarget(arm.ToLocal(targetObject.position));
                ResetCycle();
            }
        }

        /// <summary>다음 한 번을 위해 되돌린다.
        /// 빼먹으면 두 번째부터 집기 보상이 없어서 가만히 있는 게 이득이 된다.</summary>
        void ResetCycle()
        {
            _grabbed = false;
            _lastDistance = DistanceToGoal();
        }

        void UpdateHud()
        {
            if (hud == null) return;

            hud.statusLine = string.Format("{0}  목표={1}  이번 판 {2}회  보상 {3:F2}",
                _holding ? "[운반중]" : "[빈손]", _holding ? "놓을자리" : "물체",
                _cycles, GetCumulativeReward());
        }

        // ── 사람이 직접 조작하기 ─────────────────────────────────────

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            for (int i = 0; i < arm.JointCount && i < continuous.Length; i++)
                continuous[i] = RobotArmInput.Joint(i);

            // DiscreteActions는 프로퍼티라서 바로 [0] = ... 로 대입할 수 없다.
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = RobotArmInput.Grip ? 1 : 0;
        }
    }
}
