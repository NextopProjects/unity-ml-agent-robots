using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HumanoidEditor
{
    // 위치 안내 — "어디를 고쳐야 하나"를 화면에 표시한다.
    //
    // Unity 6 에서는 UnityEditor.Highlighter 가 Inspector 를 강조하지 못한다 (Unity-Tutorial 프로젝트 실측).
    // 그래서 공개 API 로 할 수 있는 것만 쓴다.
    //   Hierarchy · Project  해당 줄에 노란 배경 + "◀ 여기"   (hierarchy/projectWindowItemOnGUI)
    //   Scene 뷰             대상에 노란 와이어 상자          (SceneView.duringSceneGui)
    //   Inspector            [📍 여기 보기]: 선택 + 창 앞으로 + 고칠 컴포넌트만 펼치기
    // 튜토리얼 창이 매 0.1초 '지금 할 일'의 대상을 넣어 준다.
    [InitializeOnLoad]
    internal static class TutorialGuide
    {
        static GameObject _object;      // 지금 할 일의 대상 (Hierarchy · Scene 에 표시)
        static string _asset;           // 지금 할 일의 에셋 경로 (Project 에 표시)
        static readonly Color Yellow = new Color(1f, 0.82f, 0.1f);

        static TutorialGuide()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItem;
            EditorApplication.projectWindowItemOnGUI += OnProjectItem;
            SceneView.duringSceneGui += OnScene;
        }

        // 표시할 대상을 바꾼다. 없으면 null.
        public static void Set(Task task)
        {
            var go = task != null && task.path != null ? GameObject.Find(task.path) : null;
            string asset = task?.window == "Project" ? task.path : null;
            if (go == _object && asset == _asset) return;
            _object = go;
            _asset = asset;
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
            SceneView.RepaintAll();
        }

        // [📍 여기 보기]: 대상을 선택하고 그 창을 앞으로 가져온다.
        public static void Reveal(Task task)
        {
            if (task == null) return;
            var go = task.path != null ? GameObject.Find(task.path) : null;

            switch (task.window)
            {
                case "Hierarchy":
                    EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                    if (go != null) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); }
                    break;

                case "Inspector":
                    if (go != null) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); }
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                    // 고칠 컴포넌트만 펼치고 나머지는 접는다. Inspector 에서 찾기 쉽게.
                    if (go != null && task.component != null)
                    {
                        foreach (var c in go.GetComponents<Component>())
                            if (c != null) InternalEditorUtility.SetIsInspectorExpanded(c, task.component.IsInstanceOfType(c));
                        ActiveEditorTracker.sharedTracker.ForceRebuild();
                    }
                    break;

                case "Project":
                    EditorApplication.ExecuteMenuItem("Window/General/Project");
                    var asset = AssetDatabase.LoadMainAssetAtPath(task.path);
                    if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                    break;

                case "Game":
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    break;

                case "Scene":
                    if (go != null) { Selection.activeGameObject = go; if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected(); }
                    break;
            }
        }

        // ── 그리기 ──────────────────────────────────────────────────

        static void OnHierarchyItem(int id, Rect row)
        {
            if (_object == null || id != _object.GetInstanceID()) return;
            Mark(row);
        }

        static void OnProjectItem(string guid, Rect row)
        {
            if (_asset == null || AssetDatabase.GUIDToAssetPath(guid) != _asset) return;
            Mark(row);
        }

        static void Mark(Rect row)
        {
            EditorGUI.DrawRect(row, new Color(Yellow.r, Yellow.g, Yellow.b, 0.25f));
            var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = Yellow } };
            GUI.Label(row, "◀ 여기  ", style);
        }

        static void OnScene(SceneView view)
        {
            if (_object == null) return;
            var renderers = _object.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(_object.transform.position, Vector3.one * 0.2f);
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            Handles.color = Yellow;
            Handles.DrawWireCube(b.center, b.size * 1.05f);
            Handles.Label(b.center + Vector3.up * (b.extents.y + 0.1f), "◀ " + _object.name, EditorStyles.boldLabel);
        }
    }
}
