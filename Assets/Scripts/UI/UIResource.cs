using UnityEngine;
using UnityEngine.UI;

namespace WaveTeam.UI
{
    /// <summary>
    /// 素材接口（C15）：从 Resources 按路径加载美术贴图；加载失败由调用方回退到程序化占位色。
    /// 约定：把 PNG 放到 Assets/Resources/ 下对应路径即可被自动拾取（路径见游玩界面需求表 A1-A14）。
    /// </summary>
    public static class UIResource
    {
        public static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return Resources.Load<Sprite>(path);
        }

        /// <summary>尝试给 Image 换贴图；成功返回 true（没有素材时保持原样）。</summary>
        public static bool TryApply(Image img, string path)
        {
            if (img == null) return false;
            var s = Load(path);
            if (s == null) return false;
            img.sprite = s;
            return true;
        }

        /// <summary>有贴图 → 用贴图（不加色）；无贴图 → 用 fallback 占位色。</summary>
        public static void ApplySpriteOrColor(Image img, string path, Color fallback)
        {
            if (img == null) return;
            if (TryApply(img, path))
            {
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
            }
        }
    }
}
