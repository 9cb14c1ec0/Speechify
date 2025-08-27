using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Speechify.Services;

namespace Speechify;

public partial class MainWindow : Window
{
    private OpenAIRealtimeService? _openAIService;
    private AudioCaptureService? _audioService;
    private readonly ConfigurationService _configService;
    private readonly DispatcherTimer _audioLevelTimer;
    private readonly Queue<byte[]> _audioBuffer = new();
    private bool _isStreaming = false;

    public MainWindow()
    {
        InitializeComponent();
        _configService = new ConfigurationService();
        
        _audioLevelTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _audioLevelTimer.Tick += UpdateAudioLevel;
        
        Closing += MainWindow_Closing;
        
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        var savedApiKey = _configService.GetApiKey();
        if (!string.IsNullOrWhiteSpace(savedApiKey))
        {
            ApiKeyTextBox.Text = savedApiKey;
            ApiKeyTextBox.Foreground = new SolidColorBrush(Colors.Black);
        }
        else
        {
            SetupPlaceholderText();
        }
    }

    private void SetupPlaceholderText()
    {
        ApiKeyTextBox.Text = ApiKeyTextBox.Tag?.ToString();
        ApiKeyTextBox.Foreground = new SolidColorBrush(Colors.Gray);
    }

    private void ApiKeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (ApiKeyTextBox.Text == ApiKeyTextBox.Tag?.ToString())
        {
            ApiKeyTextBox.Text = "";
            ApiKeyTextBox.Foreground = new SolidColorBrush(Colors.Black);
        }
    }

    private void ApiKeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyTextBox.Text))
        {
            SetupPlaceholderText();
        }
    }

    private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var apiKey = ApiKeyTextBox.Text;
        if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != ApiKeyTextBox.Tag?.ToString())
        {
            try
            {
                _configService.SaveApiKey(apiKey);
            }
            catch
            {
                // Silently fail - we'll save again on successful connection
            }
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_openAIService?.IsConnected == true)
        {
            await DisconnectServices();
        }
        else
        {
            await ConnectServices();
        }
    }

    private async Task ConnectServices()
    {
        try
        {
            var apiKey = ApiKeyTextBox.Text;
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == ApiKeyTextBox.Tag?.ToString())
            {
                ShowError("Please enter a valid OpenAI API key");
                return;
            }

            ConnectButton.IsEnabled = false;
            ConnectButton.Content = "Connecting...";
            
            _openAIService = new OpenAIRealtimeService(apiKey);
            _openAIService.TranscriptionReceived += OnTranscriptionReceived;
            _openAIService.ErrorOccurred += OnError;
            _openAIService.Connected += OnConnected;
            _openAIService.Disconnected += OnDisconnected;
            
            await _openAIService.ConnectAsync();
            
            _audioService = new AudioCaptureService();
            _audioService.AudioDataAvailable += OnAudioDataAvailable;
            _audioService.ErrorOccurred += OnError;
            
            UpdateConnectionStatus(true);
            RecordButton.IsEnabled = true;
            ConnectButton.Content = "Disconnect";
            ConnectButton.IsEnabled = true;
            
            ClearError();
        }
        catch (Exception ex)
        {
            ShowError($"Connection failed: {ex.Message}");
            ConnectButton.Content = "Connect";
            ConnectButton.IsEnabled = true;
            UpdateConnectionStatus(false);
        }
    }

    private async Task DisconnectServices()
    {
        try
        {
            if (_audioService?.IsRecording == true)
            {
                StopRecording();
            }
            
            _audioService?.Dispose();
            _audioService = null;
            
            if (_openAIService != null)
            {
                await _openAIService.DisconnectAsync();
                _openAIService.Dispose();
                _openAIService = null;
            }
            
            UpdateConnectionStatus(false);
            RecordButton.IsEnabled = false;
            ConnectButton.Content = "Connect";
        }
        catch (Exception ex)
        {
            ShowError($"Disconnect error: {ex.Message}");
        }
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_audioService?.IsRecording == true)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        try
        {
            _audioService?.StartRecording();
            RecordButton.Content = "Stop Recording";
            RecordButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
            _audioLevelTimer.Start();
            _isStreaming = true;
            
            _ = Task.Run(StreamAudioToAPI);
            
            TranscriptionTextBox.Text = "";
            ClearError();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to start recording: {ex.Message}");
        }
    }

    private void StopRecording()
    {
        try
        {
            _isStreaming = false;
            _audioService?.StopRecording();
            RecordButton.Content = "Start Recording";
            RecordButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));
            _audioLevelTimer.Stop();
            AudioLevelBar.Value = 0;
            
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                if (_openAIService?.IsConnected == true)
                {
                    await _openAIService.CommitAudioAsync();
                    await _openAIService.CreateResponseAsync();
                }
            });
        }
        catch (Exception ex)
        {
            ShowError($"Failed to stop recording: {ex.Message}");
        }
    }

    private async Task StreamAudioToAPI()
    {
        try
        {
            while (_isStreaming)
            {
                if (_audioBuffer.Count > 0)
                {
                    var audioData = _audioBuffer.Dequeue();
                    if (_openAIService?.IsConnected == true)
                    {
                        await _openAIService.SendAudioDataAsync(audioData);
                    }
                }
                else
                {
                    await Task.Delay(50);
                }
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ShowError($"Streaming error: {ex.Message}"));
        }
    }

    private void OnAudioDataAvailable(object? sender, byte[] audioData)
    {
        _audioBuffer.Enqueue(audioData);
        
        var level = CalculateAudioLevel(audioData);
        Dispatcher.Invoke(() => AudioLevelBar.Value = level);
    }

    private double CalculateAudioLevel(byte[] audioData)
    {
        if (audioData.Length < 2) return 0;

        double sum = 0;
        for (int i = 0; i < audioData.Length; i += 2)
        {
            short sample = BitConverter.ToInt16(audioData, i);
            sum += Math.Abs(sample);
        }

        double average = sum / (audioData.Length / 2);
        double normalized = (average / short.MaxValue) * 100;
        
        return Math.Min(100, normalized * 3);
    }

    private void OnTranscriptionReceived(object? sender, string transcription)
    {
        Dispatcher.Invoke(() =>
        {
            if (TranscriptionTextBox.Text == "Your transcribed text will appear here..." || 
                string.IsNullOrEmpty(TranscriptionTextBox.Text))
            {
                TranscriptionTextBox.Text = transcription;
            }
            else
            {
                TranscriptionTextBox.Text += " " + transcription;
            }
            
            TranscriptionTextBox.ScrollToEnd();
        });
    }

    private void OnError(object? sender, string error)
    {
        Dispatcher.Invoke(() => ShowError(error));
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(true));
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(false));
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        if (isConnected)
        {
            StatusText.Text = "Connected";
            StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        }
        else
        {
            StatusText.Text = "Disconnected";
            StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"));
        }
    }

    private void UpdateAudioLevel(object? sender, EventArgs e)
    {
        if (!_audioService?.IsRecording == true)
        {
            AudioLevelBar.Value = Math.Max(0, AudioLevelBar.Value - 5);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        TranscriptionTextBox.Text = "Your transcribed text will appear here...";
        ClearError();
    }

    private void ShowError(string message)
    {
        // Prepend new messages to show most recent first
        if (string.IsNullOrEmpty(ErrorText.Text))
        {
            ErrorText.Text = message;
        }
        else
        {
            ErrorText.Text = message + " | " + ErrorText.Text;
            // Limit the length to prevent UI overflow
            if (ErrorText.Text.Length > 500)
            {
                ErrorText.Text = ErrorText.Text.Substring(0, 500) + "...";
            }
        }
        ErrorText.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorText.Text = "";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await DisconnectServices();
    }
}