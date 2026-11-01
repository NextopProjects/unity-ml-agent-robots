using Humanoid;
using Unity.MLAgents;
using UnityEditor;
using UnityEngine;

namespace HumanoidEditor
{
    // 집어서 옮기기 평가. Play 중에 지금 붙어 있는 모델로 판을 여러 번 돌리고 성적표를 콘솔에 찍는다.
    // 성공률 · 잡기 · 들기 · 배치 오차 · 떨어뜨림 · 넘어짐 · 걸린 시간 · 몸통 기울기.
    // 걸음걸이 평가(5장)처럼 같은 씨앗으로 화면 없이 물리를 직접 돌린다.
    public static class CarryEvaluation
    {
        [MenuItem("Humanoid/집어서 옮기기 평가 (Play 중 · 20판)", false, 21)]
        static void Evaluate()
        {
            var agent = Object.FindFirstObjectByType<CarryAgent>();
            if (!EditorApplication.isPlaying || agent == null) { Debug.LogError("[Humanoid] 옮기기 에이전트가 있는 씬을 Play 한 뒤 실행하세요."); return; }
            Debug.Log("[Humanoid] 집어서 옮기기 평가 — " + Run(agent, 20, 1));
        }

        public static string Run(CarryAgent agent, int episodes, int seed)
        {
            int success = 0, grasped = 0, lifted = 0, dropped = 0, fell = 0;
            float error = 0f, seconds = 0f, tilt = 0f;

            var oldMode = Physics.simulationMode;
            bool oldStepping = Academy.Instance.AutomaticSteppingEnabled;
            Physics.simulationMode = SimulationMode.Script;
            Academy.Instance.AutomaticSteppingEnabled = false;
            try
            {
                Random.InitState(seed);
                agent.EndEpisode();
                int start = agent.CompletedEpisodes;
                int guard = episodes * (agent.MaxStep + 10) + 10;
                while (agent.CompletedEpisodes - start < episodes && guard-- > 0)
                {
                    int before = agent.CompletedEpisodes;
                    Academy.Instance.EnvironmentStep();
                    Physics.Simulate(Time.fixedDeltaTime);
                    if (agent.CompletedEpisodes == before) continue;

                    // 판 하나가 끝났다. EndEpisode 가 곧바로 다음 판을 시작하며(OnEpisodeBegin) 지난 판 요약을 만든다
                    var s = agent.Last;
                    if (s.result == "Success") { success++; error += s.placeError; seconds += s.seconds; }
                    if (s.grasped) grasped++;
                    if (s.lifted) lifted++;
                    if (s.result == "Dropped") dropped++;
                    if (s.result == "Fell") fell++;
                    tilt += s.tilt;
                }
                int n = agent.CompletedEpisodes - start;
                return string.Format("{0}판 · 성공 {1} ({2:P0}) · 잡기 {3} · 들기 {4} · 떨어뜨림 {5} · 넘어짐 {6} · 시간 초과 {7}\n" +
                                     "성공한 판: 배치 오차 {8:F3} m · 걸린 시간 {9:F1} 초 · 몸통 기울기 평균 {10:F1}°",
                    n, success, success / (float)Mathf.Max(1, n), grasped, lifted, dropped, fell, n - success - dropped - fell,
                    success > 0 ? error / success : 0f, success > 0 ? seconds / success : 0f, tilt / Mathf.Max(1, n));
            }
            finally
            {
                Physics.simulationMode = oldMode;
                Academy.Instance.AutomaticSteppingEnabled = oldStepping;
            }
        }
    }
}
