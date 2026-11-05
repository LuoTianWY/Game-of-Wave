using UnityEditor;
using UnityEngine;
using WaveTeam.UI;

namespace WaveTeam.EditorTools
{
    public static class OpenMelodyLab
    {
        [MenuItem("波形小队/打开旋律测试台（Play Mode）")]
        private static void Open()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Melody Lab] 先进入 Play Mode 并返回排练室。");
                return;
            }
            MelodyLab.Open();
        }
    }
}
