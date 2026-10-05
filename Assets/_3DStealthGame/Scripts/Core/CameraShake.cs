using Unity.Cinemachine;
using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Rung camera bằng Cinemachine Impulse. Mọi camera ảo có CinemachineImpulseListener
    /// (CameraModeManager tự gắn) sẽ nhận rung. Tôn trọng tùy chọn tắt rung trong Cài đặt.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        static CameraShake s_Instance;
        CinemachineImpulseSource m_Source;

        public static void Shake(float strength)
        {
            if (!GameSettings.CameraShake || strength <= 0f) return;
            if (s_Instance == null)
            {
                var go = new GameObject("[CameraShake]");
                s_Instance = go.AddComponent<CameraShake>();
                s_Instance.m_Source = go.AddComponent<CinemachineImpulseSource>();
                var def = s_Instance.m_Source.ImpulseDefinition;
                def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
                def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
                def.ImpulseDuration = 0.3f;
            }
            var v = Random.insideUnitSphere;
            v.z *= 0.3f;
            s_Instance.m_Source.GenerateImpulseWithVelocity(v.normalized * strength * 0.25f);
        }
    }
}
