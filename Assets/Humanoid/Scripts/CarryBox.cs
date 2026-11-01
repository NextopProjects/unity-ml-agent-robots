using UnityEngine;

namespace Humanoid
{
    // 옮길 상자. 어느 아래팔이 닿아 있는지 기록하고, 상태를 색으로 보여 준다.
    // 붙잡는 코드는 없다 — 양팔이 조여서 생긴 마찰만으로 들린다. 여기서는 닿았는지만 본다.
    [RequireComponent(typeof(Rigidbody))]
    public class CarryBox : MonoBehaviour
    {
        public Color idle = new Color(0.95f, 0.6f, 0.2f), grasped = new Color(0.95f, 0.9f, 0.2f), lifted = new Color(0.3f, 0.85f, 0.4f);

        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public bool touchL, touchR;     // 왼 · 오른 아래팔이 닿아 있나

        Renderer _renderer;
        MaterialPropertyBlock _colors;

        public bool Grasped => touchL && touchR;          // 양쪽에서 조이고 있다

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<Renderer>();
            _colors = new MaterialPropertyBlock();
        }

        public void ResetContacts() => touchL = touchR = false;

        void OnCollisionStay(Collision c) => Touch(c, true);
        void OnCollisionExit(Collision c) => Touch(c, false);

        void Touch(Collision c, bool down)
        {
            if (c.collider.name == "ForearmL") touchL = down;
            else if (c.collider.name == "ForearmR") touchR = down;
        }

        // 화면 표시: 그대로 주황 · 잡음 노랑 · 들어 올림 초록. 학습 중에도 한눈에 보이게
        public void Show(bool isLifted)
        {
            if (_renderer == null) return;
            _colors.SetColor("_BaseColor", isLifted ? lifted : Grasped ? grasped : idle);
            _colors.SetColor("_Color", isLifted ? lifted : Grasped ? grasped : idle);
            _renderer.SetPropertyBlock(_colors);
        }
    }
}
