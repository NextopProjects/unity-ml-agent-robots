using UnityEditor;
using UnityEngine;

namespace HumanoidEditor
{
    // 씬 구성 메뉴들이 함께 쓰는 것만 모아 둔다.
    internal static class HumanoidBuild
    {
        // RobotArms의 "TrainingArea"와 이름이 겹치지 않게 한다.
        public const string AreaName = "HumanoidArea";

        // 학습 영역 안에서 이름으로 찾는다. 없으면 콘솔에 안내하고 null.
        public static Transform Find(string name)
        {
            var area = GameObject.Find(AreaName);
            if (area != null)
                foreach (var t in area.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;

            Debug.LogError("[Humanoid] '" + name + "'이(가) 없습니다. 메뉴를 장 순서대로 실행하세요.");
            return null;
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();     // ?? 를 쓰지 않는다. Unity의 "없음"은 C#의 null과 다를 수 있다
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        // 색 이름으로 머티리얼 에셋을 만들어 재사용한다.
        public static Material Mat(string name, Color color)
        {
            string path = "Assets/Humanoid/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            System.IO.Directory.CreateDirectory("Assets/Humanoid/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");      // URP가 없는 프로젝트
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 보이는 모양만 만든다(콜라이더 없음). 충돌은 스케일 1인 부모의 콜라이더가 맡는다.
        public static void Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = parent.name + "Mesh";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
