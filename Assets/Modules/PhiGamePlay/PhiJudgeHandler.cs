﻿using System;
using Klrohias.NFast.PhiChartLoader;
using Klrohias.NFast.UIComponent;
using Klrohias.NFast.Utilities;
using UnityEngine;

namespace Klrohias.NFast.PhiGamePlay
{
    public class PhiJudgeHandler : MonoBehaviour
    {
        public PhiGamePlayer Player;
        public ScreenAdapter ScreenAdapter;

        public float PerfectJudgeRange = 80f;
        public float GoodJudgeRange = 150f;
        public float BadJudgeRange = 350f;
        public bool Autoplay = false;

        private UnorderedList<PhiNote> _judgeNotes;

        private readonly UnorderedList<TouchDetail> _touches = new UnorderedList<TouchDetail>();
        private readonly UnorderedList<Tuple<PhiNote, PhiGamePlayer.JudgeResult>> _judgingHoldNotes = new();
        private int _touchCount = 0; // update each frame
        private float _currentTime = 0f;
        private const float NOTE_WIDTH = 2.5f * 0.88f;
        private class TouchDetail
        {
            public Touch RawTouch;
            public float[] LandDistances = null;
        }
        private void Start()
        {
            _judgeNotes = Player.JudgeNotes;
        }
        private void Update()
        {
            if (!Player.GameRunning) return;

            // update touch detail
            _touchCount = Input.touchCount;
            UpdateTouchDetails();


            _currentTime = Player.Timer.Time;
            ProcessJudgeNotes();

            UpdateHoldNotes();
        }

        private void UpdateHoldNotes()
        {
            for (var i = 0; i < _judgingHoldNotes.Length; i++)
            {
                var (note, judgeResult) = _judgingHoldNotes[i];
                if (note.EndTime <= Player.CurrentBeats)
                {
                    PutJudgeResult(note, judgeResult);
                    _judgingHoldNotes.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!UpdateHoldNote(note))
                {
                    PutJudgeResult(note, PhiGamePlayer.JudgeResult.Miss);
                    _judgingHoldNotes.RemoveAt(i);
                    i--;
                }
            }
        }

        private bool UpdateHoldNote(PhiNote note)
        {
            var lineId = note.LineId;
            for (var j = 0; j < _touchCount; j++)
            {
                var touch = _touches[j];

                if (MathF.Abs(ScreenAdapter.ToGameXPos(note.XPosition) -
                              touch.LandDistances[lineId]) > NOTE_WIDTH / 1.75f) continue;

                return true;
            }

            return false;
        }

        private static Vector2 GetLandPos(Vector2 lineOrigin, float rotation, Vector2 touchPos)
        {
            if (rotation % MathF.PI == 0f) return new Vector2(touchPos.x, lineOrigin.y);
            var k = MathF.Tan(rotation);
            var b = lineOrigin.y - k * lineOrigin.x;
            var k2 = -1 / k;
            var b2 = touchPos.y - k2 * touchPos.x;
            var x = (b2 - b) / (k - k2);
            var y = k * x + b;
            return new Vector2(x, y);
        }
        private void UpdateTouchDetails()
        {
            for (int i = 0; i < _touchCount; i++)
            {
                if (_touches.Length <= i)
                {
                    _touches.Add(new TouchDetail
                    {
                        LandDistances = new float[Player.Lines.Count]
                    });
                }

                var item = _touches[i];
                item.RawTouch = Input.GetTouch(i);
                UpdateLandDistances(i);
            }
        }
        private void UpdateLandDistances(int touchIndex)
        {
            var lines = Player.Lines;
            var touchDetail = _touches[touchIndex];
            var worldPos = Camera.main.ScreenToWorldPoint(touchDetail.RawTouch.position);

            for (var index = 0; index < lines.Count; index++)
            {
                var chartLine = lines[index];
                var linePos = Player.LineObjects[(int)chartLine.LineId].transform.position;
                var landPos = Vector2.Distance(GetLandPos(linePos, chartLine.Rotation, worldPos), linePos);
                touchDetail.LandDistances[index] = landPos;
            }
        }

        private void PutJudgeResult(PhiNote note, PhiGamePlayer.JudgeResult result)
        {
            Player.JudgeNotes.Remove(note);
            Debug.Log($"note judge {result}");
        }

        private void ProcessDragNote(PhiNote note)
        {
            if (note.JudgeTime > _currentTime) return;
            var lineId = note.LineId;
            for (var i = 0; i < _touchCount; i++)
            {
                var touch = _touches[i];
                if (MathF.Abs(ScreenAdapter.ToGameXPos(note.XPosition) -
                                touch.LandDistances[lineId]) > NOTE_WIDTH / 1.75f) continue;

                PutJudgeResult(note, PhiGamePlayer.JudgeResult.Perfect);
                break;
            }
        }

        private void ProcessTapNote(PhiNote note)
        {
            var lineId = note.LineId;
            for (var i = 0; i < _touchCount; i++)
            {
                var touch = _touches[i];
                if (touch.RawTouch.phase != TouchPhase.Began) continue;

                if (MathF.Abs(ScreenAdapter.ToGameXPos(note.XPosition) -
                              touch.LandDistances[lineId]) > NOTE_WIDTH / 1.75f) continue;
                
                var range = MathF.Abs(_currentTime - note.JudgeTime);
                if (range < PerfectJudgeRange) PutJudgeResult(note, PhiGamePlayer.JudgeResult.Perfect);
                else if (range < GoodJudgeRange) PutJudgeResult(note, PhiGamePlayer.JudgeResult.Good);
                else if (range < BadJudgeRange) PutJudgeResult(note, PhiGamePlayer.JudgeResult.Bad);
                break;
            }
        }

        private void ProcessFlickNote(PhiNote note)
        {
            var lineId = note.LineId;
            for (var j = 0; j < _touchCount; j++)
            {
                var touch = _touches[j];
                if (touch.RawTouch.phase != TouchPhase.Moved) continue;

                if (MathF.Abs(ScreenAdapter.ToGameXPos(note.XPosition) -
                              touch.LandDistances[lineId]) > NOTE_WIDTH / 1.75f) continue;

                PutJudgeResult(note, PhiGamePlayer.JudgeResult.Perfect);
                break;
            }
        }

        private void ProcessHoldNote(PhiNote note)
        {
            var lineId = note.LineId;
            for (var j = 0; j < _touchCount; j++)
            {
                var touch = _touches[j];

                if (MathF.Abs(ScreenAdapter.ToGameXPos(note.XPosition) -
                              touch.LandDistances[lineId]) > NOTE_WIDTH / 1.75f) continue;

                var range = MathF.Abs(_currentTime - note.JudgeTime);
                var judgeResult = PhiGamePlayer.JudgeResult.Miss;
                if (range < PerfectJudgeRange) judgeResult = PhiGamePlayer.JudgeResult.Perfect;
                else if (range < GoodJudgeRange) judgeResult = PhiGamePlayer.JudgeResult.Good;

                if (judgeResult == PhiGamePlayer.JudgeResult.Miss) break;

                _judgingHoldNotes.Add(new Tuple<PhiNote, PhiGamePlayer.JudgeResult>(note, PhiGamePlayer.JudgeResult.Miss));
                Player.JudgeNotes.Remove(note);
                break;
            }
        }

        private void ProcessJudgeNote(PhiNote note)
        {
            if (Autoplay && note.JudgeTime <= _currentTime)
            {
                PutJudgeResult(note, PhiGamePlayer.JudgeResult.Perfect);
                return;
            }

            switch (note.Type)
            {
                case NoteType.Tap:
                    ProcessTapNote(note);
                    break;
                case NoteType.Flick:
                    ProcessFlickNote(note);
                    break;
                case NoteType.Hold:
                    ProcessHoldNote(note);
                    break;
                case NoteType.Drag:
                    ProcessDragNote(note);
                    break;
            }
        }

        private void ProcessJudgeNotes()
        {
            for (var i = 0; i < Player.JudgeNotes.Length; i++)
            {
                var item = _judgeNotes[i];

                if (item.JudgeTime - _currentTime > BadJudgeRange) continue;
                if (_currentTime - item.JudgeTime > BadJudgeRange)
                {
                    PutJudgeResult(item, PhiGamePlayer.JudgeResult.Miss);
                    _judgeNotes.RemoveAt(i);
                    i--;
                    continue;
                }

                ProcessJudgeNote(item);
            }
        }
    }
}