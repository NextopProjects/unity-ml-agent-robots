using RobotArms;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 4장 — 도달 에이전트를 "집어서 옮겨 놓기"로 교체한다. (자석 방식)
    /// 자세한 설명은 Docs/04-집어서-옮겨놓기.md 를 본다.
    /// </summary>
    internal static class Ch4_PickPlace
    {
        // 편집 화면에서 보이는 자리. 실행 중에는 매 판 랜덤으로 다시 뽑힌다.
        static readonly Vector2 ObjectSpot = new Vector2(1.8f, 20f);
        static readonly Vector2 PlaceSpot = new Vector2(1.5f, -45f);

        [MenuItem("RobotArms/4. 집어서 옮겨 놓기 (자석)", false, 4)]
        static void Setup()
        {
            if (!RequireParts("Base", "Tip", "Object", "PlaceTarget")) return;

            var baseTf = Find("Base");
            var obj = Find("Object");
            var place = Find("PlaceTarget");
            var arm = baseTf.GetComponent<RobotArmController>();

            if (arm == null || arm.JointCount < 3)
            {
                EditorUtility.DisplayDialog("순서가 다릅니다",
                    "먼저 'RobotArms > 2. 관절 제어 붙이기'를 실행하세요.", "확인");
                return;
            }

            // ── 에이전트 교체 ───────────────────────────────────────
            // 순서 주의: 새 Agent를 먼저 붙인 뒤에 옛 것을 지운다.
            // 먼저 지우면 DecisionRequester가 Agent가 필요하다며 삭제를 거부한다.
            var agent = GetOrAdd<RobotArmPickPlaceAgent>(baseTf.gameObject);

            var reachAgent = baseTf.GetComponent<RobotArmAgent>();
            if (reachAgent != null) Undo.DestroyObjectImmediate(reachAgent);

            Undo.RecordObject(agent, "집기 에이전트 설정");
            agent.arm = arm;
            agent.targetObject = obj;
            agent.placeTarget = place;
            agent.hud = baseTf.GetComponent<ArmControlHud>();

            // 3장(1000)보다 길게 준다. 가서 집고 옮겨 놓기까지 해야 하고,
            // 성공해도 판이 끝나지 않고 다음 한 번을 이어서 한다.
            agent.MaxStep = 3000;

            var bp = GetOrAdd<BehaviorParameters>(baseTf.gameObject);
            Undo.RecordObject(bp, "두뇌 설정 갱신");
            bp.BehaviorName = "RobotArmPickPlace";

            // 관측 16 = 3장의 12 + 잡고있는가 1 + 지금 가야 할 점 3
            bp.BrainParameters.VectorObservationSize = 16;
            bp.BrainParameters.NumStackedVectorObservations = 1;

            // 액션: 연속 3(관절) + 이산 1(잡기/놓기, 값 2개)
            bp.BrainParameters.ActionSpec = new ActionSpec(3, new[] { 2 });
            bp.BehaviorType = BehaviorType.Default;

            // ── 물체 ────────────────────────────────────────────────
            // 잡는 동안만 isKinematic을 켰다 끈다. 평소에는 진짜 물리 물체다.
            var objBody = obj.GetComponent<Rigidbody>();
            Undo.RecordObject(objBody, "물체 물리 설정");
            objBody.isKinematic = false;
            objBody.useGravity = true;
            EditorUtility.SetDirty(objBody);

            // ── 편집 화면을 시작 자세로 정돈한다 ────────────────────
            // 메뉴를 실행한 뒤의 씬이 곧 저장되는 씬이기 때문이다.
            Undo.RecordObject(obj, "물체 배치");
            obj.SetPositionAndRotation(
                arm.transform.TransformPoint(Spot(arm, ObjectSpot, arm.spawnHeight)),
                Quaternion.identity);

            Undo.RecordObject(place, "놓을 자리 배치");
            place.position = arm.transform.TransformPoint(Spot(arm, PlaceSpot, 0.01f));

            arm.ResetPose();

            EditorUtility.SetDirty(agent);
            EditorUtility.SetDirty(bp);
            EditorUtility.SetDirty(obj);
            EditorUtility.SetDirty(place);
            Selection.activeGameObject = baseTf.gameObject;

            Debug.Log(
                "[RobotArms] 4장 완료 — 집어서 옮겨 놓기 (관측 16 / 연속 3 + 이산 1)\n" +
                "  Space를 누르면 가까이 있는 물체가 팔 끝에 붙습니다. 떼면 놓입니다.\n" +
                "  성공해도 판이 끝나지 않고 놓을 자리만 새 랜덤 자리로 옮겨집니다.\n" +
                "  성적표는 TensorBoard의 Task/Cycles (한 판에 몇 번 옮겼나)입니다.\n" +
                "  mlagents-learn Assets/RobotArms/robotarm_config.yaml --run-id=ch4_pickplace");
        }

        /// <summary>(거리, 좌우각) 을 받침대 기준 좌표로. 높이는 따로 준다.</summary>
        static Vector3 Spot(RobotArmController arm, Vector2 spot, float height)
        {
            Vector3 p = arm.SpawnPointLocal(spot.x, spot.y);
            p.y = height;
            return p;
        }
    }
}
