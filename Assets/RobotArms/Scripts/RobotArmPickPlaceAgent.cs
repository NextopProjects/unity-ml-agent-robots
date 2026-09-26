using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 4장 — 물체를 집어서 놓을 자리로 옮겨 놓는다.
    ///
    /// <b>이 프로젝트의 최종 과제가 여기서 완성된다.</b>
    /// 5장부터는 이 과제를 그대로 두고 집는 방식과 난이도만 바꿔가며 실험한다.
    ///
    /// 3장의 <see cref="RobotArmAgent"/>와 비교하면 바뀐 곳이 보인다.
    ///
    ///   관측 +4 : 지금 잡고 있는가 1 + 놓을 자리 위치 3   (12 → 16)
    ///   액션 +1 : 이산 액션 "잡기 / 놓기"                 (연속 3 → 연속 3 + 이산 1)
    ///   보상 +3 : 집기 +0.5, 놓기 성공 +1.0, 잘못 놓기 -0.3
    ///
    /// 핵심 아이디어는 <b>"현재 목표"가 상태에 따라 바뀐다</b>는 것이다.
    ///
    ///   잡기 전 → 현재 목표 = 물체
    ///   잡은 후 → 현재 목표 = 놓을 자리
    ///
    /// 그래도 보상 공식은 하나로 유지된다.
    /// AI는 "현재 목표를 향해 간다"는 단순한 규칙 하나만 배우면 된다.
    /// </summary>
    public class RobotArmPickPlaceAgent : Agent
    {
        [Header("참조")]
        [Tooltip("팔의 몸. 관절을 돌리는 일은 전부 이쪽이 한다.")]
        public RobotArmController arm;

        [Tooltip("집어서 옮길 물체.")]
        public Transform targetObject;

        [Tooltip("물체를 내려놓아야 할 자리.")]
        public Transform placeTarget;

        [Tooltip("화면 표시용. 없어도 동작한다.")]
        public ArmControlHud hud;

        [Header("집기 — 자석 방식")]
        [Tooltip("팔 끝이 이 거리 안에 있을 때만 잡을 수 있다. " +
                 "통과 방지의 tipExemptDistance보다 작아야 한다.")]
        public float grabDistance = 0.25f;

        [Header("놓기 성공 조건")]
        [Tooltip("물체와 놓을 자리의 수평 거리가 이 안이어야 성공.")]
        public float placeRadius = 0.25f;

        [Tooltip("물체가 이보다 느려야 '놓였다'고 본다. 떨어지는 중에 성공 처리되는 것을 막는다.")]
        public float restSpeed = 0.2f;

        [Header("물체가 나타나는 범위")]
        [Tooltip("받침대에서 떨어진 거리.")]
        public Vector2 spawnDistanceRange = new Vector2(1.2f, 2.4f);

        [Tooltip("좌우 각도 범위(도). 0 = 정면.")]
        public Vector2 spawnYawRange = new Vector2(-60f, 60f);

        [Header("놓을 자리")]
        [Tooltip("놓을 자리의 위치. (받침대 기준 거리, 좌우각) — 4장에서는 고정이다.")]
        public Vector2 placeSpot = new Vector2(1.5f, -45f);

        [Tooltip("물체를 놓을 자리에서 이만큼은 떨어뜨려 놓는다.")]
        public float minSeparation = 0.6f;

        [Header("보상")]
        [Tooltip("끄면 '놓기 성공 +1'만 준다. 학습이 안 되는 것을 보기 위한 스위치.")]
        public bool useDenseReward = true;

        [Tooltip("집는 데 성공한 순간 주는 보상. 한 에피소드에 한 번만.")]
        public float grabReward = 0.5f;

        [Tooltip("놓을 자리에 제대로 내려놓았을 때.")]
        public float placeReward = 1.0f;

        [Tooltip("엉뚱한 곳에 놓았을 때의 벌점. 에피소드는 끝내지 않는다.")]
        public float dropPenalty = 0.3f;

        // 지금 물체를 들고 있는가. 이 한 개가 "현재 목표"를 가른다.
        bool _isHolding;

        // 이번 에피소드에 한 번이라도 들었는가.
        // 가만히 있다가 성공 처리되는 것을 막는다. (아래 GiveReward 참고)
        bool _hasGrabbed;

        // 집기 보상은 한 에피소드에 한 번만.
        // 없으면 잡았다 놨다를 반복해 보상만 긁어모으는 것을 배운다.
        bool _grabRewarded;

        Rigidbody _objectBody;
        Transform _objectHome;      // 물체가 원래 매달려 있던 부모
        float _previousDistance;
        int _successCount;

        public override void Initialize()
        {
            _objectBody = targetObject.GetComponent<Rigidbody>();
            _objectHome = targetObject.parent;
        }

        // ── 에피소드 시작 ────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            Release(givePenalty: false);    // 들고 있던 것이 있으면 놓는다
            _hasGrabbed = false;
            _grabRewarded = false;

            arm.ResetPose();
            PlaceObjectAndTarget();

            _previousDistance = DistanceToGoal();
        }

        void PlaceObjectAndTarget()
        {
            // ① 놓을 자리를 먼저 정한다. 4장에서는 고정이다.
            //    (6장에서 이걸 랜덤으로 바꿔 난이도를 올린다)
            Vector3 placeLocal = arm.SpawnPointLocal(placeSpot.x, placeSpot.y);

            // ② 물체는 놓을 자리에서 충분히 떨어진 곳에 둔다.
            //
            //    이 검사가 없으면 물체가 처음부터 놓을 자리 위에 놓일 수 있고,
            //    그러면 아무것도 하지 않았는데 성공 판정이 나버린다.
            //    놓을 자리가 고정이어도 물체는 랜덤이라 겹칠 수 있다.
            Vector3 objLocal;
            int guard = 0;
            do
            {
                objLocal = arm.SpawnPointLocal(
                    Random.Range(spawnDistanceRange.x, spawnDistanceRange.y),
                    Random.Range(spawnYawRange.x, spawnYawRange.y));
            }
            while (HorizontalDistance(objLocal, placeLocal) < minSeparation && ++guard < 50);

            targetObject.position = arm.transform.TransformPoint(objLocal);
            targetObject.rotation = Quaternion.identity;

            // 이전 에피소드에서 굴러가던 속도를 지운다.
            if (_objectBody != null)
            {
                _objectBody.linearVelocity = Vector3.zero;
                _objectBody.angularVelocity = Vector3.zero;
            }

            // 놓을 자리는 납작한 원판이라 테이블 표면에 붙여 둔다.
            placeLocal.y = 0.01f;
            placeTarget.position = arm.transform.TransformPoint(placeLocal);
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
            => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // ── 관측 ─────────────────────────────────────────────────────

        public override void CollectObservations(VectorSensor sensor)
        {
            // 3장과 똑같은 12개
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedAngle(i));                    // 3

            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedVelocity(i));                 // 3

            sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition));        // 3
            sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));  // 3

            // ★ 4장에서 추가하는 4개.
            //
            //   "지금 잡고 있는가"가 없으면 학습이 불가능하다.
            //   팔 자세가 똑같아도 "가지러 가는 중"과 "들고 가는 중"은
            //   해야 할 행동이 정반대다. 그 둘을 구분할 유일한 단서다.
            //
            //   "놓을 자리가 어디인가"는 그 짝이다.
            //   잡고 있으면 이쪽으로, 아니면 물체 쪽으로 가면 된다.
            sensor.AddObservation(_isHolding ? 1f : 0f);                          // 1
            sensor.AddObservation(arm.ToNormalizedLocal(placeTarget.position));   // 3
        } // = 16

        // ── 행동 ─────────────────────────────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 연속 액션: 관절 회전 (3장과 동일)
            var continuous = actions.ContinuousActions;
            for (int i = 0; i < arm.JointCount; i++)
            {
                arm.Drive(i, continuous[i], Time.fixedDeltaTime);
            }

            // 이산 액션: 잡기 / 놓기
            //   관절 각도는 "조금 더 / 조금 덜"이 의미 있는 연속량이지만,
            //   잡기는 잡거나 안 잡거나 둘 중 하나다. 중간값이 의미가 없다.
            //   → 액션의 성질에 맞는 타입을 고르는 것도 설계의 일부다.
            bool wantGrip = actions.DiscreteActions[0] == 1;
            UpdateGrip(wantGrip);

            GiveReward();
            UpdateHud();
        }

        void UpdateGrip(bool wantGrip)
        {
            if (!_isHolding && wantGrip)
            {
                // 자석 방식 — 가까이서 "잡기"를 누르면 붙는다.
                // 5장에서 이 조건이 "양쪽 손가락 접촉"으로 바뀐다.
                float d = Vector3.Distance(arm.TipPosition, targetObject.position);
                if (d < grabDistance) Grab();
            }
            else if (_isHolding && !wantGrip)
            {
                Release(givePenalty: true);
            }
        }

        void Grab()
        {
            _isHolding = true;
            _hasGrabbed = true;

            // 잡고 있는 동안은 물리를 꺼둔다.
            // 안 그러면 중력이 물체를 끌어내리고 팔이 붙잡으면서 서로 싸운다.
            if (_objectBody != null) _objectBody.isKinematic = true;
            targetObject.SetParent(arm.tip);

            if (!_grabRewarded)
            {
                // 이 보상이 없으면 AI는 "잡기"를 시도할 이유가 없어서
                // 영원히 물체 근처만 맴돌다 끝난다. 새 행동으로 건너가는 다리다.
                AddReward(grabReward);
                _grabRewarded = true;
            }

            // 목표가 "물체"에서 "놓을 자리"로 바뀌었으므로 거리 기준을 새로 잡는다.
            // 안 하면 기준이 바뀌는 그 한 스텝에 보상이 크게 튄다.
            _previousDistance = DistanceToGoal();
        }

        /// <summary>손을 편다. 놓을 자리 위가 아니면 벌점.</summary>
        void Release(bool givePenalty)
        {
            if (!_isHolding) return;

            _isHolding = false;
            targetObject.SetParent(_objectHome);

            if (_objectBody != null)
            {
                _objectBody.isKinematic = false;
                _objectBody.linearVelocity = Vector3.zero;
                _objectBody.angularVelocity = Vector3.zero;
            }

            if (givePenalty && !IsOverPlaceTarget())
            {
                // 엉뚱한 곳에 놓았다. 벌점만 주고 에피소드는 계속한다.
                //
                // 여기서 에피소드를 끝내면 AI는 "놓으면 큰일 난다"만 배우고
                // "잘못 놓았으면 다시 주우면 된다"는 배우지 못한다.
                // 실수에서 회복하는 것도 과제의 일부다.
                AddReward(-dropPenalty);
            }

            _previousDistance = DistanceToGoal();
        }

        // ── 보상 ─────────────────────────────────────────────────────

        /// <summary>물체가 놓을 자리 위에 있는가. 수평 거리로만 본다.</summary>
        bool IsOverPlaceTarget()
        {
            return HorizontalDistance(arm.ToLocal(targetObject.position),
                                      arm.ToLocal(placeTarget.position)) < placeRadius;
        }

        bool IsObjectAtRest()
        {
            if (_objectBody == null) return true;
            return _objectBody.linearVelocity.magnitude < restSpeed;
        }

        /// <summary>
        /// "현재 목표까지 남은 거리".
        /// 잡기 전에는 팔 끝에서 물체까지, 잡은 후에는 물체에서 놓을 자리까지.
        /// 목표가 바뀌어도 보상 공식은 하나로 유지된다.
        /// </summary>
        float DistanceToGoal()
        {
            if (_isHolding)
                return Vector3.Distance(targetObject.position, placeTarget.position);

            return Vector3.Distance(arm.TipPosition, targetObject.position);
        }

        void GiveReward()
        {
            float distance = DistanceToGoal();

            if (useDenseReward)
            {
                // 현재 목표에 가까워진 만큼 칭찬한다. 멀어지면 자동으로 음수.
                AddReward((_previousDistance - distance) / arm.reach);

                // 시간 페널티. 에피소드 내내 더하면 대략 -1 이 된다.
                if (MaxStep > 0) AddReward(-1f / MaxStep);
            }

            _previousDistance = distance;

            // 성공 조건 네 가지가 모두 맞아야 한다.
            //   1. 한 번이라도 물체를 들었다   ← 가만히 있다가 성공하는 것을 막는다
            //   2. 지금은 손을 뗀 상태다
            //   3. 물체가 놓을 자리 위에 있다
            //   4. 물체가 멈춰 있다            ← 떨어지는 도중에 성공 처리되는 것을 막는다
            if (_hasGrabbed && !_isHolding && IsOverPlaceTarget() && IsObjectAtRest())
            {
                AddReward(placeReward);
                _successCount++;
                EndEpisode();
            }
        }

        void UpdateHud()
        {
            if (hud == null) return;

            hud.statusLine = string.Format("{0} 목표={1}  에피 {2}  성공 {3}  보상 {4:F2}",
                _isHolding ? "[운반중]" : "[빈손]",
                _isHolding ? "놓을자리" : "물체",
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

            // DiscreteActions는 프로퍼티라서 바로 [0] = ... 로 대입할 수 없다.
            // 지역 변수로 한 번 받아서 쓴다.
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = RobotArmInput.Grip ? 1 : 0;
        }
    }
}
