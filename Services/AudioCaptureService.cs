using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Speechify.Services;

public class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly int _sampleRate = 24000;
    private readonly int _channels = 1;
    private readonly int _bitsPerSample = 16;
    private bool _isRecording = false;
    
    public event EventHandler<byte[]>? AudioDataAvailable;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? RecordingStarted;
    public event EventHandler? RecordingStopped;
    
    public bool IsRecording => _isRecording;

    public AudioCaptureService()
    {
        InitializeAudioCapture();
    }

    private void InitializeAudioCapture()
    {
        try
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channels),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Audio initialization error: {ex.Message}");
        }
    }

    public void StartRecording()
    {
        try
        {
            if (_waveIn != null && !_isRecording)
            {
                _waveIn.StartRecording();
                _isRecording = true;
                RecordingStarted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _isRecording = false;
            ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
        }
    }

    public void StopRecording()
    {
        try
        {
            if (_waveIn != null && _isRecording)
            {
                _waveIn.StopRecording();
                _isRecording = false;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to stop recording: {ex.Message}");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);
            AudioDataAvailable?.Invoke(this, audioData);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isRecording = false;
        RecordingStopped?.Invoke(this, EventArgs.Empty);
        
        if (e.Exception != null)
        {
            ErrorOccurred?.Invoke(this, $"Recording stopped with error: {e.Exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_isRecording)
        {
            StopRecording();
        }
        
        _waveIn?.Dispose();
    }
}