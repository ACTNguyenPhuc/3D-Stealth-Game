using UnityEngine;

namespace StealthGame
{
    /// <summary>Camera nền menu chính: bay chậm quanh dinh thự.</summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        public Vector3 center = new(-2f, 0f, 2f);
        public float radius = 16f;
        public float height = 11f;
        public float speed = 4f;   // độ / giây

        float m_Angle = 200f;

        void Update()
        {
            m_Angle += speed * Time.unscaledDeltaTime;
            float rad = m_Angle * Mathf.Deg2Rad;
            transform.position = center + new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);
            transform.LookAt(center + Vector3.up * 0.5f);
        }
    }
}
