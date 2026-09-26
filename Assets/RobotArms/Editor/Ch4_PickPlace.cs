using RobotArms;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 4장 — 집어서 옮겨 놓기. (자석 방식)
    ///
    /// 3장의 <see cref="RobotArmAgent"/>를 떼고
    /// <see cref="RobotArmPickPlaceAgent"/>로 바꾼다.
    /// 두 파일을 나란히 열어놓고 비교하면 무엇이 늘었는지 그대로 보인다.
    /// </summary>
    internal static class Ch4_PickPlace
    {
        // 놓을 자리. 4장에서는 고정이다. (6장에서 랜덤으로 바꾼다)
        static readonly Vector2 PlaceSpot = new Vector2(1.5f, -45f);

        [MenuItem("RobotArms/4. 집어서 옮겨 놓기 (자석)", false, 4)]
        static void Setup()
        {
            if (!RequireParts("Base", "Tip", "Object", "PlaceTarget")) return;

            var baseTf = Find("Base");
            var arm = baseTf.GetComponent<RobotArmController>();
            if (arm == null || arm.JointCount < 3)
            {
                EditorUtility.DisplayDialog("순서가 다릅니다",
                    "먼저 'RobotArms > 2. 관절 제어 붙이기'를 실행하세요.", "확인");
                return;
            }

            // 한 오브젝트에 Agent는 하나만 있어야 한다.
            //
            // 순서가 중요하다. 3장의 에이전트를 먼저 지우면
            // DecisionRequester가 "Agent가 필요하다"며 삭제를 거부한다.
            // 새 에이전트를 먼저 붙여 조건을 만족시킨 뒤에 옛 것을 지운다.
            var agent = GetOrAdd<RobotArmPickPlaceAgent>(baseTf.gameObject);

            var reachAgent = baseTf.GetComponent<RobotArmAgent>();
            if (reachAgent != null) Undo.DestroyObjectImmediate(reachAgent);

            Undo.RecordObject(agent, "집어서 옮겨놓기 에이전트 설정");
            agent.arm = arm;
            agent.targetObject = Find("Object");
            agent.placeTarget = Find("PlaceTarget");
            agent.hud = baseTf.GetComponent<ArmControlHud>();

            // 잡을 수 있는 거리는 통과 방지의 "물체 검사 예외 거리"보다 작아야 한다.
            // 그렇지 않으면 다가가는 것 자체가 막혀 영영 잡을 수 없다.
            agent.grabDistance = 0.25f;

            agent.placeRadius = 0.25f;
            agent.restSpeed = 0.2f;
            agent.spawnDistanceRange = SpawnDistanceRange;
            agent.spawnYawRange = SpawnYawRange;
            agent.placeSpot = PlaceSpot;
            agent.minSeparation = 0.6f;

            agent.useDenseReward = true;
            agent.grabReward = 0.5f;
            agent.placeReward = 1.0f;
            agent.dropPenalty = 0.3f;

            // 도달 + 집기 + 운반 + 놓기. 3장(1000)보다 할 일이 훨씬 많다.
            agent.MaxStep = 2500;

            var bp = GetOrAdd<BehaviorParameters>(baseTf.gameObject);
            Undo.RecordObject(bp, "두뇌 설정 갱신");

            // 관측: 3장의 12개 + 잡고 있는가 1 + 놓을 자리 위치 3 = 16
            bp.BehaviorName = "RobotArmPickPlace";
            bp.BrainParameters.VectorObservationSize = 16;
            bp.BrainParameters.NumStackedVectorObservations = 1;

            // 액션: 연속 3개(관절) + 이산 1묶음(크기 2: 0=놓기, 1=잡기)
            // ML-Agents는 연속과 이산을 동시에 쓸 수 있다. RollerBall에는 없던 개념이다.
            bp.BrainParameters.ActionSpec = new ActionSpec(3, new[] { 2 });
            bp.BehaviorType = BehaviorType.Default;

            // 놓을 자리를 제자리에 옮겨둔다 (편집 중에도 보이도록)
            var place = Find("PlaceTarget");
            Undo.RecordObject(place, "놓을 자리 배치");
            Vector3 local = arm.SpawnPointLocal(PlaceSpot.x, PlaceSpot.y);
            local.y = 0.01f;
            place.position = arm.transform.TransformPoint(local);

            EditorUtility.SetDirty(agent);
            EditorUtility.SetDirty(bp);
            EditorUtility.SetDirty(place);
            Selection.activeGameObject = baseTf.gameObject;

            Debug.Log(
                "[RobotArms] 4장 완료 — 집어서 옮겨 놓기 (관측 16 / 연속 3 + 이산 1)\n" +
                "  Q/A 어깨,  W/S 팔꿈치,  E/D 좌우 회전,  Space 잡기,  R 초기화\n" +
                "  물체를 집어서 초록 원판 위로 옮기고 Space를 떼면 성공입니다.\n" +
                "  먼저 Heuristic Only로 직접 해보고 나서 학습을 돌리세요.\n" +
                "  mlagents-learn Assets/RobotArms/robotarm_config.yaml --run-id=ch4_pickplace");
        }
    }
}
