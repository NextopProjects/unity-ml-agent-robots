using Humanoid;
using Unity.MLAgents;
using UnityEditor;
using UnityEngine;

namespace HumanoidEditor
{
    // 걸음걸이 평가. Play 중에 지금 붙어 있는 모델로 정해진 시간만큼 걷게 하고 성적표(GaitStats)를 콘솔에 찍는다.
    // 같은 씨앗(무작위 시작값)과 같은 조건으로 돌리므로, 모델 두 개를 바꿔 끼워 가며 숫자로 비교할 수 있다.
    // 화면을 그리지 않고 물리를 직접 돌려서 120초가 몇 초 만에 끝난다.
    public static class GaitEvaluation
    {
        const int Seconds = 120;

        [MenuItem("Humanoid/걸음걸이 평가 (Play 중 · 120초)", false, 20)]
        static void Evaluate()
        {
            var agent = Object.FindFirstObjectByType<WalkAgent>();
            if (!EditorApplication.isPlaying || agent == null) { Debug.LogError("[Humanoid] 걷기 에이전트가 있는 씬을 Play 한 뒤 실행하세요."); return; }
            Debug.Log("[Humanoid] 걸음걸이 평가 — " + Run(agent, Seconds, 1));
        }

        // 학습 중과 같은 방식으로 에이전트를 돌린다: 판단 · 행동 · 판 끝내기는 Academy 가, 물리는 Physics.Simulate 가.
        public static string Run(WalkAgent agent, int seconds, int seed)
        {
            var body = agent.GetComponent<HumanoidBody>();
            var stats = new GaitStats();
            int falls = 0;

            var oldMode = Physics.simulationMode;
            bool oldStepping = Academy.Instance.AutomaticSteppingEnabled;
            Physics.simulationMode = SimulationMode.Script;
            Academy.Instance.AutomaticSteppingEnabled = false;
            try
            {
                Random.InitState(seed);
                agent.EndEpisode();
                int episodes = agent.CompletedEpisodes;
                bool wasFallen = false;
                for (int i = 0; i < seconds / Time.fixedDeltaTime; i++)
                {
                    Academy.Instance.EnvironmentStep();
                    Physics.Simulate(Time.fixedDeltaTime);

                    Vector3 dir = agent.target.position - body.pelvis.transform.position;
                    dir.y = 0f;
                    stats.Step(body, dir.normalized, Time.fixedDeltaTime);
                    if (body.HasFallen && !wasFallen) falls++;
                    wasFallen = body.HasFallen;
                }
                return string.Format("{0}초 · 넘어짐 {1}번 · 판 {2}개\n{3}", seconds, falls, agent.CompletedEpisodes - episodes, stats.Report());
            }
            finally
            {
                Physics.simulationMode = oldMode;
                Academy.Instance.AutomaticSteppingEnabled = oldStepping;
            }
        }
    }
}
