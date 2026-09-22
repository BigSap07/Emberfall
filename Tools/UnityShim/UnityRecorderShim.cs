// OFFLINE TYPE-CHECK SHIM - NOT PART OF THE UNITY PROJECT. See UnityCore.cs.
//
// The slice of com.unity.recorder (5.1.7, Packages/manifest.json) that TeaserCapture drives.
// Signatures follow the package's own Unity.Recorder.Editor.api; bodies do nothing.
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Recorder
{
    public enum FrameRatePlayback { Constant = 0, Variable = 1 }

    public static class RecorderOptions
    {
        public static bool ShowRecorderGameObject { get; set; }
        public static bool VerboseMode { get; set; }
    }

    public abstract class RecorderSettings : ScriptableObject
    {
        public bool Enabled { get; set; }
        public string OutputFile { get; set; }
        public float FrameRate { get; set; }
        public bool CapFrameRate { get; set; }
    }

    public class MovieRecorderSettings : RecorderSettings
    {
        public bool CaptureAlpha { get; set; }
        public bool CaptureAudio { get; set; }
        public Encoder.IEncoderSettings EncoderSettings { get; set; }
        public Input.ImageInputSettings ImageInputSettings { get; set; }
    }

    public class RecorderControllerSettings : ScriptableObject
    {
        public bool CapFrameRate { get; set; }
        public bool ExitPlayMode { get; set; }
        public float FrameRate { get; set; }
        public FrameRatePlayback FrameRatePlayback { get; set; }
        public IEnumerable<RecorderSettings> RecorderSettings { get { return new List<RecorderSettings>(); } }
        public void AddRecorderSettings(RecorderSettings recorder) { }
        public void SetRecordModeToManual() { }
        public void SetRecordModeToFrameInterval(int startFrame, int endFrame) { }
    }

    public class RecorderController
    {
        public RecorderController(RecorderControllerSettings settings) { }
        public RecorderControllerSettings Settings { get { return null; } }
        public bool IsRecording() { return false; }
        public void PrepareRecording() { }
        public bool StartRecording() { return false; }
        public void StopRecording() { }
    }
}

namespace UnityEditor.Recorder.Encoder
{
    public interface IEncoderSettings { }

    public sealed class CoreEncoderSettings : IEncoderSettings
    {
        public enum OutputCodec { MP4 = 0, WEBM = 1 }
        public enum H264EncodingProfile { Baseline = 0, Main = 1, High = 2 }
        public enum VideoEncodingQuality { Low = 0, Medium = 1, High = 2, Custom = 3 }

        public OutputCodec Codec { get; set; }
        public H264EncodingProfile EncodingProfile { get; set; }
        public VideoEncodingQuality EncodingQuality { get; set; }
        public uint GopSize { get; set; }
        public uint KeyframeDistance { get; set; }
        public uint NumConsecutiveBFrames { get; set; }
        public float TargetBitRate { get; set; }
    }
}

namespace UnityEditor.Recorder.Input
{
    public abstract class RecorderInputSettings { }

    public abstract class ImageInputSettings : RecorderInputSettings
    {
        public abstract int OutputWidth { get; set; }
        public abstract int OutputHeight { get; set; }
    }

    public class GameViewInputSettings : ImageInputSettings
    {
        public override int OutputWidth { get; set; }
        public override int OutputHeight { get; set; }
    }
}
