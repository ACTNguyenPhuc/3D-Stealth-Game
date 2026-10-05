using System;
using UnityEngine;

namespace StealthGame
{
    /// <summary>Phát "tiếng động" để AI nghe thấy (bước chân, tiếp đất, đòn tấn công...).</summary>
    public static class NoiseSystem
    {
        public static event Action<Vector3, float> NoiseEmitted;

        public static void Emit(Vector3 position, float radius)
        {
            if (radius > 0.05f) NoiseEmitted?.Invoke(position, radius);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => NoiseEmitted = null;
    }
}
