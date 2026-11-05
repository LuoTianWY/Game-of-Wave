using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Room;

namespace WaveTeam.UI
{
    /// <summary>排练室关卡衔接，沿用像素边框和现有按钮工厂。</summary>
    internal static class RehearsalStoryPanel
    {
        private static readonly (string Speaker, string Line)[] Dialogue =
        {
            ("阿宁", "这次排练效果不错。我们终于开始像一支真正的乐队了。"),
            ("小川", "我刚刚找到一份以前留下的录音。不过里面好像缺了几段声音。"),
            ("石头", "底鼓还在，但军鼓和踩镲都有一部分不见了。如果能找到原来的节奏，应该还能修好。"),
            ("小川", "我们一起试试吧。这次不是创作一首新曲子，而是找回一段被遗忘的音乐。")
        };

        public static GameObject ShowIntroduction(Transform parent, Action onComplete, Action onClose)
        {
            return ShowSequence(parent, Dialogue, "NEW QUEST UNLOCKED", RehearsalChallenge.MissingBeat.Title,
                "前往架子鼓前的排练设备，选择旧录音开始修复。", onComplete, onClose);
        }

        public static GameObject ShowSequence(Transform parent, (string Speaker, string Line)[] dialogue,
            string completionHeading, string completionTitle, string completionBody, Action onComplete, Action onClose,
            string completionButton = "返回排练室")
        {
            var overlay = CreateOverlay("MissingBeatIntroduction", parent, out var card);
            var heading = AddHeading(card, "乐队对话  1 / " + dialogue.Length);
            var speaker = AddTitle(card, dialogue[0].Speaker);
            var body = AddBody(card, dialogue[0].Line);
            var close = AddButton(card, "Close", "稍后", -170f, UIStyle.Grey, onClose);
            int page = 0;
            Text nextLabel = null;
            var next = AddButton(card, "Continue", "继续", 170f, UIStyle.Accent, () =>
            {
                if (page == dialogue.Length)
                {
                    onClose();
                    return;
                }

                page++;
                if (page == dialogue.Length)
                {
                    onComplete();
                    heading.text = completionHeading;
                    speaker.text = completionTitle;
                    body.text = completionBody;
                    nextLabel.text = completionButton;
                    close.gameObject.SetActive(false);
                    return;
                }

                heading.text = "乐队对话  " + (page + 1) + " / " + dialogue.Length;
                speaker.text = dialogue[page].Speaker;
                body.text = dialogue[page].Line;
                if (page == dialogue.Length - 1) nextLabel.text = "继续";
            });
            nextLabel = next.GetComponentInChildren<Text>();
            return overlay;
        }

        public static GameObject ShowMenu(Transform parent, string title, string description,
            IReadOnlyList<(string Label, bool Enabled, Action Click)> options, Action onClose)
        {
            var overlay = CreateOverlay("EquipmentMenu", parent, out var card);
            ((RectTransform)card).sizeDelta = new Vector2(940f, 540f);
            AddHeading(card, "排练室 · 音乐工作台");
            AddTitle(card, title);
            AddText(card, "Description", description, 21, UIStyle.TextDim, -118f, 45f);
            Transform buttonsParent = card;
            float firstY = -175f;
            if (options.Count > 4)
            {
                var viewport = UIFactory.CreatePanel("MenuViewport", card, Color.clear).rectTransform;
                UIFactory.Place(viewport, new Vector2(0.5f, 1f), new Vector2(840f, 255f), new Vector2(0f, -170f));
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = UIFactory.CreateRect("Options", viewport);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(0f, options.Count * 58f);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.scrollSensitivity = 32f;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                buttonsParent = content;
                firstY = 0f;
            }
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = AddButton(buttonsParent, "Option_" + i, option.Label, 0f, UIStyle.Grey, option.Click);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f),
                    new Vector2(800f, 52f), new Vector2(0f, firstY - i * 58f));
                button.interactable = option.Enabled;
            }
            AddButton(card, "Close", "返回", 0f, UIStyle.Accent, onClose);
            return overlay;
        }

        public static GameObject ShowSaveSong(Transform parent, Action<string> save, Action close)
        {
            var overlay = CreateOverlay("SaveSong", parent, out var card);
            AddHeading(card, "作品合格 · 命名并保存");
            AddTitle(card, "Our Own Sound");
            AddText(card, "SaveFeedback", "保存后可以在排练设备的曲库重载和试听。", 22, UIStyle.TextDim, -125f, 45f);
            var inputImage = UIFactory.CreatePanel("SongName", card, UIStyle.Cell);
            UIFactory.Place(inputImage.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(800f, 58f), Vector2.zero);
            var input = inputImage.gameObject.AddComponent<InputField>();
            var text = UIFactory.CreateText("Text", input.transform, "", 24, UIStyle.Text);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14f, 4f);
            text.rectTransform.offsetMax = new Vector2(-14f, -4f);
            input.textComponent = text;
            input.characterLimit = 48;
            input.text = "A Song We Made Together";
            AddButton(card, "Close", "返回", -170f, UIStyle.Grey, close);
            AddButton(card, "Save", "保存作品", 170f, UIStyle.Accent, () => save(input.text));
            return overlay;
        }

        public static GameObject ShowBriefing(Transform parent, Action onStart, Action onClose)
        {
            var overlay = CreateOverlay("MissingBeatBriefing", parent, out var card);
            AddHeading(card, "关卡介绍");
            AddTitle(card, RehearsalChallenge.MissingBeat.Title);
            AddBody(card, "丢失的节拍\n\n这段录音缺失了两段节奏：军鼓和踩镲。\n试听已有片段，找到能补全两个缺口的波形。");
            AddButton(card, "Close", "返回", -170f, UIStyle.Grey, onClose);
            AddButton(card, "StartRepair", "开始修复", 170f, UIStyle.Accent, onStart);
            return overlay;
        }

        private static GameObject CreateOverlay(string name, Transform parent, out Transform card)
        {
            var overlay = UIFactory.CreatePanel(name, parent, new Color(0f, 0f, 0f, 0.75f));
            UIFactory.Stretch(overlay.rectTransform);
            var panel = UIFactory.CreatePanel("StoryCard", overlay.transform, UIStyle.BgDeep);
            UIStyle.ApplyFrame(panel, UIStyle.BgDeep);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(940f, 400f), Vector2.zero);
            card = panel.transform;
            return overlay.gameObject;
        }

        private static Text AddHeading(Transform parent, string text)
        {
            return AddText(parent, "Heading", text, 20, UIStyle.Accent, -25f, 34f);
        }

        private static Text AddTitle(Transform parent, string text)
        {
            return AddText(parent, "SpeakerName", text, 30, UIStyle.Text, -67f, 45f);
        }

        private static Text AddBody(Transform parent, string text)
        {
            return AddText(parent, "Dialogue", text, 24, UIStyle.Text, -130f, 170f);
        }

        private static Text AddText(Transform parent, string name, string text, int size, Color color, float y, float height)
        {
            var label = UIFactory.CreateText(name, parent, text, size, color, TextAnchor.UpperLeft);
            label.raycastTarget = false;
            UIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(840f, height), new Vector2(50f, y));
            return label;
        }

        private static Button AddButton(Transform parent, string name, string label, float x, Color fill, Action click)
        {
            var button = UIFactory.CreateButton(name, parent, label, click);
            UIStyle.ApplyFrame(button.image, fill);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = 22;
            text.color = fill == UIStyle.Accent ? UIStyle.Ink : UIStyle.Text;
            text.raycastTarget = false;
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f),
                new Vector2(230f, 55f), new Vector2(x, 34f));
            return button;
        }
    }
}
