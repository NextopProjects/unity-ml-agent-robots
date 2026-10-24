using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Humanoid;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.SideChannels;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BodyPart = Humanoid.BodyPart;

namespace HumanoidEditor
{
    // 실습 테스트. 임시 씬에 몸(과 에이전트)을 만들어 학습자가 작성한 코드를 실제로 불러 보고, 요구사항별로 확인한다.
    // 케이스 하나 = 요구사항 하나를 확인하는 상황. 실패하면 그 요구사항의 수정 방향(fix)을 보여 준다.
    // 규칙: ① 시작 코드에서는 하나 이상 실패해야 한다  ② 이름에 (경계)가 붙은 케이스를 넣는다
    // 물리는 돌리지 않는다 (돌리면 학습자 씬의 몸도 같이 움직인다). 속도 · 바닥 접촉은 값을 직접 넣어 만든다.
    internal static class BlankCheck
    {
        public struct Result { public string req, label, expected, actual, fix, why; public bool pass; }

        struct Case { public string id, req, label, fix; public Func<(object expected, object actual)> run; }

        static readonly List<Case> Cases = new List<Case>();

        static void Add(string id, string req, string fix, params (string label, Func<(object, object)> run)[] cases)
        {
            foreach (var c in cases) Cases.Add(new Case { id = id, req = req, fix = fix, label = c.label, run = c.run });
        }

        // ── 테스트용 몸 ─────────────────────────────────────────────

        // 임시 씬에 1장과 같은 몸을 만든다. kind: 0 = 몸만, 1 = 서기 에이전트, 2 = 걷기 에이전트. 끝나면 통째로 지운다.
        sealed class Fix : IDisposable
        {
            public HumanoidBody body;
            public StandAgent agent;
            public WalkAgent walk;
            public KeyboardControl keyboard;
            public Transform target;
            readonly Scene _scene, _prev;
            readonly int _undo;

            public Fix(int kind = 0, bool withKeyboard = false, bool academy = false)
            {
                Undo.IncrementCurrentGroup();
                _undo = Undo.GetCurrentGroup();
                _prev = SceneManager.GetActiveScene();
                // 저장된 씬이면 임시 씬을 덧붙여 거기에 만든다. 저장 안 된 새 씬에는 덧붙일 수 없어서 그 씬에 만들고 Undo 로 지운다.
                if (!string.IsNullOrEmpty(_prev.path))
                {
                    _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(_scene);
                }
                try { Build(kind, withKeyboard, academy); }
                catch { Dispose(); throw; }                     // 만들다 실패해도 임시 씬이 남지 않게
            }

            void Build(int kind, bool withKeyboard, bool academy)
            {
                var area = Ch1_Body.Area();
                Ch1_Body.Ground(area);
                var root = Ch1_Body.Root(area);
                var pelvis = Ch1_Body.Pelvis(root);
                var torso = Ch1_Body.Torso(pelvis);
                Ch1_Body.WaistJoint(torso);
                var thighL = Ch1_Body.Thigh(pelvis, -0.10f);
                Ch1_Body.HipJoint(thighL);
                var armL = Ch1_Body.UpperArm(torso, -0.23f);
                Ch1_Body.ShoulderJoint(armL);
                Ch1_Body.RestOfLegs(thighL);
                Ch1_Body.RestOfArms(armL);
                Ch1_Body.Wire(root);

                body = root.GetComponent<HumanoidBody>();
                body.spring = 4000f;
                body.damper = 400f;
                if (withKeyboard) { keyboard = Undo.AddComponent<KeyboardControl>(root); keyboard.driveBody = false; }
                Call(body, "Awake");                            // 에디트 모드라 Unity 가 부르지 않는다
                if (keyboard != null) Call(keyboard, "Start");

                if (kind > 0)
                {
                    agent = kind == 2 ? (StandAgent)Undo.AddComponent<WalkAgent>(root) : Undo.AddComponent<StandAgent>(root);
                    walk = agent as WalkAgent;
                    var bp = root.GetComponent<BehaviorParameters>();
                    bp.BehaviorName = "Test";
                    bp.BrainParameters.VectorObservationSize = kind == 2 ? 51 : 48;
                    bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(18);
                    bp.BehaviorType = BehaviorType.HeuristicOnly;
                    agent.pushInterval = 0;
                    if (walk != null)
                    {
                        target = new GameObject("Target").transform;
                        Undo.RegisterCreatedObjectUndo(target.gameObject, "Target");
                        target.SetParent(area.transform, false);
                        target.localPosition = new Vector3(0f, 0.25f, 6f);
                        walk.target = target;
                        walk.targetSpeed = 1f;
                    }
                    if (academy) agent.LazyInitialize();         // EndEpisode · StatsRecorder 를 쓰려면 ML-Agents 가 준비돼 있어야 한다
                    else agent.Initialize();
                }
                Physics.SyncTransforms();
            }

            public BodyPart Part(string name) => body.GetComponentsInChildren<BodyPart>().First(p => p.name == name);
            public Quaternion TargetOf(string part) => Part(part).joint.targetRotation;

            public void Dispose()
            {
                if (Academy.IsInitialized) Academy.Instance.Dispose();
                Undo.RevertAllDownToGroup(_undo);                // 만든 것 되돌리기 (Undo 기록도 남기지 않게)
                if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
                if (_prev.IsValid()) SceneManager.SetActiveScene(_prev);
            }
        }

        // ── 반사(reflection) 도우미 — private 멤버를 테스트에서 읽고 쓴다 ──

        const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static MemberInfo Member(Type t, string name)
        {
            for (; t != null; t = t.BaseType)
            {
                var m = (MemberInfo)t.GetField(name, All) ?? t.GetMethod(name, All);
                if (m != null) return m;
            }
            throw new MissingMemberException(name);
        }

        static object Call(object o, string method, params object[] args) => ((MethodInfo)Member(o.GetType(), method)).Invoke(o, args);
        static object Get(object o, string field) => ((FieldInfo)Member(o.GetType(), field)).GetValue(o);
        static void Set(object o, string field, object value) => ((FieldInfo)Member(o.GetType(), field)).SetValue(o, value);

        static ActionBuffers Act(params (int i, float v)[] set)
        {
            var c = new float[18];
            foreach (var s in set) c[s.i] = s.v;
            return new ActionBuffers(c, new int[0]);
        }

        static List<float> Observe(StandAgent agent, int size)
        {
            var sensor = new VectorSensor(size);
            agent.CollectObservations(sensor);
            return ((IEnumerable<float>)Call(sensor, "GetObservations")).ToList();
        }

        static Dictionary<string, float> SentStats()
        {
            var queue = (List<byte[]>)Get(Get(Academy.Instance.StatsRecorder, "m_Channel"), "MessageQueue");
            var stats = new Dictionary<string, float>();
            foreach (var bytes in queue)
                using (var msg = new IncomingMessage(bytes)) stats[msg.ReadString()] = msg.ReadFloat32();
            return stats;
        }

        static void ClearStats() => ((List<byte[]>)Get(Get(Academy.Instance.StatsRecorder, "m_Channel"), "MessageQueue")).Clear();

        static Dictionary<string, Func<float>> Curriculum() =>
            (Dictionary<string, Func<float>>)Get(Get(Academy.Instance.EnvironmentParameters, "m_Channel"), "m_Parameters");

        // 몸이 처음과 다른 상태가 되게 흔들어 둔다
        static void Disturb(Fix f)
        {
            var t = f.Part("ThighL").transform;
            t.localPosition += new Vector3(0.3f, 0.2f, 0.1f);
            t.localRotation = Quaternion.Euler(40f, 0f, 10f);
            f.Part("Pelvis").transform.position += new Vector3(0.5f, -0.3f, 0.4f);
            Physics.SyncTransforms();
        }

        static Quaternion E(float x, float z) => Quaternion.Euler(x, 0f, z);

        static BlankCheck()
        {
            // ── 1-1 ResetPose ──
            Add("1-1", "위치를 처음 자리로", "transform.localPosition 에 _startPosition 을 넣는다 (부모 기준 = local).",
                ("옮겨진 허벅지 → 처음 자리", () => { using (var f = new Fix()) { var p = f.Part("ThighL"); var start = p.transform.localPosition; Disturb(f); p.ResetPose(); return (start, p.transform.localPosition); } }));
            Add("1-1", "방향을 처음 방향으로", "transform.localRotation 에 _startRotation 을 넣는다.",
                ("돌아간 허벅지 → 처음 방향", () => { using (var f = new Fix()) { var p = f.Part("ThighL"); var start = p.transform.localRotation; Disturb(f); p.ResetPose(); return (start, p.transform.localRotation); } }));
            Add("1-1", "움직임을 멈춘다", "rb.linearVelocity · rb.angularVelocity 에 Vector3.zero 를 넣는다. 안 지우면 넘어지던 기세 그대로 다시 넘어진다.",
                ("움직이던 허벅지 → 속도 0", () => { using (var f = new Fix()) { var p = f.Part("ThighL"); p.rb.linearVelocity = new Vector3(1, 2, 3); p.rb.angularVelocity = new Vector3(3, 0, 1); p.ResetPose(); return (0f, p.rb.linearVelocity.magnitude + p.rb.angularVelocity.magnitude); } }));
            Add("1-1", "바닥 기록을 지운다", "touchingGround = false. 안 지우면 새 판이 시작되자마자 '넘어짐'으로 끝난다.",
                ("바닥에 닿았던 기록 (경계) → 지워짐", () => { using (var f = new Fix()) { var p = f.Part("ThighL"); p.touchingGround = true; p.ResetPose(); return (false, p.touchingGround); } }));

            // ── 1-2 HasFallen ──
            Add("1-2", "서 있으면 넘어지지 않았다", "아무 부품도 바닥에 닿지 않았으면 false.",
                ("아무것도 안 닿음 → false", () => { using (var f = new Fix()) return (false, f.body.HasFallen); }));
            Add("1-2", "발은 닿아도 괜찮다", "isFoot 인 부품은 건너뛴다 (!p.isFoot).",
                ("양발이 닿음 (경계) → false", () => { using (var f = new Fix()) { f.Part("FootL").touchingGround = f.Part("FootR").touchingGround = true; return (false, f.body.HasFallen); } }));
            Add("1-2", "발 아닌 부품이 닿으면 넘어짐", "_parts 를 모두 돌며 발이 아닌데 touchingGround 인 부품이 하나라도 있으면 true.",
                ("정강이가 닿음 → true", () => { using (var f = new Fix()) { f.Part("ShinL").touchingGround = true; return (true, f.body.HasFallen); } }),
                ("몸통이 닿음 → true", () => { using (var f = new Fix()) { f.Part("Torso").touchingGround = true; return (true, f.body.HasFallen); } }),
                ("오른 아래팔만 닿음 (경계: 맨 마지막 쪽 부품) → true", () => { using (var f = new Fix()) { f.Part("ForearmR").touchingGround = true; return (true, f.body.HasFallen); } }));

            // ── 2-1 SetTarget ──
            Add("2-1", "앞뒤(X) 각도", "Quaternion.Euler(x, 0f, z) 의 첫 번째 칸이 x.",
                ("SetTarget(30, 0)", () => { using (var f = new Fix()) { f.Part("ThighL").SetTarget(30f, 0f); return (E(30, 0), f.TargetOf("ThighL")); } }));
            Add("2-1", "좌우(Z) 각도", "세 번째 칸이 z. 가운데(Y 비틀기)는 0.",
                ("SetTarget(0, 15)", () => { using (var f = new Fix()) { f.Part("ThighL").SetTarget(0f, 15f); return (E(0, 15), f.TargetOf("ThighL")); } }),
                ("SetTarget(30, 15)", () => { using (var f = new Fix()) { f.Part("ThighL").SetTarget(30f, 15f); return (E(30, 15), f.TargetOf("ThighL")); } }));
            Add("2-1", "부호를 바꾸지 않는다", "가동 범위도 같은 관절 공간이라 값을 그대로 넣는다 (−x 로 뒤집지 않는다).",
                ("SetTarget(-40, -10) (경계)", () => { using (var f = new Fix()) { f.Part("ThighL").SetTarget(-40f, -10f); return (E(-40, -10), f.TargetOf("ThighL")); } }));

            // ── 2-2 SetActions (행동 8·9 = 왼 엉덩이 X·Z, 10 = 왼 무릎) ──
            Add("2-2", "행동 0 = 차렷", "ActionToAngle 은 행동 0 을 0도로 바꾼다. 모든 관절에 SetTarget 해야 한다.",
                ("행동 전부 0 → 모든 관절 목표 0도", () => { using (var f = new Fix()) { f.Part("ThighL").SetTarget(50, 10); f.body.SetActions(new float[18]); float worst = f.body.GetComponentsInChildren<BodyPart>().Where(p => p.joint != null).Max(p => Quaternion.Angle(p.joint.targetRotation, Quaternion.identity)); return (0f, worst); } }));
            Add("2-2", "행동 → 각도 (가동 범위 안에서)", "Range(i) 로 범위를 얻고 HumanoidMath.ActionToAngle(actions[i], r.x, r.y). +1 → high.",
                ("왼 엉덩이 앞뒤 +1 → 90도", () => { using (var f = new Fix()) { var a = new float[18]; a[8] = 1f; f.body.SetActions(a); return (E(90, 0), f.TargetOf("ThighL")); } }),
                ("왼 무릎 -1 → -120도", () => { using (var f = new Fix()) { var a = new float[18]; a[10] = -1f; f.body.SetActions(a); return (E(-120, 0), f.TargetOf("ShinL")); } }));
            Add("2-2", "한 관절의 X · Z 를 함께", "X 는 x, Z 는 z 에 모았다가 관절이 끝나는 곳에서 SetTarget(x, z) 한 번. 축마다 따로 넣으면 앞 축 값이 지워진다.",
                ("엉덩이 앞뒤 0.5 · 좌우 -1 → (45, -25)", () => { using (var f = new Fix()) { var a = new float[18]; a[8] = 0.5f; a[9] = -1f; f.body.SetActions(a); return (E(45, -25), f.TargetOf("ThighL")); } }));
            Add("2-2", "다음 관절에 값이 남지 않는다", "SetTarget 한 뒤 x = z = 0f. 안 지우면 축이 하나뿐인 무릎에 엉덩이의 좌우 값이 따라간다.",
                ("엉덩이 좌우 +1 만 → 무릎은 0도 (경계)", () => { using (var f = new Fix()) { var a = new float[18]; a[9] = 1f; f.body.SetActions(a); return (Quaternion.identity, f.TargetOf("ShinL")); } }));

            // ── 3-1 OnEpisodeBegin ──
            Add("3-1", "몸을 처음 자세로", "body.ResetBody() 를 부른다 (안에서 부품마다 ResetPose — 실습 1-1 이 맞아야 한다).",
                ("흐트러진 골반 → 처음 자리", () => { using (var f = new Fix(1)) { var p = f.Part("Pelvis"); var start = p.transform.localPosition; Disturb(f); f.agent.OnEpisodeBegin(); return (start, p.transform.localPosition); } }));
            Add("3-1", "관절 목표도 차렷으로", "ResetBody() 가 목표 각도도 0 으로 되돌린다 (실습 2-2 가 맞아야 한다).",
                ("굽혀 둔 엉덩이 목표 → 0도", () => { using (var f = new Fix(1)) { f.Part("ThighL").joint.targetRotation = E(50, 0); f.agent.OnEpisodeBegin(); return (Quaternion.identity, f.TargetOf("ThighL")); } }));
            Add("3-1", "키보드 행동도 차렷으로", "_keyboard 가 있으면 _keyboard.ResetActions(). 안 하면 Heuristic 일 때 이전 자세로 다시 당겨진다.",
                ("키보드에 남은 행동 → 0", () => { using (var f = new Fix(1, withKeyboard: true)) { f.keyboard.actions[8] = 0.8f; f.agent.OnEpisodeBegin(); return (0f, f.keyboard.actions.Max(Mathf.Abs)); } }));
            Add("3-1", "키보드가 없어도 된다", "if (_keyboard != null) 로 감싼다. 학습할 때는 키보드가 없을 수 있다.",
                ("키보드 없음 (경계) → 오류 없이 끝남", () => { using (var f = new Fix(1)) { f.agent.OnEpisodeBegin(); return ("오류 없음", "오류 없음"); } }));

            // ── 3-2 CollectObservations ──
            Add("3-2", "관측 48개", "1 + 3 + 3 + 3 + (각도 · 속도) × 18 + 2 = 48. Vector3 는 한 번에 3개다.",
                ("넣은 개수", () => { using (var f = new Fix(1)) return (48f, (float)Observe(f.agent, 48).Count); }));
            Add("3-2", "골반 높이가 첫 번째", "첫 칸 = body.pelvis.transform.position.y − transform.position.y.",
                ("서 있을 때 첫 값 → 0.96", () => { using (var f = new Fix(1)) { var o = Observe(f.agent, 48); return (0.96f, o.Count > 0 ? o[0] : float.NaN); } }));
            Add("3-2", "몸이 느끼는 아래쪽 (기울기)", "2~4번째 = HumanoidMath.DownInBody(pelvis.rotation). 똑바로 서면 (0, -1, 0).",
                ("서 있을 때 2~4번째 → (0,-1,0)", () => { using (var f = new Fix(1)) { var o = Observe(f.agent, 48); return (Vector3.down, o.Count >= 4 ? new Vector3(o[1], o[2], o[3]) : Vector3.zero); } }));
            Add("3-2", "관절마다 각도 · 속도 순서", "11번째부터 축마다 [NormalizedAngle(i), Rate(i) / 10] 을 번갈아 넣는다.",
                ("왼 엉덩이를 굽힘 → 27번째 = 그 각도", () => { using (var f = new Fix(1)) { var t = f.Part("ThighL").transform; t.RotateAround(t.position + Vector3.up * 0.21f, Vector3.right, -45f); Physics.SyncTransforms(); var o = Observe(f.agent, 48); return (f.body.NormalizedAngle(8), o.Count > 26 ? o[26] : float.NaN); } }));
            Add("3-2", "발 접촉이 마지막 두 칸", "맨 끝에 footL · footR 의 touchingGround (bool 은 1 / 0 으로 들어간다).",
                ("왼발만 닿음 (경계) → [1, 0]", () => { using (var f = new Fix(1)) { f.body.footL.touchingGround = true; var o = Observe(f.agent, 48); return (new Vector2(1, 0), o.Count >= 48 ? new Vector2(o[46], o[47]) : Vector2.one * -1); } }));

            // ── 3-3 OnActionReceived ──
            Add("3-3", "행동을 몸에 보낸다", "actions.ContinuousActions 를 _actions 에 옮겨 body.SetActions(_actions).",
                ("왼 무릎 행동 -1 → 무릎 목표 -120도", () => { using (var f = new Fix(1, academy: true)) { f.agent.OnActionReceived(Act((10, -1f))); return (E(-120, 0), f.TargetOf("ShinL")); } }));
            Add("3-3", "서 있으면 보상", "AddReward(StepReward()). 똑바로 제 높이면 약 1점.",
                ("서 있는 한 스텝 → 보상 1", () => { using (var f = new Fix(1, academy: true)) { f.agent.OnActionReceived(Act()); return (1f, f.agent.GetCumulativeReward()); } }));
            Add("3-3", "넘어지면 판을 끝낸다", "if (body.HasFallen) EndEpisode(); — 끝난 판 수(CompletedEpisodes)가 1 늘어야 한다.",
                ("몸통이 바닥에 닿음 → 판 끝", () => { using (var f = new Fix(1, academy: true)) { f.Part("Torso").touchingGround = true; f.agent.OnActionReceived(Act()); return (1f, (float)f.agent.CompletedEpisodes); } }));
            Add("3-3", "서 있으면 판이 계속된다", "넘어지지 않았는데 EndEpisode 를 부르지 않는다.",
                ("넘어지지 않음 (경계) → 판 계속", () => { using (var f = new Fix(1, academy: true)) { f.agent.OnActionReceived(Act()); return (0f, (float)f.agent.CompletedEpisodes); } }));

            // ── 4-1 CollectObservations (걷기) ──
            Add("4-1", "관측 51개 (서기 48 + 3)", "base.CollectObservations(sensor) 다음에 3개를 더 넣는다.",
                ("넣은 개수", () => { using (var f = new Fix(2)) return (51f, (float)Observe(f.agent, 51).Count); }));
            Add("4-1", "서기 관측이 먼저", "base 를 맨 앞에서 부른다 — 첫 값은 서기와 같은 골반 높이.",
                ("첫 값 → 0.96", () => { using (var f = new Fix(2)) { var o = Observe(f.agent, 51); return (0.96f, o.Count > 0 ? o[0] : float.NaN); } }));
            Add("4-1", "목표 방향을 내가 보는 방향 기준으로", "Quaternion.Inverse(HumanoidMath.HeadingRotation(골반 forward)) * DirectionToTarget() 의 x · z.",
                ("목표가 오른쪽 → (1, 0)", () => { using (var f = new Fix(2)) { f.target.position = f.body.pelvis.transform.position + new Vector3(3, 0, 0); var o = Observe(f.agent, 51); return (new Vector2(1, 0), o.Count >= 50 ? new Vector2(o[48], o[49]) : Vector2.zero); } }),
                ("몸을 오른쪽으로 돌림, 목표도 오른쪽 (경계) → 앞 (0, 1)", () => { using (var f = new Fix(2)) { f.body.pelvis.transform.rotation = Quaternion.Euler(0, 90, 0); Physics.SyncTransforms(); f.target.position = f.body.pelvis.transform.position + new Vector3(3, 0, 0); var o = Observe(f.agent, 51); return (new Vector2(0, 1), o.Count >= 50 ? new Vector2(o[48], o[49]) : Vector2.zero); } }));
            Add("4-1", "목표 속도", "마지막에 targetSpeed.",
                ("targetSpeed 1.3 → 마지막 값 1.3", () => { using (var f = new Fix(2)) { f.walk.targetSpeed = 1.3f; var o = Observe(f.agent, 51); return (1.3f, o.Count >= 51 ? o[50] : float.NaN); } }));

            // ── 4-2 StepReward (걷기) ──
            Add("4-2", "목표에 닿으면 +1", "목표까지 수평 거리가 0.6 보다 작으면 bonus = 1.",
                ("목표가 0.3m 앞 → 보상 1 이상", () => { using (var f = new Fix(2)) { f.target.position = f.body.pelvis.transform.position + new Vector3(0.3f, 0, 0); return (true, (float)Call(f.walk, "StepReward") >= 0.99f); } }));
            Add("4-2", "닿으면 목표를 새 자리로", "닿았을 때 MoveTarget() 을 부른다 (4~8m 떨어진 새 자리). _reached 도 1 늘린다.",
                ("닿은 뒤 목표까지 거리 → 4m 이상", () => { using (var f = new Fix(2)) { f.target.position = f.body.pelvis.transform.position + new Vector3(0.3f, 0, 0); Call(f.walk, "StepReward"); var d = f.target.position - f.body.pelvis.transform.position; d.y = 0; return (true, d.magnitude >= 3.9f); } }),
                ("닿은 수 → 1", () => { using (var f = new Fix(2)) { f.target.position = f.body.pelvis.transform.position + new Vector3(0.3f, 0, 0); Call(f.walk, "StepReward"); return (1f, (float)(int)Get(f.walk, "_reached")); } }));
            Add("4-2", "걷기 점수 = 속도 × 방향", "return bonus + HumanoidMath.SpeedReward(velocity, dir, targetSpeed) * HumanoidMath.FacingReward(body.torso.transform.forward, dir).",
                ("목표 쪽을 보며 1 m/s → 1", () => { using (var f = new Fix(2)) { foreach (var p in f.body.GetComponentsInChildren<BodyPart>()) p.rb.linearVelocity = Vector3.forward; return (1f, (float)Call(f.walk, "StepReward")); } }),
                ("서 있기만 함 (경계) → 0", () => { using (var f = new Fix(2)) return (0f, (float)Call(f.walk, "StepReward")); }));
            Add("4-2", "더하지 않고 곱한다", "속도 점수와 방향 점수를 곱한다. 더하면 목표를 등지고 걸어도 점수가 난다.",
                ("목표를 등지고 1 m/s 로 목표 쪽 이동 (경계) → 0", () => { using (var f = new Fix(2)) { f.body.torso.transform.rotation = Quaternion.Euler(0, 180, 0); Physics.SyncTransforms(); foreach (var p in f.body.GetComponentsInChildren<BodyPart>()) p.rb.linearVelocity = Vector3.forward; return (0f, (float)Call(f.walk, "StepReward")); } }));

            // ── 4-3 OnActionReceived (걷기) ──
            Add("4-3", "제한시간이 지나면 판 끝", "if (StepCount > _deadline) EndEpisode();",
                ("마감 지남 → 판 끝", () => { using (var f = new Fix(2, academy: true)) { Set(f.walk, "_deadline", -1); f.agent.OnActionReceived(Act()); return (1f, (float)f.agent.CompletedEpisodes); } }));
            Add("4-3", "제한시간 안에는 계속", "마감 전에는 EndEpisode 를 부르지 않는다.",
                ("마감 전 (경계) → 판 계속", () => { using (var f = new Fix(2, academy: true)) { Set(f.walk, "_deadline", 100000); f.agent.OnActionReceived(Act()); return (0f, (float)f.agent.CompletedEpisodes); } }));
            Add("4-3", "서기 동작도 그대로", "base.OnActionReceived(actions) 를 지우지 않는다 — 행동 · 보상 · 넘어지면 끝내기가 거기 있다.",
                ("왼 무릎 행동 -1 → 무릎 목표 -120도", () => { using (var f = new Fix(2, academy: true)) { Set(f.walk, "_deadline", 100000); f.agent.OnActionReceived(Act((10, -1f))); return (E(-120, 0), f.TargetOf("ShinL")); } }));

            // ── 5-1 RecordStats ──
            Add("5-1", "성적 네 가지를 남긴다", "Academy.Instance.StatsRecorder.Add(\"Walk/…\", 값) 를 네 번. 이름 철자가 다르면 TensorBoard 에서 못 찾는다.",
                ("Walk/Speed = 평균 속도", () => Stat("Walk/Speed", 0.8f)),
                ("Walk/SpeedError = 속도 오차", () => Stat("Walk/SpeedError", 0.2f)),
                ("Walk/FallRate = 넘어졌으면 1", () => Stat("Walk/FallRate", 1f)),
                ("Walk/Targets = 닿은 수", () => Stat("Walk/Targets", 3f)));
            Add("5-1", "기록이 없으면 남기지 않는다", "_speed.Count == 0 이면 return. 첫 판에 0 이 섞여 평균이 틀어지지 않게.",
                ("첫 판 (경계) → 아무것도 안 남김", () => { using (var f = new Fix(2, academy: true)) { ClearStats(); Call(f.walk, "RecordStats"); return (0f, (float)SentStats().Count); } }));

            // ── 5-2 ReadCurriculum ──
            Add("5-2", "커리큘럼 값을 읽는다", "Academy.Instance.EnvironmentParameters.GetWithDefault(\"target_speed\", targetSpeed) 를 targetSpeed 에 넣는다. 이름은 yaml 과 같게.",
                ("커리큘럼 target_speed 1.5 → targetSpeed 1.5", () => { using (var f = new Fix(2, academy: true)) { Curriculum()["target_speed"] = () => 1.5f; Call(f.walk, "ReadCurriculum"); return (1.5f, f.walk.targetSpeed); } }),
                ("단계가 0.5 → 1.0 으로 오름 → 다음 판은 1.0", () => { using (var f = new Fix(2, academy: true)) { Curriculum()["target_speed"] = () => 0.5f; Call(f.walk, "ReadCurriculum"); Curriculum()["target_speed"] = () => 1.0f; Call(f.walk, "ReadCurriculum"); return (1.0f, f.walk.targetSpeed); } }));
            Add("5-2", "없으면 지금 값 그대로", "기본값으로 지금 targetSpeed 를 준다. 0 같은 고정값을 주면 커리큘럼 없이 Play 할 때 멈춰 선다.",
                ("커리큘럼 없음 (경계) → 1.2 그대로", () => { using (var f = new Fix(2, academy: true)) { f.walk.targetSpeed = 1.2f; Call(f.walk, "ReadCurriculum"); return (1.2f, f.walk.targetSpeed); } }));
        }

        // 5-1: 성적을 만들어 두고 RecordStats 를 불러, 그 이름으로 보낸 값을 본다
        static (object, object) Stat(string key, float expected)
        {
            using (var f = new Fix(2, academy: true))
            {
                var speed = (SpeedStats)Get(f.walk, "_speed");
                speed.Add(0.8f);
                speed.Add(0.8f);
                Set(f.walk, "_reached", 3);
                Set(f.walk, "_fell", true);
                f.walk.targetSpeed = 1f;
                ClearStats();
                Call(f.walk, "RecordStats");
                var sent = SentStats();
                return (expected, sent.TryGetValue(key, out float v) ? (object)v : "안 남김");
            }
        }

        // 이 실습의 요구사항 이름들 (창에 미리 보여 준다)
        public static List<string> Requirements(string id) => Cases.Where(c => c.id == id).Select(c => c.req).Distinct().ToList();
        public static int CaseCount(string id) => Cases.Count(c => c.id == id);

        // id가 prefix로 시작하는 케이스를 실행한다. ("2-1" = 실습 하나, "" = 전부)
        public static List<Result> Grade(string prefix)
        {
            var results = new List<Result>();
            var sim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;      // 테스트하는 동안 물리가 저절로 돌지 않게
            try
            {
                foreach (var c in Cases)
                {
                    if (!c.id.StartsWith(prefix)) continue;
                    object expected = null, value;
                    try { (expected, value) = c.run(); }
                    catch (TargetInvocationException e) { value = e.InnerException; }
                    catch (Exception e) { value = e; }                    // 학습자 코드가 터져도 테스트는 계속한다

                    bool pass = value is float f ? expected is float g && Mathf.Abs(f - g) < 2e-2f
                              : value is Vector3 v ? expected is Vector3 w && (v - w).magnitude < 2e-2f
                              : value is Vector2 v2 ? expected is Vector2 w2 && (v2 - w2).magnitude < 2e-2f
                              : value is Quaternion q ? expected is Quaternion r && Quaternion.Angle(q, r) < 0.5f
                              : Equals(value, expected);
                    results.Add(new Result { req = c.req, label = c.label, expected = Show(expected), actual = Show(value), fix = c.fix, why = Why(value), pass = pass });
                }
            }
            finally { Physics.simulationMode = sim; }
            return results;
        }

        // 값만 보고 알 수 있는 흔한 원인
        static string Why(object value)
        {
            bool bad(float x) => float.IsNaN(x) || float.IsInfinity(x);
            if (value is float f && bad(f) || value is Vector3 v && (bad(v.x) || bad(v.y) || bad(v.z)))
                return "NaN · 무한대가 나왔다 — 0으로 나눴거나, 관측이 모자라 그 칸이 없다.";
            if (value is NullReferenceException) return "null 을 썼다 — 없을 수 있는 것(_keyboard 등)은 != null 로 먼저 확인한다.";
            if (value is IndexOutOfRangeException || value is ArgumentOutOfRangeException) return "배열 범위를 벗어났다 — 반복 조건(i < 길이)을 본다.";
            if (value is Exception e) return "예외 " + e.GetType().Name + " — " + e.Message;
            return null;
        }

        static string Show(object o) =>
            o is float f ? (float.IsNaN(f) ? "없음(NaN)" : float.IsInfinity(f) ? "무한대" : f.ToString("0.###"))
          : o is Quaternion q ? ShowRotation(q)
          : o is Vector3 v ? v.ToString("0.##")
          : o is Vector2 v2 ? v2.ToString("0.##")
          : o is bool b ? (b ? "예" : "아니오")
          : o is Exception e ? "예외 " + e.GetType().Name
          : o?.ToString() ?? "null";

        // 관절 목표 회전을 "앞뒤 x° 좌우 z°" 로. 오일러각은 90°를 넘으면 뒤집혀(-120 → 앞뒤 -60 좌우 180) 위쪽 방향으로 읽는다.
        static string ShowRotation(Quaternion q)
        {
            Vector3 up = q * Vector3.up;
            return "앞뒤 " + (Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg).ToString("0") + "° 좌우 " + (-Mathf.Asin(Mathf.Clamp(up.x, -1f, 1f)) * Mathf.Rad2Deg).ToString("0") + "°";
        }

        // 개발자용: 정답 상태에서 전부 통과하는지
        [MenuItem("Humanoid/개발자/전체 채점", false, 302)]
        static void RunAll()
        {
            var results = Grade("");
            var failed = results.FindAll(r => !r.pass);
            if (failed.Count == 0) Debug.Log("[Humanoid] 전체 채점 — " + results.Count + "개 케이스 전부 통과");
            else Debug.LogWarning("[Humanoid] 전체 채점 — 실패 " + failed.Count + " / " + results.Count + "개:\n  " + string.Join("\n  ", failed.ConvertAll(r => r.label + " (기대 " + r.expected + " · 실제 " + r.actual + (r.why != null ? " · " + r.why : "") + ")")));
        }
    }
}
