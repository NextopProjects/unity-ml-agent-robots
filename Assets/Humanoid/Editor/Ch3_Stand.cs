using Humanoid;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using static HumanoidEditor.HumanoidBuild;

namespace HumanoidEditor
{
    /// <summary>3장 — 서기 에이전트를 붙인다. 설명은 Docs/03-서기-학습.md</summary>
    internal static class Ch3_Stand
    {
        [MenuItem("Humanoid/3. 서기 에이전트 붙이기", false, 3)]
        static void Setup()
        {
            var root = Find("Humanoid");
            if (root == null) return;
            var go = root.gameObject;

            // 키보드는 행동만 만들고 몸은 에이전트가 움직인다. (Heuristic Only 일 때 키보드 행동이 쓰인다)
            GetOrAdd<KeyboardControl>(go).driveBody = false;

            // 4장 상태(WalkAgent)에서 실행해도 서기 에이전트로 돌아오게 한다. (WalkAgent도 StandAgent라 GetOrAdd로는 못 바꾼다)
            var agent = go.GetComponent<StandAgent>();
            if (agent == null || agent is WalkAgent)
            {
                var old = agent;
                agent = Undo.AddComponent<StandAgent>(go);      // DecisionRequester가 Agent를 요구해서 새 것을 먼저 붙인다
                if (old != null) Undo.DestroyObjectImmediate(old);
            }
            agent.MaxStep = 1000;   // 20초. 넘어지지 않으면 여기서 끝난다

            var bp = GetOrAdd<BehaviorParameters>(go);
            bp.BehaviorName = "HumanoidStand";
            bp.BrainParameters.VectorObservationSize = 36;              // CollectObservations 개수와 같아야 한다
            bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(12);
            bp.BehaviorType = BehaviorType.Default;

            var dr = GetOrAdd<DecisionRequester>(go);
            dr.DecisionPeriod = 5;                      // 물리 50Hz → 판단 10Hz
            dr.TakeActionsBetweenDecisions = true;      // 판단 사이에도 직전 행동 유지

            EditorUtility.SetDirty(go);
            UnityEngine.Debug.Log("[Humanoid] 3장 완료 — 관측 36 / 행동 12. mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=stand");
        }
    }
}
