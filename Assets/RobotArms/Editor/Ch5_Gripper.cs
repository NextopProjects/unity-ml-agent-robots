using RobotArms;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 5장 — 자석을 떼고 집게를 단다. 팔은 1~2장에서 만든 그대로 둔다.
    ///
    /// 물리 부품은 셋뿐이다.
    ///   Hand      팔 끝을 따라다니는 kinematic Rigidbody (손목)
    ///   FingerL/R 경첩 + 모터로 여닫히는 손가락
    ///   Object    원래부터 Rigidbody였다. 손가락에 마찰 재질만 붙인다.
    ///
    /// 치수를 이렇게 정한 이유는 Docs/05-그리퍼로-교체.md 에 있다.
    /// </summary>
    internal static class Ch5_Gripper
    {
        // ── 집게 치수 (손목 기준, 오므린 자세) ──────────────────────
        //
        //     ● ●      경첩   HingeHalf
        //    ╱   ╲     위마디
        //   ▌     ▐    팔꿈치 ElbowHalf — 가장 바깥
        //    ╲   ╱     아래마디
        //     ▓ ▓      패드   PadHalf — 여기로 문다
        const float HingeHalf = 0.17f;
        const float ElbowHalf = 0.27f;
        const float ElbowDepth = 0.10f;
        const float PadHalf = 0.115f;       // 안쪽 면 0.095 — 물체 반폭 0.1을 문다
        const float PadTop = 0.18f;
        const float PadBottom = 0.28f;
        const float PadThickness = 0.04f;
        const float PadDepth = 0.26f;       // 앞뒤 허용 오차를 정하는 값
        const float ArmThickness = 0.05f;
        const float UpperDepth = 0.12f;
        const float LowerDepth = 0.14f;

        const float GripDepth = 0.22f;      // 손목 ~ 손가락 사이. 작으면 손끝이 테이블을 판다
        const float OpenAngle = 50f;        // 쉬는 자세 = 이만큼 벌어져 있다
        const float SqueezeAngle = 15f;     // 나란해진 뒤 더 미는 여유
        const float GripRange = OpenAngle + SqueezeAngle;
        const float GripperClearance = 0.14f;   // ★ 0.29로 두면 팔이 갇혀 학습이 실패한다

        // 편집 화면에서 보이는 자리. 실행 중에는 매 판 랜덤으로 다시 뽑힌다.
        static readonly Vector2 ObjectSpot = new Vector2(1.8f, 20f);
        static readonly Vector2 PlaceSpot = new Vector2(1.5f, -45f);

        [MenuItem("RobotArms/5. 집게로 교체 (마찰로 잡기)", false, 5)]
        static void Setup()
        {
            if (!RequireParts("Base", "Tip", "Object", "PlaceTarget")) return;

            var baseTf = Find("Base");
            var tip = Find("Tip");
            var obj = Find("Object");
            var place = Find("PlaceTarget");
            var area = GameObject.Find(AreaName).transform;

            var arm = baseTf.GetComponent<RobotArmController>();
            if (arm == null || arm.JointCount < 3)
            {
                EditorUtility.DisplayDialog("순서가 다릅니다",
                    "먼저 RobotArms > 2. 관절 제어 붙이기 를 실행하세요.", "확인");
                return;
            }

            // ── 1. 집게 (팔 바깥에 만든다) ──────────────────────────
            // Tip의 자식으로 매달면 손가락이 물리 몰래 순간이동해서 마찰이 안 생긴다.
            var old = Find("Gripper");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject("Gripper");
            Undo.RegisterCreatedObjectUndo(root, "집게 만들기");
            root.transform.SetParent(area, false);

            var handGo = new GameObject("Hand");
            handGo.transform.SetParent(root.transform, false);
            handGo.transform.SetPositionAndRotation(
                tip.position, Gripper.AimRotation(tip.position, baseTf.position));

            var handRb = handGo.AddComponent<Rigidbody>();
            handRb.isKinematic = true;
            handRb.useGravity = false;

            var friction = MakeFrictionMaterial();
            var fingerL = MakeFinger("FingerL", root.transform, handGo.transform, handRb, friction, true);
            var fingerR = MakeFinger("FingerR", root.transform, handGo.transform, handRb, friction, false);

            var gripper = handGo.AddComponent<Gripper>();
            gripper.follow = tip;
            gripper.yawReference = baseTf;
            gripper.target = obj;
            gripper.fingerL = fingerL.GetComponent<HingeJoint>();
            gripper.fingerR = fingerR.GetComponent<HingeJoint>();
            gripper.gripRange = GripRange;
            gripper.gripDepth = GripDepth;
            gripper.fingerOffset = HingeHalf;
            gripper.openAngle = OpenAngle;

            // ── 2. 팔 설정을 집게에 맞춘다 ──────────────────────────
            Undo.RecordObject(arm, "집게에 맞춰 팔 조정");

            // 2장 값(120·180·120)은 너무 빨라서 다가갈 때 물체를 쳐서 날린다.
            // 실제 모터처럼 절반으로 낮춘다. (어깨 · 팔꿈치 · 좌우)
            float[] speeds = { 60f, 90f, 60f };
            for (int i = 0; i < arm.joints.Length && i < speeds.Length; i++)
                arm.joints[i].maxSpeed = speeds[i];

            arm.joints[1].maxAngle = 0f;            // 팔꿈치를 아래로만 접히게
            arm.clearance = GripperClearance;       // 손가락 길이만큼 바닥 한계선을 올린다
            EditorUtility.SetDirty(arm);

            // ── 3. 물체 ─────────────────────────────────────────────
            // 마찰 재질은 손가락에만 붙인다. Combine이 Maximum이라 그걸로 충분하고,
            // 물체에까지 붙이면 테이블에 들러붙어 집게가 밀어 넣지 못한다.
            var objBody = obj.GetComponent<Rigidbody>();
            Undo.RecordObject(objBody, "물체 물리 설정");
            objBody.isKinematic = false;
            objBody.useGravity = true;
            objBody.sleepThreshold = 0f;    // 잠들면 손가락이 밀어도 안 움직인다
            objBody.solverIterations = 20;  // 물린 채 미끄러지는 것을 줄인다
            EditorUtility.SetDirty(objBody);

            // ── 4. 에이전트 교체 ────────────────────────────────────
            // 새 Agent를 먼저 붙인다. 먼저 지우면 DecisionRequester가 삭제를 거부한다.
            var agent = GetOrAdd<RobotArmGripperAgent>(baseTf.gameObject);

            var pickPlace = baseTf.GetComponent<RobotArmPickPlaceAgent>();
            if (pickPlace != null) Undo.DestroyObjectImmediate(pickPlace);

            var reachAgent = baseTf.GetComponent<RobotArmAgent>();
            if (reachAgent != null) Undo.DestroyObjectImmediate(reachAgent);

            Undo.RecordObject(agent, "집게 에이전트 설정");
            agent.arm = arm;
            agent.gripper = gripper;
            agent.targetObject = obj;
            agent.placeTarget = place;
            agent.hud = baseTf.GetComponent<ArmControlHud>();

            // 4장(2500)보다 넉넉하게. 손가락을 맞춰 넣는 시간이 든다.
            agent.MaxStep = 3000;

            var bp = GetOrAdd<BehaviorParameters>(baseTf.gameObject);
            Undo.RecordObject(bp, "두뇌 설정 갱신");
            bp.BehaviorName = "RobotArmGripper";
            bp.BrainParameters.VectorObservationSize = 19;  // 4장 16 + 쥔 정도 1 + 접촉 2
            bp.BrainParameters.NumStackedVectorObservations = 1;
            bp.BrainParameters.ActionSpec = new ActionSpec(3, new[] { 2 });
            bp.BehaviorType = BehaviorType.Default;

            // ── 5. 편집 화면을 시작 자세로 정돈한다 ──────────────────
            //
            // 메뉴를 실행한 뒤의 씬이 곧 <b>저장되는 씬</b>이다.
            // 팔이 아무 자세로, 손가락이 반쯤 오므린 채로 저장되면
            // 그 자세가 그대로 "처음 모습"이 된다.
            Undo.RecordObject(place, "놓을 자리 배치");
            place.position = arm.transform.TransformPoint(Spot(arm, PlaceSpot, 0.01f));

            Undo.RecordObject(obj, "물체 배치");
            obj.position = arm.transform.TransformPoint(Spot(arm, ObjectSpot, arm.spawnHeight));
            obj.rotation = Quaternion.identity;
            objBody.linearVelocity = Vector3.zero;
            objBody.angularVelocity = Vector3.zero;

            arm.ResetPose();        // 팔을 시작 각도로
            gripper.ResetPose();    // 집게를 팔 끝으로 데려오고 활짝 벌린다

            EditorUtility.SetDirty(obj);
            EditorUtility.SetDirty(agent);
            EditorUtility.SetDirty(bp);
            EditorUtility.SetDirty(place);
            Selection.activeGameObject = handGo;

            Debug.Log(
                "[RobotArms] 5장 완료 — 집게로 교체 (관측 19 / 연속 3 + 이산 1)\n" +
                "  Space를 누르면 손가락이 오므라들고, 마찰로 물체를 물어 쥡니다.\n" +
                "  붙이는 코드가 없으므로 세게 휘두르면 빠집니다. 그게 물리입니다.\n" +
                "  mlagents-learn Assets/RobotArms/robotarm_config.yaml --run-id=ch5_gripper");
        }

        // ── 도우미 ───────────────────────────────────────────────────

        /// <summary>(거리, 좌우각) 을 받침대 기준 좌표로. 높이는 따로 준다.</summary>
        static Vector3 Spot(RobotArmController arm, Vector2 spot, float height)
        {
            Vector3 p = arm.SpawnPointLocal(spot.x, spot.y);
            p.y = height;
            return p;
        }

        /// <summary>두 점을 잇는 막대 하나. 좌표는 (좌우, 아래로)이고 경첩이 원점이다.</summary>
        static GameObject Segment(string name, Transform parent, Vector2 from, Vector2 to,
            float thickness, float depth, Material material)
        {
            Vector2 delta = to - from;
            Vector2 mid = (from + to) * 0.5f;

            var go = CreateMesh(PrimitiveType.Cube, name, parent,
                new Vector3(mid.x, 0f, mid.y),
                new Vector3(thickness, depth, delta.magnitude),
                material, keepCollider: true);

            go.transform.localRotation =
                Quaternion.AngleAxis(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, Vector3.up);

            return go;
        }

        /// <summary>
        /// 손가락 하나. 마디 3개로 집게발 모양을 만든다.
        /// 치수는 "다 오므려 면이 나란해진 자세" 기준으로 잡고, 마지막에 통째로 젖힌다.
        /// </summary>
        static GameObject MakeFinger(string name, Transform parent, Transform hand,
            Rigidbody handBody, PhysicsMaterial friction, bool left)
        {
            float side = left ? -1f : 1f;

            // 뿌리는 스케일 1인 빈 오브젝트다. 스케일이 있으면 자식이 찌그러지고,
            // 원점이 곧 경첩 자리라서 anchor를 0으로 둘 수 있다.
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "손가락 생성");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(
                hand.TransformPoint(new Vector3(side * HingeHalf, 0f, 0f)),
                hand.rotation * Quaternion.AngleAxis(side * OpenAngle, Vector3.up));

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
                box.isTrigger = false;          // 실제로 눌러야 마찰이 생긴다
                box.sharedMaterial = friction;
            }

            // ★ 손가락이 가벼우면 아무리 세게 쥐어도 빠진다.
            //   0.05kg -> 0.2kg까지, 0.6kg -> 2.0kg까지 버틴다. 마찰·힘으로는 안 고쳐진다.
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.solverIterations = 20;
            rb.angularDamping = 3f;             // 없으면 모터가 각도를 유지하려다 떨린다

            var h = go.AddComponent<HingeJoint>();
            h.connectedBody = handBody;
            h.axis = Vector3.up;                // 두 손가락 모두 같은 축. 방향은 부호로 구분한다
            h.anchor = Vector3.zero;
            h.useLimits = true;
            h.limits = left
                ? new JointLimits { min = -2f, max = GripRange }
                : new JointLimits { min = -GripRange, max = 2f };
            h.useMotor = true;
            h.enableCollision = false;          // 손목과는 부딪히지 않게

            return go;
        }

        /// <summary>마찰 재질. 고무 장갑 수준. 약하면 물체가 손가락 사이로 흘러내린다.</summary>
        static PhysicsMaterial MakeFrictionMaterial()
        {
            const string path = "Assets/RobotArms/Materials/PM_Grip.asset";

            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            bool isNew = m == null;
            if (isNew) m = new PhysicsMaterial("PM_Grip");

            m.staticFriction = 2.0f;
            m.dynamicFriction = 1.6f;
            m.bounciness = 0f;

            // 두 재질이 만나면 큰 쪽을 쓴다. 그래서 손가락에만 붙이면 된다.
            m.frictionCombine = PhysicsMaterialCombine.Maximum;
            m.bounceCombine = PhysicsMaterialCombine.Minimum;

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);

            return m;
        }
    }
}
