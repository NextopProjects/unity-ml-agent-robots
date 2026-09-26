using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 5장 — 4장과 <b>똑같은 과제</b>를 자석이 아니라 집게로 푼다.
    ///
    /// <see cref="RobotArmPickPlaceAgent"/>(4장)와 나란히 열어놓고 비교해보라.
    /// 팔도, 과제도, 보상 공식도 그대로다. 바뀐 것은 <b>집는 방법</b> 하나뿐이다.
    ///
    ///   4장: 가까이서 버튼을 누르면 붙는다  (SetParent — 프로그램이 붙여준다)
    ///   5장: 손가락이 눌러서 마찰로 버틴다  (붙이는 코드가 아예 없다)
    ///
    /// 그래서 이 스크립트에는 물체를 붙잡는 코드가 한 줄도 없다.
    /// 4장에 있던 Grab() 같은 함수 자체가 사라졌다.
    /// 우리가 하는 일은 <b>양쪽 손가락이 물체에 닿아 있는지 보는 것</b>뿐이다.
    ///
    /// 관측이 3개 늘었다. 손의 상태를 알려줘야 하기 때문이다. (16 → 19)
    ///   쥔 정도 1 + 왼쪽 접촉 1 + 오른쪽 접촉 1
    ///
    /// 4장에서 "잡았다"는 프로그램이 확실히 아는 값이었지만,
    /// 5장에서는 <b>손끝의 감각으로 알아내야 하는 값</b>이 되었다.
    /// 실제 로봇이 겪는 문제와 같다.
    /// </summary>
    public class RobotArmGripperAgent : Agent
    {
        [Header("참조")]
        [Tooltip("팔의 몸. 1~2장에서 만든 것을 그대로 쓴다.")]
        public RobotArmController arm;

        [Tooltip("집게. 손가락을 여닫고, 닿았는지 알려준다.")]
        public PincerGripper gripper;

        [Tooltip("팔 끝을 따라다니는 손. 자세를 되돌릴 때 필요하다.")]
        public GripperHand hand;

        [Tooltip("집어서 옮길 물체.")]
        public Transform targetObject;

        [Tooltip("물체를 내려놓아야 할 자리.")]
        public Transform placeTarget;

        [Tooltip("화면 표시용. 없어도 동작한다.")]
        public ArmControlHud hud;

        [Header("놓기 성공 조건")]
        [Tooltip("물체와 놓을 자리의 수평 거리가 이 안이어야 성공.")]
        public float placeRadius = 0.25f;

        [Tooltip("물체가 이보다 느려야 다 놓였다고 본다. 떨어지는 중에 성공 처리되는 것을 막는다.")]
        public float restSpeed = 0.2f;

        [Header("물체가 나타나는 범위")]
        public Vector2 spawnDistanceRange = new Vector2(1.2f, 2.4f);
        public Vector2 spawnYawRange = new Vector2(-60f, 60f);

        [Header("작업 범위 — 여기를 벗어난 물체는 포기한다")]
        [Tooltip("집게가 위에서 내려와 집을 수 있는 거리. 스폰 범위보다 조금 넓게 둔다.\n" +
                 "밀려서 이 밖으로 나가면 판을 새로 연다. 안 그러면 갈 수 없는 곳을 향해 헛돈다.")]
        public Vector2 workRange = new Vector2(1.05f, 2.65f);

        [Tooltip("좌우 한계(도). 스폰은 ±60이므로 조금 넓게 둔다.")]
        public float workYawLimit = 100f;

        [Header("놓을 자리")]
        [Tooltip("물체를 놓을 자리에서 이만큼은 떨어뜨려 놓는다.")]
        public float minSeparation = 0.6f;

        [Header("단계 — 과제를 네 토막으로 나눈다")]
        [Tooltip("물체 위 이만큼 높이에서 접근한다. 옆에서 돌진하면 집게가 물체를 밀어낸다.")]
        public float hoverHeight = 0.35f;

        [Tooltip("수평으로 이만큼 안에 들어오면 목표가 서서히 내려온다.")]
        public float alignRadius = 0.30f;

        [Tooltip("이 높이까지 들어올려야 '들었다'로 본다.")]
        public float liftHeight = 0.40f;

        [Header("보상")]
        public bool useDenseReward = true;

        [Tooltip("⓪ 집게를 물체 바로 위에 정렬했을 때. 첫 단계로 가는 다리다.")]
        public float approachReward = 0.3f;

        [Tooltip("집게 한가운데와 물체가 이 거리 안이면 '정렬했다'로 본다.")]
        public float approachRadius = 0.15f;

        [Tooltip("① 처음 물었을 때. 한 에피소드에 한 번만.")]
        public float grabReward = 0.5f;

        [Tooltip("② 물체를 liftHeight까지 들어올렸을 때. 한 번만.")]
        public float liftReward = 0.5f;

        [Tooltip("③ 놓을 자리에 제대로 내려놓았을 때.")]
        public float placeReward = 1.0f;

        [Tooltip("엉뚱한 곳에서 놓쳤을 때의 벌점.")]
        public float dropPenalty = 0.3f;

        [Tooltip("물체를 테이블 밖으로 떨어뜨렸을 때의 벌점. 에피소드도 끝난다.")]
        public float lostPenalty = 0.5f;

        [Tooltip("한 스텝에 받을 수 있는 진전 보상의 상한. 튀는 값이 학습을 덮는 것을 막는다.")]
        public float maxStepReward = 0.05f;

        // 물고 있는지를 우리가 정하지 않는다.
        // 양쪽 손가락이 물체에 닿아 있으면 물고 있는 것이고,
        // 마찰이 부족해 빠지면 저절로 닿지 않게 된다.
        bool _isHolding;
        bool _hasGrabbed;
        bool _grabRewarded;
        bool _liftRewarded;
        bool _approachRewarded;

        Rigidbody _objectBody;
        float _previousDistance;
        int _successCount;
        int _roundCount;

        public override void Initialize()
        {
            _objectBody = targetObject.GetComponent<Rigidbody>();
        }

        // ── 에피소드 시작 ────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            // 지난 판에 몇 번 옮겼는지 TensorBoard에 남긴다.
            //
            // 이제 성공해도 에피소드가 끝나지 않으므로, "에피소드 길이"로는
            // 잘하고 있는지 알 수 없다. <b>한 판에 몇 번 옮겼는가</b>가 성적표다.
            // (tensorboard에서 Task/Cycles 로 보인다)
            if (CompletedEpisodes > 0)
                Academy.Instance.StatsRecorder.Add("Task/Cycles", _roundCount);

            _roundCount = 0;
            _isHolding = false;
            _hasGrabbed = false;
            _grabRewarded = false;
            _liftRewarded = false;
            _approachRewarded = false;

            arm.ResetPose();
            if (hand != null) hand.Snap();   // 손가락도 팔 끝 자리로 데려온다
            gripper.ResetOpen();

            PlaceObjectAndTarget();

            _previousDistance = DistanceToGoal();
        }

        /// <summary>물체가 나타나는 부채꼴 안에서 한 점을 뽑는다. (받침대 기준 로컬)</summary>
        Vector3 RandomSpot()
        {
            return arm.SpawnPointLocal(
                Random.Range(spawnDistanceRange.x, spawnDistanceRange.y),
                Random.Range(spawnYawRange.x, spawnYawRange.y));
        }

        /// <summary>
        /// 놓을 자리를 <b>랜덤한 곳</b>으로 옮긴다.
        /// 물체가 있는 곳과는 충분히 떨어뜨린다.
        /// 겹치면 아무것도 안 하고 성공해버리기 때문이다.
        /// </summary>
        void MovePlaceTarget(Vector3 objLocal)
        {
            Vector3 placeLocal;
            int guard = 0;
            do
            {
                placeLocal = RandomSpot();
            }
            while (HorizontalDistance(objLocal, placeLocal) < minSeparation && ++guard < 50);

            placeLocal.y = 0.01f;   // 납작한 원판이라 테이블에 붙여 둔다
            placeTarget.position = arm.transform.TransformPoint(placeLocal);
        }

        void PlaceObjectAndTarget()
        {
            // ★ 물체도 놓을 자리도 <b>매번 랜덤</b>이다.
            //
            //   놓을 자리를 고정해두면 에이전트가 "그 자리"를 통째로 외워버린다.
            //   관측을 보지 않고도 성공하니, 겉보기 성적은 좋은데 아무것도 배우지 못한다.
            //   둘 다 흔들어야 "관측을 읽고 목표로 간다"를 배운다.
            Vector3 objLocal = RandomSpot();
            Vector3 world = arm.transform.TransformPoint(objLocal);

            targetObject.position = world;
            targetObject.rotation = Quaternion.identity;

            if (_objectBody != null)
            {
                // 물체는 5장 내내 진짜 물리 물체다.
                // 4장처럼 잡을 때 isKinematic을 껐다 켜지 않는다.
                _objectBody.position = world;
                _objectBody.rotation = Quaternion.identity;
                _objectBody.linearVelocity = Vector3.zero;
                _objectBody.angularVelocity = Vector3.zero;
                _objectBody.WakeUp();
            }

            MovePlaceTarget(objLocal);

            // 옮긴 결과를 물리 엔진에 알려준다. 안 하면 겹침 검사가 옛 위치로 계산된다.
            Physics.SyncTransforms();
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
            => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // ── 관측 ─────────────────────────────────────────────────────

        public override void CollectObservations(VectorSensor sensor)
        {
            // 4장과 똑같은 16개
            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedAngle(i));                    // 3

            for (int i = 0; i < arm.JointCount; i++)
                sensor.AddObservation(arm.NormalizedVelocity(i));                 // 3

            // 4장에서는 팔 끝 위치였다. 5장에서는 집게가 팔 끝보다 아래에 있으므로
            // "손가락 사이"를 알려준다. 물체를 가져다 대야 하는 자리가 그곳이다.
            sensor.AddObservation(arm.ToNormalizedLocal(gripper.GripCenter));     // 3
            sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));  // 3
            sensor.AddObservation(_isHolding ? 1f : 0f);                          // 1

            // ★ 4장에서는 "놓을 자리"를 알려줬다. 잡기 전에는 쓸모없는 정보였다.
            //   5장에서는 <b>지금 가야 할 점</b>을 알려준다. 개수는 그대로 3개지만
            //   단계가 바뀔 때마다 뜻이 바뀐다 — 물체 위 / 들어올릴 높이 / 놓을 자리 위.
            //   다단계 과제에서는 "무엇을 관측에 넣느냐"가 개수보다 중요하다.
            sensor.AddObservation(arm.ToNormalizedLocal(CurrentGoal()));          // 3

            // ★ 5장에서 추가하는 3개 — 손의 감각.
            //
            //   4장에서는 잡았는지가 확실했다. 프로그램이 붙였으니까.
            //   5장에서는 확실하지 않다. 물렸는지 미끄러지는 중인지 알 수 없다.
            //   그래서 손가락이 얼마나 오므라들었는지, 양쪽이 닿았는지를 알려준다.
            sensor.AddObservation(gripper.GripValue);                             // 1
            sensor.AddObservation(gripper.contactL.Touching ? 1f : 0f);           // 1
            sensor.AddObservation(gripper.contactR.Touching ? 1f : 0f);           // 1
        } // = 19

        // ── 행동 ─────────────────────────────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 연속 액션: 관절 회전. 4장과 완전히 같다.
            var continuous = actions.ContinuousActions;
            for (int i = 0; i < arm.JointCount; i++)
            {
                arm.Drive(i, continuous[i], Time.fixedDeltaTime);
            }

            // 이산 액션: 집게를 오므리거나 편다.
            // 4장의 잡기/놓기와 같은 자리지만 뜻이 다르다.
            // 여기서는 손가락 모터를 돌리라는 명령일 뿐이고,
            // 실제로 잡히는지는 물리가 정한다.
            gripper.Drive(actions.DiscreteActions[0] == 1);
            gripper.UpdateContacts();

            UpdateHoldingState();
            GiveReward();
            UpdateHud();
        }

        /// <summary>
        /// 물고 있는지 다시 판단한다.
        ///
        /// 4장의 Grab()/Release()가 하던 일을 이 함수 하나가 대신한다.
        /// 다만 <b>우리가 상태를 바꾸는 게 아니라, 물리가 만든 결과를 읽을 뿐이다.</b>
        /// </summary>
        void UpdateHoldingState()
        {
            bool nowHolding = gripper.BothTouching;

            if (nowHolding && !_isHolding)
            {
                _hasGrabbed = true;

                if (!_grabRewarded)
                {
                    // 물기를 시도할 이유를 만들어준다. 한 에피소드에 한 번만.
                    // 없으면 물었다 놨다를 반복해 보상만 긁어모으는 것을 배운다.
                    AddReward(grabReward);
                    _grabRewarded = true;
                }
            }
            else if (!nowHolding && _isHolding)
            {
                // 일부러 놓았거나, 너무 세게 움직여서 빠졌거나.
                // 둘을 구분하지 않는다. 결과가 같기 때문이다.
                if (!IsOverPlaceTarget()) AddReward(-dropPenalty);
            }

            if (nowHolding != _isHolding)
            {
                _isHolding = nowHolding;

                // 목표가 바뀌었으니 거리 기준을 새로 잡는다.
                // 안 하면 기준이 바뀌는 그 한 스텝에 보상이 크게 튄다.
                _previousDistance = DistanceToGoal();
            }
        }

        // ── 보상 (4장과 같은 공식) ───────────────────────────────────

        bool IsOverPlaceTarget()
            => HorizontalDistance(arm.ToLocal(targetObject.position),
                                  arm.ToLocal(placeTarget.position)) < placeRadius;

        bool IsObjectAtRest()
            => _objectBody == null || _objectBody.linearVelocity.magnitude < restSpeed;

        // ── 단계 ─────────────────────────────────────────────────────
        //
        // 과제를 네 토막으로 나눈다. 단계마다 <b>가야 할 점 하나</b>가 정해지고,
        // 보상 공식은 여전히 "그 점에 가까워진 만큼" 하나뿐이다.
        //
        //   ① 잡으러 간다 — 물체 <b>위</b>로 간다. 정렬되면 목표가 내려온다
        //   ② 들어올린다  — 물었으면 수직으로 들어올린다
        //   ③ 옮긴다      — 놓을 자리 <b>위</b>로 간다. 정렬되면 내려온다
        //   ④ 놓는다      — 손을 펴면 끝
        //
        // ★ ①과 ③에서 "위로 먼저"가 핵심이다.
        //   자석(4장)은 아무 방향에서나 닿기만 하면 됐다.
        //   집게는 옆에서 돌진하면 <b>물체를 밀어내 버린다.</b>
        //   밀린 물체를 또 쫓아가니 거리가 영영 줄지 않는다.
        //   실제로 이것 때문에 800만 스텝을 돌려도 보상이 -1에서 꿈쩍하지 않았다.

        /// <summary>지금 움직이고 있는 것 — 물기 전엔 집게, 문 뒤엔 물체.</summary>
        Vector3 Mover => _isHolding ? targetObject.position : gripper.GripCenter;

        /// <summary>
        /// 어떤 지점의 <b>위쪽</b>을 노린다.
        /// 수평으로 멀면 높이 뜬 점을, 가까워질수록 낮은 점을 목표로 준다.
        /// 그래서 "위로 돌아 내려오는" 경로가 저절로 만들어진다.
        /// </summary>
        Vector3 Above(Vector3 spot)
        {
            float horizontal = HorizontalDistance(Mover, spot);
            float lift = Mathf.Clamp01(horizontal / Mathf.Max(0.01f, alignRadius)) * hoverHeight;
            return spot + Vector3.up * lift;
        }

        /// <summary>지금 가야 할 점. 단계가 바뀌면 이 점이 바뀐다.</summary>
        Vector3 CurrentGoal()
        {
            // ① 잡으러 간다
            if (!_isHolding) return Above(targetObject.position);

            // ② 들어올린다 — 옆으로 끌지 말고 먼저 똑바로 든다
            if (targetObject.position.y < liftHeight)
                return new Vector3(targetObject.position.x, liftHeight, targetObject.position.z);

            // ③ 옮긴다 — 놓을 자리 위로
            return Above(new Vector3(placeTarget.position.x, arm.spawnHeight, placeTarget.position.z));
        }

        /// <summary>화면에 보여줄 단계 이름.</summary>
        string PhaseName
        {
            get
            {
                if (!_hasGrabbed) return "① 잡으러 간다";
                if (!_isHolding) return "④ 놓았다";
                if (targetObject.position.y < liftHeight) return "② 들어올린다";
                return "③ 옮긴다";
            }
        }

        /// <summary>현재 목표까지 남은 거리.</summary>
        float DistanceToGoal() => Vector3.Distance(Mover, CurrentGoal());

        /// <summary>
        /// 물체가 되돌릴 수 없는 곳으로 갔는가.
        ///
        /// ★ 이 검사가 없어서 학습이 망가졌다.
        ///   집게가 물체를 쳐서 테이블 밖으로 떨어뜨리면, 물체는 계속 낙하한다.
        ///   목표까지의 거리가 매 스텝 멀어지니 진전 보상이 계속 큰 음수가 되고,
        ///   에피소드는 3000스텝을 꽉 채운다.
        ///   실제로 한 에피소드 보상이 <b>-144</b>까지 내려갔다.
        ///
        ///   끝낼 수 없는 상황은 <b>끝내줘야 한다.</b>
        /// </summary>
        bool IsObjectLost()
        {
            Vector3 local = arm.ToLocal(targetObject.position);

            if (local.y < -0.3f) return true;                   // 테이블 아래로 떨어졌다

            // ★ "팔 길이 안"이라고 집을 수 있는 게 아니다.
            //
            //   집게는 물체 <b>위</b>에서 내려와야 하므로 실제 작업 범위는
            //   팔이 닿는 범위보다 훨씬 좁다. 그 밖으로 밀려나면 에이전트는
            //   갈 수 없는 곳을 향해 3000스텝을 헛돈다(timeout).
            //
            //   팔이 물체를 밀고 다니다가 이 구역을 벗어나는 일이 실제로 잦았다.
            //   그래서 <b>작업 범위</b>를 따로 두고, 벗어나면 판을 새로 연다.
            float distance = new Vector2(local.x, local.z).magnitude;
            if (distance < workRange.x || distance > workRange.y) return true;

            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Abs(yaw) > workYawLimit;
        }

        void GiveReward()
        {
            // 물체를 잃었으면 더 볼 것도 없다. 벌점을 주고 새로 시작한다.
            if (IsObjectLost())
            {
                AddReward(-lostPenalty);
                EndEpisode();
                return;
            }

            float distance = DistanceToGoal();

            if (useDenseReward)
            {
                // ★ 한 스텝에 받을 수 있는 진전 보상을 제한한다.
                //   물체가 튕겨 나가면 거리가 한 번에 몇 미터씩 벌어지는데,
                //   그걸 그대로 보상에 넣으면 한 스텝이 에피소드 전체를 압도한다.
                //   (Value Loss가 116까지 치솟았던 이유다)
                float progress = (_previousDistance - distance) / arm.reach;
                AddReward(Mathf.Clamp(progress, -maxStepReward, maxStepReward));

                if (MaxStep > 0) AddReward(-1f / MaxStep);
            }

            _previousDistance = distance;

            // ── 단계를 넘어갈 때마다 한 번씩 보너스 ──────────────────
            // 이 보너스들이 다음 단계로 건너가는 <b>다리</b>다. 없으면 못 넘어간다.

            // ⓪ 물체 바로 위에 집게를 정렬했다 — 첫 번째 다리
            if (!_approachRewarded && !_hasGrabbed
                && Vector3.Distance(gripper.GripCenter, targetObject.position) < approachRadius)
            {
                AddReward(approachReward);
                _approachRewarded = true;
            }

            // ② 들어올렸다
            if (!_liftRewarded && _isHolding && targetObject.position.y >= liftHeight)
            {
                AddReward(liftReward);
                _liftRewarded = true;
            }

            // 4장과 같은 네 가지 조건.
            if (_hasGrabbed && !_isHolding && IsOverPlaceTarget() && IsObjectAtRest())
            {
                AddReward(placeReward);
                _successCount++;
                _roundCount++;
                StartNextRound();
            }
        }

        /// <summary>
        /// 한 번 옮기는 데 성공했다. <b>에피소드를 끝내지 않고</b>
        /// 놓을 자리를 새 랜덤 위치로 옮겨 다시 시키다.
        ///
        /// ── 왜 끝내지 않는가 ────────────────────────────────────────
        ///
        /// 성공할 때마다 에피소드를 끝내면, 한 판에서 배우는 것이 <b>한 번</b>뿐이다.
        /// 게다가 매번 같은 자리에서 시작하니 늘 비슷한 상황만 본다.
        ///
        /// 계속 이어가면
        ///   - 한 판에서 여러 번 배운다 (경험이 촘촘해진다)
        ///   - 물체가 <b>어디에 놓여 있든</b> 집어야 한다. 직전에 자기가 놓은 자리다
        ///   - "옮기고 나면 또 옮긴다"는 실제 작업에 가까워진다
        ///
        /// 물체는 방금 놓은 자리에 그대로 두고, <b>목표만</b> 옮긴다.
        /// </summary>
        void StartNextRound()
        {
            // 물체는 방금 놓은 자리에 그대로 두고, 목표만 새 랜덤 자리로 옮긴다.
            MovePlaceTarget(arm.ToLocal(targetObject.position));

            // 단계 보너스를 다시 받을 수 있게 되돌린다.
            // 이걸 빼먹으면 두 번째 사이클부터는 보너스가 없어서
            // "한 번만 하고 가만히 있는 것"이 이득이 된다.
            _hasGrabbed = false;
            _grabRewarded = false;
            _liftRewarded = false;
            _approachRewarded = false;

            _previousDistance = DistanceToGoal();
        }

        void UpdateHud()
        {
            if (hud == null) return;

            string state = _isHolding ? "[물고있음]"
                : (gripper.contactL.Touching || gripper.contactR.Touching ? "[한쪽만 닿음]" : "[빈손]");

            hud.statusLine = string.Format("{0} {1}  쥠 {2:F2}  에피 {3}  성공 {4}  보상 {5:F2}",
                PhaseName, state, gripper.GripValue,
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
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = RobotArmInput.Grip ? 1 : 0;
        }
    }
}
