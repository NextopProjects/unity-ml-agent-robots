using RobotArms;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 3장 — 에이전트 설정 (도달 학습).
    /// 2장의 키보드 입력 자리에 AI를 꽂는다.
    /// </summary>
    internal static class Ch3_Agent
    {
        [MenuItem("RobotArms/3. 에이전트 설정 (도달 학습)", false, 3)]
        static void Setup()
        {
            if (!RequireParts("Base", "Object")) return;

            var baseTf = Find("Base");
            var arm = baseTf.GetComponent<RobotArmController>();
            if (arm == null || arm.JointCount < 3)
            {
                EditorUtility.DisplayDialog("순서가 다릅니다",
                    "먼저 'RobotArms > 2. 관절 제어 붙이기'를 실행하세요.", "확인");
                return;
            }

            // 2장의 키보드 드라이버는 꺼둔다.
            // 이제부터는 Agent의 Heuristic()이 똑같은 일을 한다.
            // (지우지 않고 꺼두는 이유: 2장으로 돌아가 비교해보기 쉽도록)
            var driver = baseTf.GetComponent<ManualArmDriver>();
            if (driver != null)
            {
                Undo.RecordObject(driver, "키보드 드라이버 끄기");
                driver.enabled = false;
            }

            var agent = GetOrAdd<RobotArmAgent>(baseTf.gameObject);
            Undo.RecordObject(agent, "에이전트 설정");
            agent.arm = arm;
            agent.targetObject = Find("Object");
            agent.hud = baseTf.GetComponent<ArmControlHud>();
            agent.successDistance = 0.25f;
            agent.spawnDistanceRange = SpawnDistanceRange;
            agent.spawnYawRange = SpawnYawRange;
            agent.useDenseReward = true;
            agent.successReward = 1.0f;

            // 실패한 에피소드가 영원히 안 끝나는 것을 막는다.
            // RollerBall은 0(무제한)이었지만, 팔은 반드시 값을 넣어야 한다.
            agent.MaxStep = 1000;

            var bp = GetOrAdd<BehaviorParameters>(baseTf.gameObject);
            Undo.RecordObject(bp, "두뇌 설정");
            bp.BehaviorName = "RobotArm";

            // 관측: 관절 각도 3 + 각속도 3 + Tip 위치 3 + 물체 위치 3 = 12
            // ← CollectObservations에서 넣는 개수와 반드시 같아야 한다.
            //   하나라도 다르면 Play하는 순간 에러가 난다.
            bp.BrainParameters.VectorObservationSize = 12;
            bp.BrainParameters.NumStackedVectorObservations = 1;

            // 액션: 관절 개수(3 DOF)와 같다
            bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(3);
            bp.BehaviorType = BehaviorType.Default;

            var requester = GetOrAdd<DecisionRequester>(baseTf.gameObject);
            Undo.RecordObject(requester, "판단 주기 설정");
            requester.DecisionPeriod = 5;                 // 물리 50Hz → AI 10Hz
            requester.TakeActionsBetweenDecisions = true; // 판단 사이에도 직전 액션을 계속 적용

            EditorUtility.SetDirty(agent);
            EditorUtility.SetDirty(bp);
            EditorUtility.SetDirty(requester);
            Selection.activeGameObject = baseTf.gameObject;

            Debug.Log(
                "[RobotArms] 3장 완료 — 도달 학습 준비가 끝났습니다. (관측 12 / 연속 액션 3)\n" +
                "  1) Behavior Type을 'Heuristic Only'로 두고 Play → 에러 없이 도는지 확인\n" +
                "  2) mlagents-learn Assets/RobotArms/robotarm_config.yaml --run-id=ch3_reach\n" +
                "  3) 'Start training by pressing Play' 가 뜨면 Unity에서 Play\n" +
                "  ※ 먼저 Use Dense Reward를 꺼서 '학습이 안 되는 것'을 보고 나서 켜보세요.");
        }
    }
}
