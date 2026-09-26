using RobotArms;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 5장 — 자석을 떼고 집게를 단다.
    ///
    /// <b>팔은 건드리지 않는다.</b> 1~2장에서 만든 그대로 Transform으로 돈다.
    /// 바뀌는 것은 집는 방식 하나뿐이다.
    ///
    ///   4장: 가까이서 버튼 → 물체가 팔의 자식이 된다 (프로그램이 붙인다)
    ///   5장: 손가락이 눌러서 마찰로 버틴다          (붙이는 코드가 없다)
    ///
    /// 그래서 물리 부품이 필요한 곳은 딱 세 개다.
    ///   Hand     — 팔 끝을 따라다니는 kinematic Rigidbody (손목)
    ///   FingerL/R — 경첩 + 모터로 여닫히는 손가락
    ///   Object   — 원래부터 Rigidbody였다. 마찰 재질만 붙여준다.
    /// </summary>
    internal static class Ch5_Gripper
    {
        static readonly Vector2 PlaceSpot = new Vector2(1.5f, -45f);

        // ── 집게 치수 (손목 기준) ───────────────────────────────────
        //
        // 손가락 하나는 <b>마디 세 개</b>다. 집게발 모양을 만드는 최소 개수다.
        // 집게는 손목에서 아래로 뻗는다. 위에서 내려 집기 때문이다.
        //
        //          ● ●          경첩 — 손목 가까이, 안쪽에 있다
        //         ╱   ╲         위마디 — 일단 바깥으로 벌어지며 내려간다
        //        ▌     ▐        팔꿈치 — 가장 넓은 자리
        //         ╲   ╱         아래마디 — 다시 안쪽으로 모인다
        //          ▓ ▓          패드 — 끝에서 물체를 문다
        //
        // 왜 세 개인가:
        //   바깥으로 부푼 모양을 내려면 경첩보다 바깥으로 나가는 마디가 있어야 하고,
        //   물체를 제대로 물려면 마지막이 <b>나란한 평면</b>이어야 한다.
        //   둘 중 하나라도 빼면 집게발이 아니거나, 물체가 밀려 나간다.
        const float HingeHalf = 0.17f;      // 경첩의 좌우 위치
        const float ElbowHalf = 0.27f;      // 팔꿈치 — 여기가 가장 바깥
        const float ElbowDepth = 0.10f;     // 팔꿈치까지의 깊이

        const float PadHalf = 0.115f;       // 패드 중심 (안쪽 면 0.095 — 물체 반폭 0.1을 문다)
        const float PadTop = 0.18f;
        const float PadBottom = 0.28f;
        const float PadThickness = 0.04f;
        const float PadDepth = 0.26f;       // 패드 앞뒤 폭 — 이 값이 앞뒤 허용 오차를 정한다

        const float ArmThickness = 0.05f;   // 마디 두께
        const float UpperDepth = 0.12f;     // 마디 앞뒤 폭. 패드보다 좁아야 집게처럼 보인다
        const float LowerDepth = 0.14f;

        // 손목에서 손가락 사이 한가운데까지.
        //
        // 이 값이 곧 <b>집을 때 팔 끝의 높이</b>를 정한다. (물체 중심 0.1 + 이 값)
        // 너무 작게 잡으면 팔 끝이 낮아지고, 벌린 손가락 끝이 테이블을 파고들어
        // 아예 오므라들지 않는다. 실측:
        //   0.19 -> 팔 끝 0.29, 손가락 끝 -0.013  오므리다 걸림 (각도 2도)
        //   0.22 -> 팔 끝 0.32, 손가락 끝  0.017  정상 (각도 37도)
        const float GripDepth = 0.22f;

        // ── 손가락 모양의 핵심 ──────────────────────────────────────
        //
        // 회전하는 판은 그냥 달면 <b>물체와 비스듬히</b> 만난다.
        // 그러면 모서리로만 닿고, 누르는 힘이 물체를 옆으로 밀어내서
        // 수박씨처럼 튀어나간다. (실제로 그랬다)
        //
        // 그래서 <b>다 오므렸을 때 면이 물체와 나란해지도록</b> 거꾸로 설계한다.
        //   ① 먼저 "나란한 자세"에 판을 놓고
        //   ② 경첩을 판보다 바깥에 두고
        //   ③ 거기서 바깥으로 OpenAngle 만큼 젖힌 것을 <b>쉬는 자세</b>로 삼는다
        //
        // 결과: 벌리면 아래가 넓은 <b>깔때기</b> 모양이 되어 물체가 알아서 가운데로
        //       모이고, 오므리면 <b>평행한 두 면</b>이 물체를 납작하게 문다.
        //
        // 집게발 모양에는 <b>팔꿈치와 패드 사이에 가장 좁은 "목"</b>이 생긴다.
        // 물체는 이 목을 지나야 들어오므로, 넉넉히 젖혀 두어야 한다.
        // (38도로 두었더니 목이 0.487이라 허용 오차가 반 토막 났다 → 50도)
        const float OpenAngle = 50f;        // 쉬는 자세 = 이만큼 바깥으로 젖혀진 상태
        const float SqueezeAngle = 15f;     // 나란해진 뒤에도 더 밀 수 있는 여유
        const float GripRange = OpenAngle + SqueezeAngle;

        // 팔 끝이 내려갈 수 있는 한계 높이.
        //
        // ★ 이 값을 잘못 잡아서 학습이 통째로 실패한 적이 있다.
        //
        //   처음에는 "손가락이 테이블을 파고들지 않게" 0.29로 올렸다.
        //   그런데 어깨(J0·J1)가 높이 0.30에 있다. 한계선을 0.29로 두면
        //   팔 전체가 0.29~0.30 사이의 <b>아주 얇은 층</b>만 지나다닐 수 있고,
        //   통과 방지는 관절을 <b>하나씩</b> 되돌리므로 그 좁은 통로를 못 빠져나간다.
        //
        //   실측: 통과 방지 ON일 때 스폰 25곳 중 도달 가능한 곳이
        //         0.29 -> 10곳,  0.22 이하 -> 25곳.
        //
        //   즉 60%의 물체에는 접근 자체가 불가능했다. 800만 스텝을 돌려도
        //   보상이 -1에서 꿈쩍하지 않은 진짜 이유다.
        //
        // 손가락이 테이블에 닿는 것은 물리가 알아서 처리한다(경첩이 꺾인다).
        // 애초에 에이전트가 거기까지 내려갈 이유도 없다. 목표는 물체 위다.
        const float GripperClearance = 0.14f;

        [MenuItem("RobotArms/5. 집게로 교체 (마찰로 잡기)", false, 5)]
        static void Setup()
        {
            if (!RequireParts("Base", "Tip", "Object", "PlaceTarget")) return;

            var baseTf = Find("Base");
            var tip = Find("Tip");
            var obj = Find("Object");
            var area = GameObject.Find(AreaName).transform;

            var arm = baseTf.GetComponent<RobotArmController>();
            if (arm == null || arm.JointCount < 3)
            {
                EditorUtility.DisplayDialog("순서가 다릅니다",
                    "먼저 RobotArms > 2. 관절 제어 붙이기 를 실행하세요.", "확인");
                return;
            }

            var friction = MakeFrictionMaterial();

            // ── 1. 집게를 팔 바깥에 만든다 ──────────────────────────
            //
            // 왜 Tip의 자식으로 매달지 않는가:
            //   팔은 Transform을 직접 대입해서 움직인다. 물리 엔진은 그 사실을 모른다.
            //   그 밑에 Rigidbody를 매달면, 팔이 돌 때마다 손가락이 물리 엔진 몰래
            //   순간이동하게 된다. 순간이동하는 손가락은 물체를 "누르지" 못하고,
            //   누르지 못하면 마찰도 생기지 않는다. 그러면 영영 잡히지 않는다.
            //
            //   그래서 집게는 팔 바깥에 두고, 매 물리 스텝마다 MovePosition으로
            //   팔 끝을 따라가게 한다. (GripperHand.cs)
            var oldGripper = Find("Gripper");
            if (oldGripper != null) Undo.DestroyObjectImmediate(oldGripper.gameObject);

            var root = new GameObject("Gripper");
            Undo.RegisterCreatedObjectUndo(root, "집게 만들기");
            root.transform.SetParent(area, false);

            // Hand — 손목. 보이지 않는다. 팔 끝 자리를 따라다니는 역할만 한다.
            //
            // 방향은 팔 끝을 따라가지 않고 <b>항상 아래</b>를 본다.
            // 이 팔에는 손목 관절이 없어서, 팔뚝이 기울면 집게도 같이 기울어
            // 손가락 끝이 테이블을 파고들기 때문이다. (GripperHand.AimRotation 참고)
            var handGo = new GameObject("Hand");
            handGo.transform.SetParent(root.transform, false);
            handGo.transform.SetPositionAndRotation(
                tip.position, GripperHand.AimRotation(tip.position, baseTf.position));

            var handRb = handGo.AddComponent<Rigidbody>();
            handRb.isKinematic = true;      // 물리에 떠밀리지 않는다. 팔이 시키는 대로만.
            handRb.useGravity = false;
            handRb.mass = 1f;

            // ── 2. 손가락 두 개 ─────────────────────────────────────
            var fingerL = MakeFinger("FingerL", root.transform, handGo.transform, handRb,
                                     obj, friction, left: true);
            var fingerR = MakeFinger("FingerR", root.transform, handGo.transform, handRb,
                                     obj, friction, left: false);

            var hand = handGo.AddComponent<GripperHand>();
            hand.follow = tip;
            hand.yawReference = baseTf;
            hand.fingers = new[]
            {
                fingerL.GetComponent<Rigidbody>(),
                fingerR.GetComponent<Rigidbody>(),
            };

            var gripper = handGo.AddComponent<PincerGripper>();
            gripper.fingerL = fingerL.GetComponent<HingeJoint>();
            gripper.fingerR = fingerR.GetComponent<HingeJoint>();
            gripper.contactL = fingerL.GetComponent<FingerContact>();
            gripper.contactR = fingerR.GetComponent<FingerContact>();
            gripper.gripSpeed = 90f;
            gripper.gripForce = 4f;         // 너무 세면 물체가 튀어나간다
            gripper.closeSignL = 1f;        // 왼쪽 손가락은 + 방향으로 오므라든다
            gripper.closeSignR = -1f;       // 오른쪽은 - 방향
            gripper.gripRange = GripRange;
            gripper.gripDepth = GripDepth;

            // ── 관절 속도를 낮춘다 ──────────────────────────────────
            //
            // 2장 값(어깨 120 · 팔꿈치 180 · 좌우 120 도/초)은 손으로 조작할 때
            // 시원해서 좋았지만, 물리 집게를 단 뒤로는 너무 빠르다.
            //   - 빠르게 다가가면 집게가 물체를 <b>쳐서 날려버린다</b>
            //   - 급정거할 때 물고 있던 물체가 관성으로 빠진다
            //
            // 실제 로봇 팔의 모터처럼 <b>절반 속도</b>로 낮춘다.
            // 과제 전체에 10초면 충분하고 한 에피소드는 60초(3000스텝)라 여유가 많다.
            //
            // (곱셈이 아니라 절대값으로 적는다. 메뉴를 두 번 눌러도 같은 값이어야 한다)
            Undo.RecordObject(arm, "관절 속도 조정");
            float[] motorSpeeds = { 60f, 90f, 60f };    // 어깨 · 팔꿈치 · 좌우
            for (int i = 0; i < arm.joints.Length && i < motorSpeeds.Length; i++)
                arm.joints[i].maxSpeed = motorSpeeds[i];

            // ── 팔꿈치를 한 방향으로만 꺾이게 한다 ──────────────────
            //
            // 2장 값은 ±130도라서 팔꿈치가 <b>양쪽으로</b> 꺾였다.
            // 사람 팔에는 없는 움직임이고, 보기에도 부자연스럽다.
            // 게다가 같은 지점에 가는 자세가 둘씩 생겨 탐색 공간만 넓어진다.
            //
            // 위쪽(+)을 막아 실제 로봇 팔처럼 아래로만 접히게 한다.
            Undo.RecordObject(arm, "팔꿈치 가동 범위 조정");
            arm.joints[1].maxAngle = 0f;

            // 팔 끝의 바닥 한계선을 집게 길이만큼 올린다.
            // 2장 값(0.07) 그대로 두면 손가락이 테이블을 파고든다.
            Undo.RecordObject(arm, "집게에 맞춰 바닥 한계 조정");
            arm.clearance = GripperClearance;
            EditorUtility.SetDirty(arm);

            // ── 3. 물체는 그대로 둔다 ───────────────────────────────
            //
            // 마찰 재질은 <b>손가락에만</b> 붙인다. 물체에는 붙이지 않는다.
            //
            // 재질의 Combine을 Maximum으로 두었기 때문에, 손가락이 닿는 순간
            // 둘 중 큰 값(2.0)이 쓰인다. 물체에까지 붙일 필요가 없다.
            //
            // 오히려 붙이면 손해다. 물체가 <b>테이블에도</b> 2.0으로 들러붙어서,
            // 집게가 내려올 때 깔때기가 물체를 가운데로 밀어 넣지 못한다.
            // (실제로 그랬다 — 밀어준 거리가 0.008에 그쳤다)
            var objBody = obj.GetComponent<Rigidbody>();
            Undo.RecordObject(objBody, "물체 물리 설정");
            objBody.isKinematic = false;
            objBody.useGravity = true;

            // 잠든 Rigidbody는 손가락이 밀어도 안 움직이고 접촉도 늦게 잡힌다.
            // 학습 중에는 물체가 테이블 위에 가만히 있는 시간이 대부분이라 꼭 꺼둔다.
            objBody.sleepThreshold = 0f;

            // 물린 상태에서 조금씩 미끄러지는 것을 줄인다.
            objBody.solverIterations = 20;
            EditorUtility.SetDirty(objBody);

            // ── 4. 에이전트 교체 ────────────────────────────────────
            // 순서 주의: 새 Agent를 먼저 붙인 뒤에 옛 것을 지운다.
            // 먼저 지우면 DecisionRequester가 Agent가 필요하다며 삭제를 거부한다.
            var agent = GetOrAdd<RobotArmGripperAgent>(baseTf.gameObject);

            var pickPlace = baseTf.GetComponent<RobotArmPickPlaceAgent>();
            if (pickPlace != null) Undo.DestroyObjectImmediate(pickPlace);

            var reachAgent = baseTf.GetComponent<RobotArmAgent>();
            if (reachAgent != null) Undo.DestroyObjectImmediate(reachAgent);

            Undo.RecordObject(agent, "집게 에이전트 설정");
            agent.arm = arm;
            agent.gripper = gripper;
            agent.hand = hand;
            agent.targetObject = obj;
            agent.placeTarget = Find("PlaceTarget");
            agent.hud = baseTf.GetComponent<ArmControlHud>();
            agent.placeRadius = 0.25f;
            agent.restSpeed = 0.2f;
            agent.spawnDistanceRange = SpawnDistanceRange;
            agent.spawnYawRange = SpawnYawRange;
            agent.minSeparation = 0.6f;

            // 실측한 작업 범위. 집게를 물체 위로 보낼 수 있는 구역이다.
            //   거리 1.10~2.70 가능, 1.00 이하 불가 (좌우는 ±90도까지 확인)
            // 여기를 벗어난 물체는 포기하고 판을 새로 연다.
            agent.workRange = new Vector2(1.05f, 2.65f);
            agent.workYawLimit = 100f;
            agent.useDenseReward = true;
            agent.hoverHeight = 0.35f;
            agent.alignRadius = 0.30f;
            agent.liftHeight = 0.40f;
            agent.approachReward = 0.3f;
            agent.approachRadius = 0.15f;
            agent.grabReward = 0.5f;
            agent.liftReward = 0.5f;
            agent.placeReward = 1.0f;
            agent.dropPenalty = 0.3f;
            agent.lostPenalty = 0.5f;
            agent.maxStepReward = 0.05f;

            // 4장(2500)보다 넉넉하게 준다. 자석과 달리 손가락을 맞춰 넣는 시간이 든다.
            agent.MaxStep = 3000;

            var bp = GetOrAdd<BehaviorParameters>(baseTf.gameObject);
            Undo.RecordObject(bp, "두뇌 설정 갱신");
            bp.BehaviorName = "RobotArmGripper";

            // 관측: 4장의 16개 + 쥔 정도 1 + 양쪽 접촉 2 = 19
            bp.BrainParameters.VectorObservationSize = 19;
            bp.BrainParameters.NumStackedVectorObservations = 1;

            // 액션 구조는 4장과 같다. 뜻만 바뀐다.
            // (잡아라/놓아라 → 손가락 모터를 오므려라/펴라)
            bp.BrainParameters.ActionSpec = new ActionSpec(3, new[] { 2 });
            bp.BehaviorType = BehaviorType.Default;

            // 놓을 자리를 눈에 보이는 곳에 둔다.
            // <b>실행 중에는 매 판 랜덤으로 다시 뽑히므로</b> 여기 값은 편집 화면용이다.
            var place = Find("PlaceTarget");
            Undo.RecordObject(place, "놓을 자리 배치");
            Vector3 local = arm.SpawnPointLocal(PlaceSpot.x, PlaceSpot.y);
            local.y = 0.01f;
            place.position = arm.transform.TransformPoint(local);

            EditorUtility.SetDirty(agent);
            EditorUtility.SetDirty(bp);
            EditorUtility.SetDirty(place);
            Selection.activeGameObject = handGo;

            Debug.Log(
                "[RobotArms] 5장 완료 — 집게로 교체 (관측 19 / 연속 3 + 이산 1)\n" +
                "  팔은 4장 그대로입니다. 집는 방식만 물리로 바뀌었습니다.\n" +
                "  Space를 누르면 손가락이 오므라들고, 마찰로 물체를 물어 쥡니다.\n" +
                "  붙이는 코드가 없으므로 세게 휘두르면 빠집니다. 그게 물리입니다.\n" +
                "  mlagents-learn Assets/RobotArms/robotarm_config.yaml --run-id=ch5_gripper");
        }

        // ── 도우미 ───────────────────────────────────────────────────

        /// <summary>
        /// 두 점을 잇는 막대 하나를 만든다.
        /// 좌표는 (좌우, 아래로)이고 손가락 뿌리가 원점이다.
        /// 마디마다 각도를 손으로 계산하지 않아도 되게 한 겹 감쌌다.
        /// </summary>
        static GameObject Segment(string name, Transform parent, Vector2 from, Vector2 to,
            float thickness, float depth, Material material)
        {
            Vector2 delta = to - from;
            Vector2 mid = (from + to) * 0.5f;

            var go = CreateMesh(PrimitiveType.Cube, name, parent,
                new Vector3(mid.x, 0f, mid.y),
                new Vector3(thickness, depth, delta.magnitude),
                material, keepCollider: true);

            // 막대의 길이 방향(로컬 +Z)이 delta를 향하게 돌린다.
            go.transform.localRotation =
                Quaternion.AngleAxis(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, Vector3.up);

            return go;
        }

        /// <summary>
        /// 손가락 하나를 만든다. <b>집게발</b> 모양이다.
        ///
        /// 경첩에서 일단 바깥으로 벌어져 내려가다(위마디), 팔꿈치에서 꺾여
        /// 다시 안쪽으로 모이고(아래마디), 끝에 무는 면(패드)이 붙는다.
        ///
        /// 치수는 전부 <b>"다 오므려 면이 나란해진 자세"</b>를 기준으로 잡고,
        /// 마지막에 통째로 바깥으로 젖혀 쉬는 자세로 만든다. 위 상수 주석 참고.
        /// </summary>
        static GameObject MakeFinger(string name, Transform parent, Transform hand,
            Rigidbody handBody, Transform target, PhysicsMaterial friction, bool left)
        {
            float side = left ? -1f : 1f;

            // 손가락 뿌리는 <b>스케일 1인 빈 오브젝트</b>다.
            // 스케일이 있는 오브젝트에 자식을 붙이면 전부 찌그러진다.
            // (1장에서 Base·Tip을 빈 오브젝트로 둔 것과 같은 이유)
            //
            // 덤으로 뿌리의 원점을 경첩 자리에 둘 수 있어서,
            // 아래에서 anchor를 그냥 0으로 두면 된다.
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "손가락 생성");
            go.transform.SetParent(parent, false);

            Vector3 anchorLocal = new Vector3(side * HingeHalf, 0f, 0f);
            Quaternion openRot = Quaternion.AngleAxis(side * OpenAngle, Vector3.up);
            go.transform.SetPositionAndRotation(
                hand.TransformPoint(anchorLocal), hand.rotation * openRot);

            // ── 마디 세 개 ────────────────────────────────────────────
            // 좌표는 (좌우, 아래로) 이고, 경첩이 원점이다. 오므린 자세 기준.
            //
            //   경첩 (0, 0)  →  팔꿈치 (바깥, 0.10)  →  패드 (안쪽, 0.18~0.28)
            Vector2 hinge = Vector2.zero;
            Vector2 elbow = new Vector2(side * (ElbowHalf - HingeHalf), ElbowDepth);
            Vector2 padTop = new Vector2(side * (PadHalf - HingeHalf), PadTop);
            Vector2 padEnd = new Vector2(side * (PadHalf - HingeHalf), PadBottom);

            var parts = new[]
            {
                Segment("Upper", go.transform, hinge, elbow, ArmThickness, UpperDepth, GripperMaterial),
                Segment("Lower", go.transform, elbow, padTop, ArmThickness, LowerDepth, GripperMaterial),
                Segment("Pad",   go.transform, padTop, padEnd, PadThickness, PadDepth, GripPadMaterial),
            };

            foreach (var part in parts)
            {
                var box = part.GetComponent<BoxCollider>();
                box.isTrigger = false;      // 통과하면 안 된다. 실제로 눌러야 마찰이 생긴다.
                box.sharedMaterial = friction;
            }

            var rb = go.AddComponent<Rigidbody>();
            // ★ 손가락이 가벼우면 아무리 세게 쥐어도 물체가 빠진다.
            //
            // 물리 엔진은 <b>무게 차이가 큰 두 물체를 관절로 잇는 것</b>에 약하다.
            // 가벼운 쪽이 무거운 쪽에 밀려나면서 계산이 흐트러진다.
            // 0.05kg 손가락으로는 0.2kg 물체도 흔들면 빠졌다.
            //
            //   손가락 0.05kg -> 최대 0.2kg, 흔들면 빠짐
            //   손가락 0.50kg -> 최대 2.0kg, 흔들어도 버팀   ← 이 값을 쓴다
            //   손가락 2.00kg -> 자기 관성 때문에 다시 빠짐
            //
            // 마찰 계수나 쥐는 힘을 아무리 올려도 이 문제는 안 고쳐진다.
            rb.mass = 0.6f;
            rb.useGravity = true;
            rb.solverIterations = 20;
            rb.angularDamping = 3f;         // 없으면 모터가 각도를 유지하려다 떨린다

            var h = go.AddComponent<HingeJoint>();
            h.connectedBody = handBody;

            // 두 손가락 모두 같은 축으로 돈다.
            // 방향은 모터 부호와 가동 범위로 구분한다.
            // (축을 서로 반대로 달면 가동 범위와 회전 방향이 어긋나 각도가 NaN이 된다)
            h.axis = Vector3.up;

            // 뿌리의 원점이 곧 경첩 자리라서 0이면 된다.
            // (anchor는 로컬 좌표이고 스케일의 영향을 받는다. 뿌리를 스케일 1인
            //  빈 오브젝트로 둔 덕분에 이런 계산을 안 해도 된다)
            h.anchor = Vector3.zero;
            h.autoConfigureConnectedAnchor = true;

            h.useLimits = true;

            // 지금 자세(바깥으로 젖혀진 상태)가 각도 0이다.
            // 여기서 OpenAngle 만큼 돌면 면이 나란해지고, 그 뒤로 SqueezeAngle 만큼 더 민다.
            // 왼쪽은 + 방향으로, 오른쪽은 - 방향으로 오므라든다.
            h.limits = left
                ? new JointLimits { min = -2f, max = GripRange, bounciness = 0f }
                : new JointLimits { min = -GripRange, max = 2f, bounciness = 0f };

            h.useMotor = true;
            h.enableCollision = false;      // 손목과는 부딪히지 않게

            var contact = go.AddComponent<FingerContact>();
            contact.target = target;
            contact.contactMargin = 0.01f;

            return go;
        }

        /// <summary>
        /// 마찰 재질. 이것이 5장의 핵심 수치다.
        /// 마찰이 약하면 물체가 손가락 사이로 흘러내린다.
        /// </summary>
        static PhysicsMaterial MakeFrictionMaterial()
        {
            const string path = "Assets/RobotArms/Materials/PM_Grip.asset";

            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            bool isNew = m == null;
            if (isNew) m = new PhysicsMaterial("PM_Grip");

            // 고무 장갑 수준으로 잡는다. 현실의 고무-금속이 1.0~1.5쯤이고,
            // 물리 엔진의 마찰계수는 1을 넘겨도 된다.
            m.staticFriction = 2.0f;      // 미끄러지기 시작하는 문턱
            m.dynamicFriction = 1.6f;     // 미끄러지는 중에 버티는 힘
            m.bounciness = 0f;

            // 두 재질이 만나면 더 큰 쪽을 쓴다.
            // 테이블처럼 미끄러운 것과 만나도 손가락의 마찰이 살아남는다.
            m.frictionCombine = PhysicsMaterialCombine.Maximum;
            m.bounceCombine = PhysicsMaterialCombine.Minimum;

            // 확장자는 .asset 이어야 한다. .physicsMaterial 로는 저장되지 않는다.
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);

            return m;
        }
    }
}
