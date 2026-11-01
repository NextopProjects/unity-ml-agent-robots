using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Humanoid
{
    // 걸어가서 상자를 양팔로 집어 들고, 무작위로 고른 작업대에 내려놓기.
    // StandAgent 를 상속한다 — 관측 48 · 행동 18 · 넘어지면 끝은 그대로, 상자 · 놓을 자리 관측과 보상이 더해진다.
    // RobotArms 5장과 같은 구조다: 단계마다 "지금 가야 할 점"이 바뀌고, 그 점에 가까워진 만큼 보상한다.
    //   ① 손(두 아래팔 끝 가운데)을 상자로  ② 양팔로 잡고 들어 올린다  ③ 상자를 놓을 자리 위로  ④ 내려놓고 팔을 뗀다
    // 다가간 보상은 "그 단계에서 지금까지 가장 가까웠던 거리"를 갱신할 때만 준다 — 오갔다 하며 쌓을 수 없게.
    // 잡았는지는 물리가 정한다(양쪽 아래팔이 상자에 닿음). 상자를 붙이거나 옮기는 코드는 없다.
    public class CarryAgent : StandAgent
    {
        [Header("연결")]
        public CarryBox box;
        public Transform pickTable;          // 상자를 올려 두는 작업대
        public Transform placeTable;         // 놓을 작업대 (판마다 후보 자리 중 하나로 옮긴다)
        public Renderer placeMarker;         // 놓을 자리 표시. 지난 판 결과를 색으로 보여 준다

        [Header("난이도 — 최근 20판 성공률이 70% 를 넘으면 스스로 한 단계 올린다")]
        public int level;                    // 0 ~ 3
        public bool autoLevel = true;        // 끄면 level 고정 (평가할 때)
        // 단계별 (상자까지 거리 m, 상자 방향 ±도, 상자 작업대 → 놓을 작업대 m). 0 = 걷지 않고 팔이 닿는다
        static readonly Vector3[] Levels = { new Vector3(0.45f, 0f, 0.8f), new Vector3(1.0f, 30f, 1.2f), new Vector3(2.0f, 90f, 2.0f), new Vector3(3.5f, 180f, 3.0f) };

        [Header("보상")]
        public float graspReward = 1f;       // 처음 양팔로 잡았을 때 (판에 한 번)
        public float liftReward = 1f;        // 처음 0.5초 동안 들고 있었을 때 (판에 한 번)
        public float placeReward = 3f;       // 놓기 성공 — 판이 끝난다
        public float dropPenalty = 2f;       // 상자를 바닥에 떨어뜨림 — 판이 끝난다. 잡기 + 들기(2)만큼: 잡고 밀어 떨어뜨리기가 이득이 되지 않게
        public float fallPenalty = 1f;       // 넘어짐 — 판이 끝난다
        public float standReward = 0.0005f;  // 서 있는 동안 매 스텝 × 서 있는 정도 (60초 다 받아도 1.5 — 성공 3보다 작게)
        public float maxJointTorque = 300f;  // 관절 모터 힘 상한 (5장과 같다)

        // 실측으로 정한 값
        const float TableTop = 1.06f;        // 작업대 윗면 높이. 서서 팔을 앞으로 뻗은 높이(약 1.16)에 상자 가운데가 오게
        const float BoxHalf = 0.10f;         // 상자 높이의 절반
        const float LiftHeight = 0.08f;      // 작업대보다 이만큼 들면 "들었다"
        const float PlaceRadius = 0.15f;     // 놓을 자리 가운데에서 이 안이면 성공
        const float MaxStepReward = 0.05f;   // 한 스텝 진전 보상 상한 (RobotArms 와 같다)
        const float LiftWeight = 5f;         // 들기 단계는 거리가 8cm 뿐이라 다가간 보상을 5배로 (안 그러면 0.1점 — 실측: 500만 스텝까지 들기 0%)
        static readonly float[] PlaceAngles = { -120f, -90f, -60f, 60f, 90f, 120f };   // 놓을 자리 후보 (상자 작업대 둘레)

        BodyPart _forearmL, _forearmR;
        readonly float[] _lastActions = new float[18];
        float _actionChange;
        readonly float[] _best = new float[3];   // 단계별 지금까지 가장 가까웠던 거리
        bool _grasped, _lifted;
        bool _graspPaid, _liftPaid;          // 이번 판에 잡기 · 들기 보상을 줬나 (판에 한 번씩만)
        int _restSteps, _liftSteps;

        // 판 결과 (성적표 · 표시)
        enum Result { None, Success, Dropped, Fell, TimeOut }
        Result _result;
        readonly GaitStats _gait = new GaitStats();
        float _placeError;
        int _episodes, _successes;
        readonly Queue<bool> _recent = new Queue<bool>();   // 최근 20판 성공 여부 (난이도 올리기)

        // 끝난 판 하나의 요약 — 평가 메뉴(CarryEvaluation)가 읽는다
        public struct Summary { public string result; public bool grasped, lifted; public float placeError, seconds, tilt; }
        public Summary Last { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            foreach (var p in GetComponentsInChildren<BodyPart>())
            {
                if (p.name == "ForearmL") _forearmL = p;
                if (p.name == "ForearmR") _forearmR = p;
            }
        }

        // 손 = 두 아래팔 끝의 가운데. 상자를 잡으면 상자가 이 사이에 있다.
        Vector3 Hands => (Tip(_forearmL) + Tip(_forearmR)) * 0.5f;
        static Vector3 Tip(BodyPart part)
        {
            var c = part.GetComponent<BoxCollider>();
            return part.transform.TransformPoint(c.center + Vector3.down * c.size.y * 0.5f);
        }

        Vector3 PlaceSpot => new Vector3(placeTable.position.x, TableTop + BoxHalf, placeTable.position.z);

        // ── 판 시작 ─────────────────────────────────────────────────

        public override void OnEpisodeBegin()
        {
            RecordStats();
            base.OnEpisodeBegin();       // 몸을 처음 자세로
            body.SetMaxForce(maxJointTorque);
            PlaceTables();

            System.Array.Clear(_lastActions, 0, _lastActions.Length);
            _actionChange = 0f;
            _grasped = _lifted = false;
            _restSteps = _liftSteps = 0;
            _result = Result.TimeOut;    // 끝날 때까지 다른 결과가 없으면 시간 초과
            _gait.Clear();
            _best[0] = _best[1] = _best[2] = float.MaxValue;
        }

        // 상자 작업대: 몸 정면 ±(단계의 방향) 쪽 (단계의 거리). 놓을 작업대: 그 둘레 후보 중 하나.
        // 후보는 몸 쪽에서 본 옆 · 비스듬히 뒤 — 놓을 자리가 상자 작업대 너머에 숨지 않게.
        void PlaceTables()
        {
            Vector3 lv = Levels[Mathf.Clamp(level, 0, Levels.Length - 1)];
            Vector3 origin = transform.position;     // 몸이 서는 자리 (Humanoid 뿌리)
            Quaternion facing = Quaternion.Euler(0f, Random.Range(-lv.y, lv.y), 0f);
            Vector3 pick = origin + facing * Vector3.forward * lv.x;
            pickTable.position = new Vector3(pick.x, origin.y + TableTop * 0.5f, pick.z);

            Vector3 back = (origin - pick).normalized;      // 상자 작업대에서 몸 쪽
            Vector3 dir = Quaternion.Euler(0f, PlaceAngles[Random.Range(0, PlaceAngles.Length)], 0f) * back;
            Vector3 place = pick + new Vector3(dir.x, 0f, dir.z).normalized * lv.z;
            placeTable.position = new Vector3(place.x, origin.y + TableTop * 0.5f, place.z);

            box.transform.SetPositionAndRotation(new Vector3(pick.x, origin.y + TableTop + BoxHalf, pick.z), facing);
            box.rb.position = box.transform.position;
            box.rb.rotation = box.transform.rotation;
            box.rb.linearVelocity = Vector3.zero;
            box.rb.angularVelocity = Vector3.zero;
            box.ResetContacts();
            Physics.SyncTransforms();
        }

        // ── 관측 48 + 17 = 65 ───────────────────────────────────────
        // 위치는 전부 "내가 보는 방향" 기준. 어느 쪽을 보든 같은 상황이면 같은 숫자가 나오게 (4장 목표 관측과 같다).

        public override void CollectObservations(VectorSensor sensor)
        {
            base.CollectObservations(sensor);                                            // 48 서기
            Quaternion toBody = Quaternion.Inverse(HumanoidMath.HeadingRotation(body.pelvis.transform.forward));
            Vector3 pelvis = body.pelvis.transform.position;
            sensor.AddObservation(toBody * (box.transform.position - pelvis));           // 3 상자
            sensor.AddObservation(toBody * (PlaceSpot - pelvis));                        // 3 놓을 자리
            sensor.AddObservation(toBody * (PlaceSpot - box.transform.position));        // 3 상자 → 놓을 자리
            sensor.AddObservation(toBody * (box.transform.position - Hands));            // 3 손 → 상자
            sensor.AddObservation(toBody * box.rb.linearVelocity);                       // 3 상자 속도
            sensor.AddObservation(box.touchL);                                           // 1 왼팔이 닿음
            sensor.AddObservation(box.touchR);                                           // 1 오른팔이 닿음
        } // = 65

        // ── 행동 · 보상 ─────────────────────────────────────────────

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 행동이 확 바뀐 크기 (5장 GaitAgent 와 같다)
            var a = actions.ContinuousActions;
            float change = 0f;
            for (int i = 0; i < _lastActions.Length; i++)
            {
                float v = Mathf.Clamp(a[i], -1f, 1f);
                change += (v - _lastActions[i]) * (v - _lastActions[i]);
                _lastActions[i] = v;
            }
            if (change > 0f) _actionChange = change / _lastActions.Length;

            // 넘어지면 벌점. 없으면 "1초 만에 몸을 던져 잡고 들기(+2) → 넘어짐"이 배워진다 (실측: 300만 스텝까지 넘어짐 100%)
            if (body.HasFallen) { _result = Result.Fell; AddReward(-fallPenalty); }
            base.OnActionReceived(actions);     // 행동 → 몸 · StepReward · 넘어지면 끝
        }

        protected override float StepReward()
        {
            _grasped = box.Grasped;
            _lifted = _grasped && box.transform.position.y > TableTop + BoxHalf + LiftHeight;
            _gait.Step(body, Flat(GoalPoint() - body.pelvis.transform.position).normalized, Time.fixedDeltaTime);
            box.Show(_lifted);

            float r = 0f;
            if (_grasped && !_graspPaid) { r += graspReward; _graspPaid = true; }
            // 들기 보상은 0.5초 동안 들고 있어야 준다 — 순간 들어 올렸다 놓치기로는 못 받게
            _liftSteps = _lifted ? _liftSteps + 1 : 0;
            if (_liftSteps >= 25 && !_liftPaid) { r += liftReward; _liftPaid = true; }

            // 가야 할 점에 가까워진 만큼 — 이 단계의 최고 기록을 갱신할 때만. 단계에 처음 들어온 스텝은 기록만 한다.
            // 한 스텝 상한을 둔다 — 상자를 던지거나 몸을 던지는 큰 움직임이 이득이 되지 않게
            int stage = _lifted ? 2 : _grasped ? 1 : 0;
            float d = GoalDistance();
            float progress = 0f;
            if (_best[stage] == float.MaxValue) _best[stage] = d;
            else if (d < _best[stage])
            {
                progress = Mathf.Min(_best[stage] - d, MaxStepReward) * (stage == 1 ? LiftWeight : 1f);
                _best[stage] = d;
            }
            // 다가간 보상만 자세 · 떨림만큼 깎는다 (5장과 같은 생각: 숙이고 덜덜 떨며 가면 덜 받는다)
            progress *= HumanoidMath.Upright(body.torso.transform.up) / (1f + _actionChange);
            r += progress;
            r += standReward * base.StepReward();      // 서 있는 정도 (0~1, 3장 서기 보상)

            // 판 끝. 보상을 먼저 넣고 끝낸다 — EndEpisode 뒤에 돌려주면 다음 판의 보상이 된다
            if (box.transform.position.y < 0.5f)                         // 바닥에 떨어졌다 (작업대 윗면 1.06)
            {
                _result = Result.Dropped;
                AddReward(r - dropPenalty);
                EndEpisode();
                return 0f;
            }
            if (Placed())
            {
                _result = Result.Success;
                _placeError = Flat(box.transform.position - PlaceSpot).magnitude;
                AddReward(r + placeReward);
                EndEpisode();
                return 0f;
            }
            return r;
        }

        // 놓기 성공: 상자가 놓을 자리 안 · 작업대 위에 멈춰 있고, 양팔을 뗀 채로 0.5초.
        // "목표에 닿았다"가 아니라 정말 놓였는지를 본다 — 들고 서 있기만 하거나, 굴러가는 중에는 성공이 아니다.
        bool Placed()
        {
            Vector3 p = box.transform.position;
            bool onSpot = Flat(p - PlaceSpot).magnitude < PlaceRadius && Mathf.Abs(p.y - PlaceSpot.y) < 0.03f;
            bool still = box.rb.linearVelocity.magnitude < 0.1f;
            bool released = !box.touchL && !box.touchR;
            _restSteps = onSpot && still && released ? _restSteps + 1 : 0;
            return _restSteps >= 25;
        }

        // 지금 가야 할 점: 잡기 전 = 상자(손으로), 잡았지만 안 듦 = 들어 올린 높이, 들었음 = 놓을 자리 위(가까울수록 낮게)
        Vector3 GoalPoint()
        {
            Vector3 b = box.transform.position;
            if (!_grasped) return b;
            if (!_lifted) return new Vector3(b.x, TableTop + BoxHalf + LiftHeight + 0.02f, b.z);
            float far = Mathf.Clamp01(Flat(b - PlaceSpot).magnitude / 0.4f);
            return PlaceSpot + Vector3.up * (LiftHeight + 0.02f) * far;
        }

        float GoalDistance()
        {
            Vector3 from = _grasped ? box.transform.position : Hands;
            return Vector3.Distance(from, GoalPoint());
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ── 성적표 · 표시 ────────────────────────────────────────────

        void RecordStats()
        {
            if (_episodes++ == 0) { _graspPaid = _liftPaid = false; return; }     // 첫 판 전에는 기록이 없다
            if (_result == Result.Success) _successes++;
            LevelUp(_result == Result.Success);
            Last = new Summary { result = _result.ToString(), grasped = _graspPaid, lifted = _liftPaid,
                                 placeError = _result == Result.Success ? _placeError : -1f, seconds = _gait.time, tilt = _gait.Tilt };
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Carry/Success", _result == Result.Success ? 1f : 0f);
            stats.Add("Carry/Grasped", _graspPaid ? 1f : 0f);
            stats.Add("Carry/Lifted", _liftPaid ? 1f : 0f);
            stats.Add("Carry/Dropped", _result == Result.Dropped ? 1f : 0f);
            stats.Add("Carry/Fell", _result == Result.Fell ? 1f : 0f);
            if (_result == Result.Success) stats.Add("Carry/PlaceError", _placeError);
            stats.Add("Carry/Level", level);
            stats.Add("Gait/Tilt", _gait.Tilt);
            stats.Add("Gait/Slip", _gait.Slip);

            // 놓을 자리 색 = 지난 판 결과: 초록 성공 · 빨강 떨어뜨림 · 주황 넘어짐 · 회색 시간 초과
            if (placeMarker != null)
            {
                var colors = new MaterialPropertyBlock();
                Color c = _result == Result.Success ? Color.green : _result == Result.Dropped ? Color.red
                        : _result == Result.Fell ? new Color(1f, 0.5f, 0f) : Color.gray;
                colors.SetColor("_BaseColor", c);
                colors.SetColor("_Color", c);
                placeMarker.SetPropertyBlock(colors);
            }
            _graspPaid = _liftPaid = false;
        }

        // 쉬운 단계를 잘하게 되면 다음 단계로. 보상 크기가 아니라 성공률로 정한다
        // (보상으로 정했더니 성공 0 인데도 단계가 올라 걷지도 못하는 몸에 2m 떨어진 상자를 줬다 — 실측)
        void LevelUp(bool success)
        {
            _recent.Enqueue(success);
            if (_recent.Count > 20) _recent.Dequeue();
            if (!autoLevel || level >= Levels.Length - 1 || _recent.Count < 20) return;
            int n = 0;
            foreach (bool s in _recent) if (s) n++;
            if (n < 14) return;
            level++;
            _recent.Clear();
        }

        protected override string Status() =>
            base.Status() + string.Format("\n난이도 {3}  {0}  성공 {1} / {2}판",
                _lifted ? "③ 옮기는 중" : _grasped ? "② 잡음 — 들어 올려라" : "① 상자로", _successes, Mathf.Max(0, _episodes - 1), level);
    }
}
