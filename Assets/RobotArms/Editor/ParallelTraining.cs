using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 학습 영역을 여러 개로 복제한다. (병렬 학습)
    ///
    /// ── 왜 복제하는가 ───────────────────────────────────────────────
    ///
    /// 강화학습이 느린 이유는 계산이 무거워서가 아니라
    /// <b>경험을 모으는 속도가 느려서</b>다.
    /// 팔 하나로는 1초에 50스텝밖에 못 모은다.
    ///
    /// 같은 환경을 25개 만들어 동시에 돌리면 경험이 25배로 쌓인다.
    /// 학습 코드는 <b>한 줄도 바꾸지 않는다.</b>
    /// ML-Agents는 <see cref="Unity.MLAgents.Policies.BehaviorParameters.BehaviorName"/>이
    /// 같은 에이전트를 모두 <b>한 두뇌</b>로 묶어 학습하기 때문이다.
    ///
    /// ── 주의 ────────────────────────────────────────────────────────
    ///
    /// 복제는 <b>1~5장 구성을 모두 마친 뒤</b> 마지막에 한다.
    /// 장별 메뉴는 이름으로 부품을 찾으므로(Find) 복제본이 있으면
    /// 첫 번째 영역만 고치게 된다.
    /// </summary>
    internal static class ParallelTraining
    {
        // 25개 = 5 x 5. 늘릴수록 빨라지지만 그만큼 컴퓨터가 버거워진다.
        //
        // 이 숫자를 바꾸는 것만으로 경험 수집 속도가 그대로 배수가 된다.
        // 버거우면 9(3x3)로 줄인다. 학습 코드는 어느 쪽이든 그대로다.
        const int AreaCount = 25;
        const int Columns = 5;

        // 영역 간격. 테이블이 5.0 x 4.0 이므로 서로 닿지 않게 넉넉히 띄운다.
        const float SpacingX = 7f;
        const float SpacingZ = 6.5f;

        [MenuItem("RobotArms/학습 영역 복제 (병렬 학습)", false, 20)]
        static void Clone()
        {
            var origin = GameObject.Find(AreaName);
            if (origin == null)
            {
                EditorUtility.DisplayDialog("TrainingArea가 없습니다",
                    "먼저 'RobotArms > 1. 환경 만들기'부터 순서대로 실행하세요.", "확인");
                return;
            }

            RemoveClones();

            // 원본도 격자의 첫 칸에 맞춰 둔다.
            Undo.RecordObject(origin.transform, "학습 영역 배치");
            origin.transform.position = Vector3.zero;

            for (int i = 1; i < AreaCount; i++)
            {
                var copy = Object.Instantiate(origin);
                copy.name = AreaName + " " + i;
                copy.transform.position = new Vector3(
                    (i % Columns) * SpacingX, 0f, (i / Columns) * SpacingZ);

                Undo.RegisterCreatedObjectUndo(copy, "학습 영역 복제");

                // 화면 UI는 한 영역만 그린다. 25개가 겹쳐 그리면 읽을 수 없다.
                foreach (var hud in copy.GetComponentsInChildren<RobotArms.ArmControlHud>(true))
                    hud.enabled = false;
            }

            Selection.activeGameObject = origin;

            Debug.Log(
                "[RobotArms] 학습 영역 " + AreaCount + "개로 복제했습니다.\n" +
                "  스크립트는 하나도 고치지 않았습니다. Behavior Name이 같으면\n" +
                "  ML-Agents가 알아서 한 두뇌로 묶어 학습합니다.\n" +
                "  경험이 " + AreaCount + "배로 쌓이므로 그만큼 빨리 배웁니다.\n" +
                "  되돌리려면 메뉴 > RobotArms > 학습 영역 되돌리기");
        }

        [MenuItem("RobotArms/학습 영역 되돌리기 (1개로)", false, 21)]
        static void Reset()
        {
            int removed = RemoveClones();

            var origin = GameObject.Find(AreaName);
            if (origin != null)
            {
                Undo.RecordObject(origin.transform, "학습 영역 배치");
                origin.transform.position = Vector3.zero;
            }

            Debug.Log("[RobotArms] 복제본 " + removed + "개를 지웠습니다. 학습 영역 1개로 돌아왔습니다.");
        }

        /// <summary>복제본만 지운다. 원본(TrainingArea)은 남긴다.</summary>
        static int RemoveClones()
        {
            int removed = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == null) continue;
                if (go.transform.parent != null) continue;

                // "TrainingArea 1", "TrainingArea 2" ... 만 지운다
                if (go.name.StartsWith(AreaName + " "))
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }
            return removed;
        }
    }
}
