using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Vẽ hình nón tầm nhìn trên sàn (mesh hình quạt), bị cắt bởi tường bằng raycast.
    /// Màu đổi theo trạng thái: trắng (tuần tra) → vàng (nghi ngờ) → đỏ (truy đuổi).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionCone : MonoBehaviour
    {
        public int segments = 24;
        public float height = 0.04f;
        public float rayHeight = 0.6f;
        public LayerMask obstacleMask = ~0;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_Color = Shader.PropertyToID("_Color");

        Mesh m_Mesh;
        Vector3[] m_Verts;
        int[] m_Tris;
        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        float m_Range = 6f, m_Angle = 90f;
        Color m_Color = new(1f, 1f, 1f, 0.12f);
        int m_Frame;

        void Awake()
        {
            m_Mesh = new Mesh { name = "VisionCone" };
            m_Mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
            m_Renderer = GetComponent<MeshRenderer>();
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            m_Block = new MaterialPropertyBlock();
            m_Verts = new Vector3[segments + 2];
            m_Tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                m_Tris[i * 3] = 0;
                m_Tris[i * 3 + 1] = i + 1;
                m_Tris[i * 3 + 2] = i + 2;
            }
        }

        public void Configure(float range, float angle)
        {
            m_Range = range;
            m_Angle = angle;
        }

        public void SetColor(Color c) => m_Color = c;

        void LateUpdate()
        {
            // Cập nhật mesh cách frame để tiết kiệm raycast
            if ((++m_Frame & 1) == 0) Rebuild();

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, m_Color);
            m_Block.SetColor(k_Color, m_Color);
            m_Renderer.SetPropertyBlock(m_Block);
        }

        void Rebuild()
        {
            Transform t = transform;
            Vector3 origin = t.position + Vector3.up * rayHeight;
            m_Verts[0] = new Vector3(0f, height, 0f);
            float half = m_Angle * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float a = -half + m_Angle * i / segments;
                Vector3 localDir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Vector3 worldDir = t.TransformDirection(localDir);
                worldDir.y = 0f;
                worldDir.Normalize();
                float dist = m_Range;
                if (Physics.Raycast(origin, worldDir, out var hit, m_Range, obstacleMask, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(t.root) && hit.collider.GetComponentInParent<PlayerStats>() == null)
                    dist = hit.distance;
                m_Verts[i + 1] = localDir * (dist / Mathf.Max(0.001f, t.lossyScale.x)) + Vector3.up * height;
            }

            m_Mesh.vertices = m_Verts;
            m_Mesh.triangles = m_Tris;
            m_Mesh.RecalculateBounds();
        }
    }
}
