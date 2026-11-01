using Humanoid;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static HumanoidEditor.HumanoidBuild;

namespace HumanoidEditor
{
    // 걷기 에이전트를 자연스럽게 걷기 에이전트(GaitAgent)로 바꾼다. 설명은 Docs/05-학습과-평가.md
    internal static class Ch5_Gait
    {
        [MenuItem("Humanoid/5. 자연스럽게 걷기 에이전트로 바꾸기", false, 5)]
        static void Setup()
        {
            var root = Find("Humanoid");
            if (root == null) return;
            var go = root.gameObject;
            var walk = go.GetComponent<WalkAgent>();
            if (walk == null) { Debug.LogError("[Humanoid] 먼저 4장 메뉴를 실행하세요."); return; }
            if (walk is GaitAgent) return;

            // 새 Agent 를 먼저 붙이고 옛 것을 지운다 (DecisionRequester 가 Agent 를 요구한다). 목표 · 속도는 그대로 옮긴다.
            var gait = Undo.AddComponent<GaitAgent>(go);
            gait.target = walk.target;
            gait.targetSpeed = walk.targetSpeed;
            gait.pushInterval = walk.pushInterval;
            gait.MaxStep = 0;       // 상한 없음: 판은 넘어짐 · 목표 제한시간으로만 끝난다. 목표를 맞히는 한 계속 걷는다
            Undo.DestroyObjectImmediate(walk);

            // 이름을 바꿔 4장 학습 결과와 따로 학습 · 기록한다. 관측 · 행동 수는 그대로(51 · 18)라 4장 모델도 넣어 볼 수 있다.
            go.GetComponent<BehaviorParameters>().BehaviorName = "HumanoidGait";

            EditorUtility.SetDirty(go);
            Debug.Log("[Humanoid] 자연스럽게 걷기 — 관측 51 / 행동 18. mlagents-learn Assets/Humanoid/humanoid_gait_config.yaml --run-id=gait");
        }
    }
}
