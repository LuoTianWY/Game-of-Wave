
using System;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Room;

namespace WaveTeam.UI
{
    public static class RehearsalResultPanel
    {
        public static GameObject Show(
            Transform parent,
            RehearsalScoreResult result,
            Action onRetry,
            Action onExit)
        {
            // 整屏半透明遮罩
            var overlay = UIFactory.CreatePanel(
                "RehearsalResultOverlay",
                parent,
                new Color(0f, 0f, 0f, 0.75f)
            );

            UIFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            overlay.transform.SetAsLastSibling();

            // 中央结算卡片
            var panel = UIFactory.CreatePanel(
                "ResultCard",
                overlay.transform,
                new Color(0.17f, 0.13f, 0.11f, 0.98f)
            );

            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 660f);
            rt.anchoredPosition = Vector2.zero;

            int completed = 0;
            if (result.OpeningStableGood) completed++;
            if (result.DevelopmentLayeredGood) completed++;
            if (result.DensityGood) completed++;

            string grade = completed == 3
                ? "完美排练"
                : completed == 2
                    ? "排练通过"
                    : "继续努力";

            AddText(panel.transform, "Title",
                "排练结算", 36, -35f, 60f,
                TextAnchor.MiddleCenter);

            AddText(panel.transform, "Score",
                grade + "    " + result.Score + " / 100",
                34, -115f, 65f,
                TextAnchor.MiddleCenter);

            string feedback =
                "开场稳定：" +
                (result.OpeningStableGood ? "达成  +33" : "未达成  +0") +
                "\n\n发展层次：" +
                (result.DevelopmentLayeredGood ? "达成  +33" : "未达成  +0") +
                "\n\n整体密度：" +
                (result.DensityGood ? "达成  +34" : "未达成  +0");

            AddText(panel.transform, "Feedback",
                feedback, 26, -215f, 185f,
                TextAnchor.UpperLeft);

            // 根据未完成目标给玩家改进建议
            string advice = "三个排练目标全部完成！";

            if (!result.OpeningStableGood)
                advice = "建议：在开场放入稳定度更高的角色。";
            else if (!result.DevelopmentLayeredGood)
                advice = "建议：在发展段放入层次更丰富的角色。";
            else if (!result.DensityGood)
                advice = "建议：减少高密度角色的使用。";

            AddText(panel.transform, "Advice",
                advice, 23, -430f, 70f,
                TextAnchor.MiddleLeft);

            string progress = completed >= 2
                ? "已达到本轮排练通关标准。"
                : "至少完成两个目标才能通过排练。";

            AddText(panel.transform, "Progress",
                progress, 22, -520f, 50f,
                TextAnchor.MiddleCenter);

            // 返回编排：不清空音轨
            var retry = UIFactory.CreatePixelButton(
                "RetryButton",
                panel.transform,
                "重新编排",
                1.8f,
                UIStyle.Grey,
                UIStyle.Text,
                () =>
                {
                    UnityEngine.Object.Destroy(overlay.gameObject);
                    if (onRetry != null) onRetry();
                }
            );

            var retryRt = (RectTransform)retry.transform;
            retryRt.anchorMin = retryRt.anchorMax = retryRt.pivot =
                new Vector2(0.5f, 0f);
            retryRt.sizeDelta = new Vector2(280f, 64f);
            retryRt.anchoredPosition = new Vector2(-170f, 40f);

            // 退出结算，返回排练室
            var exit = UIFactory.CreatePixelButton(
                "ExitResultButton",
                panel.transform,
                "返回排练室",
                1.8f,
                UIStyle.Accent,
                UIStyle.Ink,
                () =>
                {
                    UnityEngine.Object.Destroy(overlay.gameObject);
                    if (onExit != null) onExit();
                }
            );

            var exitRt = (RectTransform)exit.transform;
            exitRt.anchorMin = exitRt.anchorMax = exitRt.pivot =
                new Vector2(0.5f, 0f);
            exitRt.sizeDelta = new Vector2(280f, 64f);
            exitRt.anchoredPosition = new Vector2(170f, 40f);

            return overlay.gameObject;
        }

        private static void AddText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            float topOffset,
            float height,
            TextAnchor alignment)
        {
            var label = UIFactory.CreateText(
                name,
                parent,
                content,
                fontSize,
                UIStyle.Text,
                alignment
            );

            label.raycastTarget = false;

            var rt = (RectTransform)label.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0.5f, 1f);

            rt.sizeDelta = new Vector2(790f, height);
            rt.anchoredPosition = new Vector2(0f, topOffset);
        }
    }
}
