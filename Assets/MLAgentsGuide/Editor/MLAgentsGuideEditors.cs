using System;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;

namespace MLAgentsGuide
{
    /// <summary>
    /// ML-Agents 컴포넌트의 Inspector 안, 필드 바로 위에 설명을 붙인다.
    ///
    /// <para><b>패키지 에디터를 가로채는 구조인 이유</b></para>
    /// <para>
    /// 같은 타입에 [CustomEditor]가 둘이면 Unity는 후보를
    /// <c>SortUnityTypesFirstComparer</c>로 정렬한 뒤 앞의 것을 고른다. 이 비교자는
    /// <c>InternalEditorUtility.IsUnityAssembly</c>(어셈블리 이름이
    /// <c>^(Unity|UnityEditor|UnityEngine).</c> 에 걸리는지)로 판정해 Unity 쪽을 뒤로 보낸다.
    /// 패키지 에디터는 <c>Unity.ML-Agents.Editor</c>, 이 파일은 <c>Assembly-CSharp-Editor</c>라
    /// <b>여기 있는 에디터가 선택된다.</b>
    /// </para>
    /// <para>
    /// 그래서 우리가 직접 필드를 그리면 패키지가 제공하던 기능(모델 호환성 검증,
    /// Play 중 잠금, 값 변경 시 정책 갱신)이 사라진다. 대신 패키지 에디터를 만들어
    /// 그리기를 그대로 넘기고, 우리는 그 위에 설명만 얹는다.
    /// </para>
    ///
    /// 설명은 값을 읽기만 한다. serializedObject를 건드리지 않는다.
    /// </summary>
    internal abstract class GuidedEditor : UnityEditor.Editor
    {
        const string MenuToggle = "Window/ML-Agents/가이드 표시";
        const string EnabledKey = "MLAgentsGuide.Enabled";
        const string FoldKeyPrefix = "MLAgentsGuide.Fold.";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuToggle, false, 20)]
        static void ToggleEnabled()
        {
            Enabled = !Enabled;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                window.Repaint();
            }
        }

        [MenuItem(MenuToggle, true)]
        static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuToggle, Enabled);
            return true;
        }

        // ------------------------------------------------------------------

        /// <summary>표시할 설명.</summary>
        protected abstract GuideContent.Component Guide { get; }

        /// <summary>그리기를 넘길 패키지 에디터의 타입 이름. 없으면 null.</summary>
        protected virtual string InnerEditorTypeName => null;

        /// <summary>현재 값을 초보자가 읽을 수 있는 문장으로 풀어 쓴다. 필요 없으면 null.</summary>
        protected virtual string CurrentState => null;

        UnityEditor.Editor m_Inner;
        bool m_Resolved;

        protected virtual void OnEnable()
        {
            if (m_Resolved || string.IsNullOrEmpty(InnerEditorTypeName))
            {
                return;
            }

            m_Resolved = true;

            try
            {
                // 패키지 에디터는 internal이라 직접 참조할 수 없어 이름으로 찾는다.
                foreach (var type in TypeCache.GetTypesDerivedFrom<UnityEditor.Editor>())
                {
                    if (type.FullName == InnerEditorTypeName)
                    {
                        m_Inner = CreateEditor(targets, type);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ML-Agents 가이드] 패키지 에디터를 준비하지 못했습니다: {e.Message}");
                m_Inner = null;
            }
        }

        protected virtual void OnDisable()
        {
            if (m_Inner != null)
            {
                DestroyImmediate(m_Inner);
                m_Inner = null;
            }

            m_Resolved = false;
        }

        public override void OnInspectorGUI()
        {
            if (Enabled)
            {
                // 설명을 그리다 예외가 나도 컴포넌트 본체는 반드시 그려야 한다.
                try
                {
                    DrawGuide();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ML-Agents 가이드] 설명을 그리지 못했습니다: {e.Message}");
                }
            }

            if (m_Inner != null)
            {
                m_Inner.OnInspectorGUI();
            }
            else if (string.IsNullOrEmpty(InnerEditorTypeName))
            {
                DrawDefaultInspector();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "ML-Agents 패키지의 기본 에디터를 찾지 못했습니다. " +
                    "Window > ML-Agents > 가이드 표시 를 끄면 원래 화면으로 돌아갑니다.",
                    MessageType.Warning);
                DrawFallback();
            }
        }

        /// <summary>패키지 에디터를 못 찾았을 때의 대체 화면.</summary>
        protected virtual void DrawFallback()
        {
            DrawDefaultInspector();
        }

        void DrawGuide()
        {
            var guide = Guide;
            if (guide == null)
            {
                return;
            }

            var key = FoldKeyPrefix + GetType().Name;
            var open = EditorPrefs.GetBool(key, true);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var next = EditorGUILayout.Foldout(open, "설명", true);
                if (next != open)
                {
                    EditorPrefs.SetBool(key, next);
                    open = next;
                }

                if (!open)
                {
                    return;
                }

                Body(guide.Summary);

                if (!string.IsNullOrEmpty(guide.Flow))
                {
                    GUILayout.Space(2f);
                    EditorGUILayout.LabelField("▸ " + guide.Flow, Styles.Flow);
                }

                if (targets.Length == 1)
                {
                    var state = CurrentState;
                    if (!string.IsNullOrEmpty(state))
                    {
                        GUILayout.Space(2f);
                        Body("<b>지금 이 설정은</b> " + state);
                    }
                }

                string lastGroup = null;

                foreach (var item in guide.Items)
                {
                    if (!string.IsNullOrEmpty(item.Group) && item.Group != lastGroup)
                    {
                        GUILayout.Space(6f);
                        EditorGUILayout.LabelField(item.Group, Styles.Group);
                    }

                    lastGroup = item.Group;

                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField(item.Label, Styles.Label);

                    using (new EditorGUI.IndentLevelScope())
                    {
                        Body(item.Text);
                    }
                }
            }
        }

        static void Body(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                EditorGUILayout.LabelField(text, Styles.Body);
            }
        }

        /// <summary>
        /// 한국어 문장은 길어지므로 줄바꿈을 켜고 &lt;b&gt; 강조를 허용한다.
        /// EditorStyles는 첫 OnGUI 전에는 준비되지 않으므로 그때 만든다.
        /// </summary>
        static class Styles
        {
            static GUIStyle s_Body, s_Label, s_Group, s_Flow;

            public static GUIStyle Body => s_Body ?? (s_Body =
                new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true });

            public static GUIStyle Label => s_Label ?? (s_Label =
                new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true,
                    fontStyle = FontStyle.Bold,
                });

            public static GUIStyle Group => s_Group ?? (s_Group =
                new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true, richText = true });

            public static GUIStyle Flow => s_Flow ?? (s_Flow =
                new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true });
        }
    }

    /// <summary>Behavior Parameters. 필드와 모델 검증은 패키지 에디터가 그대로 담당한다.</summary>
    [CustomEditor(typeof(BehaviorParameters))]
    [CanEditMultipleObjects]
    internal sealed class BehaviorParametersGuidedEditor : GuidedEditor
    {
        protected override GuideContent.Component Guide => GuideContent.BehaviorParameters;

        protected override string InnerEditorTypeName =>
            "Unity.MLAgents.Editor.BehaviorParametersEditor";

        protected override string CurrentState
        {
            get
            {
                if (!(target is BehaviorParameters bp))
                {
                    return null;
                }

                var brain = bp.BrainParameters;
                var stacks = Mathf.Max(1, brain.NumStackedVectorObservations);
                var continuous = brain.ActionSpec.NumContinuousActions;
                var branches = brain.ActionSpec.BranchSizes?.Length ?? 0;

                var seeing = stacks > 1
                    ? $"숫자 {brain.VectorObservationSize}개를 {stacks}순간분 겹쳐서 보고"
                    : $"숫자 {brain.VectorObservationSize}개를 보고";

                var doing =
                    continuous > 0 && branches > 0
                        ? $"다이얼 {continuous}개와 선택 묶음 {branches}개로 움직입니다"
                        : continuous > 0 ? $"다이얼 {continuous}개로 움직입니다"
                        : branches > 0 ? $"선택 묶음 {branches}개로 움직입니다"
                        : "아직 할 수 있는 행동이 없습니다";

                return $"{seeing} {doing}.\n{DescribePolicy(bp)}";
            }
        }

        /// <summary>
        /// 지금 설정대로면 누가 행동을 정하는지. GeneratePolicy()의 분기를 따라간다.
        /// Academy.Instance는 건드리지 않는다. 에디터에서 접근하면 실제로 생성되기 때문이다.
        /// </summary>
        static string DescribePolicy(BehaviorParameters bp)
        {
            switch (bp.BehaviorType)
            {
                case BehaviorType.HeuristicOnly:
                    return "조종은 항상 내가 짠 Heuristic() 코드가 합니다(보통 키보드).";

                case BehaviorType.InferenceOnly:
                    return bp.Model == null
                        ? "Inference Only인데 Model이 비어 있어 <b>실행하면 에러가 납니다.</b> " +
                          "Model을 넣거나 Behavior Type을 Default로 바꾸세요."
                        : "조종은 항상 넣어 둔 Model이 합니다.";

                default:
                    if (Application.isPlaying && Academy.IsInitialized)
                    {
                        return "학습이 연결되어 있으면 파이썬 트레이너가 조종합니다.";
                    }

                    return bp.Model != null
                        ? "학습을 돌리지 않을 때는 넣어 둔 Model이 조종합니다."
                        : "트레이너도 Model도 없어 지금은 <b>Heuristic() 코드(보통 키보드)</b>가 조종합니다.";
            }
        }

        /// <summary>
        /// 패키지 에디터를 못 찾은 비상시에만 쓰인다. 이 컴포넌트의 필드는 모두
        /// [HideInInspector]라 기본 인스펙터로는 아무것도 보이지 않아 직접 나열한다.
        /// </summary>
        protected override void DrawFallback()
        {
            var so = serializedObject;
            so.Update();

            foreach (var path in new[]
                     {
                         "m_BehaviorName", "m_BrainParameters", "m_Model", "m_InferenceDevice",
                         "m_DeterministicInference", "m_BehaviorType", "TeamId",
                         "m_UseChildSensors", "m_ObservableAttributeHandling",
                     })
            {
                var property = so.FindProperty(path);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            so.ApplyModifiedProperties();
        }
    }

    /// <summary>Decision Requester. 패키지에 전용 에디터가 없어 기본 인스펙터를 그대로 쓴다.</summary>
    [CustomEditor(typeof(DecisionRequester), true)]
    [CanEditMultipleObjects]
    internal sealed class DecisionRequesterGuidedEditor : GuidedEditor
    {
        protected override GuideContent.Component Guide => GuideContent.DecisionRequester;

        protected override string CurrentState
        {
            get
            {
                if (!(target is DecisionRequester requester))
                {
                    return null;
                }

                var seconds = requester.DecisionPeriod * Time.fixedDeltaTime;
                var text =
                    $"{requester.DecisionPeriod}번에 한 번, 약 <b>{seconds:0.###}초마다</b> 판단합니다.";

                if (!requester.TakeActionsBetweenDecisions && requester.DecisionPeriod > 1)
                {
                    text += " 판단하지 않는 사이에는 행동이 걸리지 않아 움직임이 끊깁니다.";
                }

                if (requester.DecisionStep >= requester.DecisionPeriod)
                {
                    text += " Decision Step이 Decision Period보다 작지 않아 " +
                            "<b>실행하면 경고가 납니다.</b>";
                }

                return text;
            }
        }
    }
}
