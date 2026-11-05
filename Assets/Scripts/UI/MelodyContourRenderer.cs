using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>音符横条的 X/宽度对应时间，Y 对应 MIDI 音高。与父片段一起缩放。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MelodyContourRenderer : MaskableGraphic
    {
        private IReadOnlyList<MelodyNote> _notes;
        private float _duration;

        public void SetNotes(IReadOnlyList<MelodyNote> notes, float duration)
        {
            _notes = notes;
            _duration = duration;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (_notes == null || _notes.Count == 0 || _duration <= 0f) return;
            var rect = GetPixelAdjustedRect();
            int low = 127, high = 0;
            foreach (var note in _notes)
            {
                low = Mathf.Min(low, note.MidiNote);
                high = Mathf.Max(high, note.MidiNote);
            }
            low -= 3;
            high += 3;
            foreach (var note in _notes)
            {
                float x = rect.xMin + note.StartBeat / _duration * rect.width;
                float width = Mathf.Max(1f, note.DurationBeats / _duration * rect.width - 3f);
                float y = Mathf.Lerp(rect.yMin + 8f, rect.yMax - 8f, (note.MidiNote - low) / (float)(high - low));
                int first = vertices.currentVertCount;
                var tint = color;
                tint.a *= Mathf.Lerp(0.3f, 1f, note.Velocity);
                vertices.AddVert(new Vector3(x, y - 3f), tint, Vector2.zero);
                vertices.AddVert(new Vector3(x + width, y - 3f), tint, Vector2.zero);
                vertices.AddVert(new Vector3(x + width, y + 3f), tint, Vector2.zero);
                vertices.AddVert(new Vector3(x, y + 3f), tint, Vector2.zero);
                vertices.AddTriangle(first, first + 1, first + 2);
                vertices.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
