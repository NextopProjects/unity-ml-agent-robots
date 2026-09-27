using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 5장 — 같은 과제를 자석이 아니라 집게로 푼다. 관측 19 / 연속 3 + 이산 1.
    ///
    /// 4장과 과제도 보상 공식도 같다. 집는 방법만 물리로 바뀌었다.
    /// <b>물체를 붙잡는 코드가 한 줄도 없다.</b> 양쪽 손가락이 닿았는지 볼 뿐이다.
    ///
    /// 왜 이렇게 만드는지는 Docs/05-그리퍼로-교체.md 를 본다.
    /// </summary>
    public class RobotArmGripperAgent : Agent
    {
        [Header("연결")]
        public RobotArmController arm;
        public Gripper gripper;
        public Transform targetObject;
        public Transform placeTarget;
        public ArmControlHud hud;       // 없어도 동작한다

        [Header("보상 — 여기만 바꿔가며 실험한다")]
        public float approachReward = 0.3f;   // ⓪ 물체 위에 정렬했다
        public float grabReward = 0.5f;       // ① 물었다
        public float liftReward = 0.5f;       // ② 들었다
        public float placeReward = 1.0f;      // ③ 놓았다
        public float dropPenalty = 0.3f;      // 엉뚱한 곳에서 놓쳤다
        public float lostPenalty = 0.5f;      // 작업 범위 밖으로 보냈다

        // 아래는 실측으로 정한 값이다. 바꾸면 팔이 물체에 닿지 못한다.
        const float PlaceRadius = 0.25f;      // 이 안에 놓으면 성공
        const float RestSpeed = 0.2f;         // 이보다 느려야 "다 놓였다"
        const float MinSeparation = 0.6f;     // 물체와 놓을 자리를 띄우는 거리
        const float HoverHeight = 0.35f;      // 물체 위 이 높이로 접근한다
        const float AlignRadius = 0.30f;      // 이 안에 들면 목표가 내려온다
        const float LiftHeight = 0.40f;       // 여기까지 들어야 "들었다"
        const float ApproachRadius = 0.15f;   // 이 안에 들면 "정렬했다"
        const float MaxStepReward = 0.05f;    // 한 스텝 진전 보상 상한
        // 집을 수 있는 거리. <b>팔이 닿는 거리보다 훨씬 좁다.</b>
        // 실측: 손가락 사이를 물체 중심에 가져갈 수 있는 범위가 1.10~2.75.
        //       (자석이던 4장은 0.95~2.90 이었다. 집게는 위에서 내려와야 한다)
        const float WorkMin = 1.15f;
        const float WorkMax = 2.70f;
        const float WorkYaw = 110f;

        bool _holding;      // 지금 물고 있는가 (물리가 정한다. 우리가 정하지 않는다)
        bool _grabbed;      // 이번 사이클에 한 번이라도 물었는가
        bool _lifted, _aligned;
        Rigidbody _objectBody;
        float _lastDistance;
        int _cycles;        // 이번 판에 몇 번 옮겼는가

        public override void Initialize() => _objectBody = targetObject.GetComponent<Rigidbody>();

        // ── 에피소드 시작 ────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            // 성공해도 판이 끝나지 않으므로 에피소드 길이로는 실력을 알 수 없다.
            // "한 판에 몇 번 옮겼나"가 성적표다. (TensorBoard의 Task/Cycles)
            if (CompletedEpisodes > 0) Academy.Instance.StatsRecorder.Add("Task/Cycles", _cycles);
            _cycles = 0;
            _holding = false;

            arm.ResetPose();
            gripper.ResetPose();

            // 물체도 놓을 자리도 매번 랜덤이다. 고정하면 자리를 외워버린다.
            Vector3 spot = RandomSpot();
            Vector3 world = arm.transform.TransformPoint(spot);
            targetObject.SetPositionAndRotation(world, Quaternion.identity);

            _objectBody.position = world;
            _objectBody.rotation = Quaternion.identity;
            _objectBody.linearVelocity = Vector3.zero;
            _objectBody.angularVelocity = Vector3.zero;
            _objectBody.WakeUp();

            MovePlaceTarget(spot);
            Physics.SyncTransforms();   // 옮긴 결과를 물리 엔진에 알려준다
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

            spot.y = 0.01f;
            placeTarget.position = arm.transform.TransformPoint(spot);
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // ── 관측 19개 ────────────────────────────────────────────────

        public override void CollectObservations(VectorSensor sensor)
        {
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedAngle(i));                    // 3
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedVelocity(i));                 // 3

            sensor.AddObservation(arm.ToNormalizedLocal(gripper.GripCenter));     // 3 손가락 사이
            sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));  // 3 물체
            sensor.AddObservation(_holding ? 1f : 0f);                            // 1
            sensor.AddObservation(arm.ToNormalizedLocal(CurrentGoal()));          // 3 지금 가야 할 점
            sensor.AddObservation(gripper.GripValue);                             // 1 쥔 정도
            sensor.AddObservation(gripper.TouchingL ? 1f : 0f);                   // 1
            sensor.AddObservation(gripper.TouchingR ? 1f : 0f);                   // 1
        } // = 19

        // ── 행동 ─────────────────────────────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            for (int i = 0; i < arm.JointCount; i++)
                arm.Drive(i, actions.ContinuousActions[i], Time.fixedDeltaTime);

            gripper.Drive(actions.DiscreteActions[0] == 1);
            gripper.UpdateContacts();

            UpdateHolding();
            GiveReward();
            UpdateHud();
        }

        /// <summary>물고 있는지는 물리가 만든 결과를 읽을 뿐이다.</summary>
        void UpdateHolding()
        {
            bool now = gripper.BothTouching;
            if (now == _holding) return;

            if (now)
            {
                if (!_grabbed) AddReward(grabReward);   // 한 사이클에 한 번만
                _grabbed = true;
            }
            else if (!OverPlaceTarget())
            {
                AddReward(-dropPenalty);                // 엉뚱한 곳에서 빠졌다
            }

            _holding = now;
            _lastDistance = DistanceToGoal();   // 목표가 바뀌었으니 기준을 새로 잡는다
        }

        // ── 단계: 가야 할 점 하나가 단계마다 바뀐다 ──────────────────
        //   ① 물체 위로  ② 똑바로 들어올린다  ③ 놓을 자리 위로  ④ 손을 편다
        //   ①③의 "위로 먼저"가 핵심이다. 옆에서 가면 집게가 물체를 밀어낸다.

        Vector3 Mover => _holding ? targetObject.position : gripper.GripCenter;

        /// <summary>멀면 높이 뜬 점, 가까우면 낮은 점 — "위로 돌아 내려오는" 경로가 생긴다.</summary>
        Vector3 Above(Vector3 spot)
            => spot + Vector3.up * (Mathf.Clamp01(Flat(Mover, spot) / AlignRadius) * HoverHeight);

        Vector3 CurrentGoal()
        {
            if (!_holding) return Above(targetObject.position);

            if (targetObject.position.y < LiftHeight)
                return new Vector3(targetObject.position.x, LiftHeight, targetObject.position.z);

            return Above(new Vector3(placeTarget.position.x, arm.spawnHeight, placeTarget.position.z));
        }

        float DistanceToGoal() => Vector3.Distance(Mover, CurrentGoal());

        bool OverPlaceTarget() => Flat(arm.ToLocal(targetObject.position),
                                       arm.ToLocal(placeTarget.position)) < PlaceRadius;

        /// <summary>집을 수 없는 곳으로 갔는가. 없으면 갈 수 없는 곳을 향해 끝까지 헛돈다.</summary>
        bool ObjectLost()
        {
            Vector3 p = arm.ToLocal(targetObject.position);
            if (p.y < -0.3f) return true;

            float d = new Vector2(p.x, p.z).magnitude;
            if (d < WorkMin || d > WorkMax) return true;

            return Mathf.Abs(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg) > WorkYaw;
        }

        // ── 보상 ─────────────────────────────────────────────────────

        void GiveReward()
        {
            if (ObjectLost()) { AddReward(-lostPenalty); EndEpisode(); return; }

            // 진전 보상 — 3장부터 쓰는 "가까워진 만큼" 공식 그대로.
            // 상한이 없으면 튕겨 나간 한 스텝이 에피소드 전체를 덮는다.
            float distance = DistanceToGoal();
            AddReward(Mathf.Clamp((_lastDistance - distance) / arm.reach, -MaxStepReward, MaxStepReward));
            AddReward(-1f / MaxStep);           // 시간 페널티
            _lastDistance = distance;

            // 단계 보너스 — 다음 단계로 건너가는 다리다. 없으면 못 넘어간다.
            if (!_aligned && !_grabbed
                && Vector3.Distance(gripper.GripCenter, targetObject.position) < ApproachRadius)
            {
                AddReward(approachReward);
                _aligned = true;
            }

            if (!_lifted && _holding && targetObject.position.y >= LiftHeight)
            {
                AddReward(liftReward);
                _lifted = true;
            }

            // 놓기 성공 — 판을 끝내지 않고 놓을 자리만 새 랜덤 자리로 옮긴다.
            if (_grabbed && !_holding && OverPlaceTarget()
                && _objectBody.linearVelocity.magnitude < RestSpeed)
            {
                AddReward(placeReward);
                _cycles++;
                MovePlaceTarget(arm.ToLocal(targetObject.position));
                ResetCycle();
            }
        }

        /// <summary>단계 보너스를 다시 받을 수 있게 되돌린다.
        /// 빼먹으면 두 번째부터 보너스가 없어서 가만히 있는 게 이득이 된다.</summary>
        void ResetCycle()
        {
            _grabbed = _lifted = _aligned = false;
            _lastDistance = DistanceToGoal();
        }

        void UpdateHud()
        {
            if (hud == null) return;

            string phase = !_grabbed ? "① 잡으러 간다"
                : !_holding ? "④ 놓았다"
                : targetObject.position.y < LiftHeight ? "② 들어올린다" : "③ 옮긴다";

            hud.statusLine = string.Format("{0}  쥠 {1:F2}  접촉 {2}{3}  이번 판 {4}회  보상 {5:F2}",
                phase, gripper.GripValue,
                gripper.TouchingL ? "L" : "-", gripper.TouchingR ? "R" : "-",
                _cycles, GetCumulativeReward());
        }

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
