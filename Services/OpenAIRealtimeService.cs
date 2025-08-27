using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Speechify.Services;

public class OpenAIRealtimeService : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly string _apiKey;
    private readonly string _model = "gpt-4o-mini-realtime-preview";
    
    public event EventHandler<string>? TranscriptionReceived;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public OpenAIRealtimeService(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task ConnectAsync()
    {
        try
        {
            ErrorOccurred?.Invoke(this, $"Connecting to OpenAI Realtime API with model: {_model}");
            
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            var uri = new Uri($"wss://api.openai.com/v1/realtime?model={_model}");
            ErrorOccurred?.Invoke(this, $"Connecting to: {uri}");
            
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            
            ErrorOccurred?.Invoke(this, $"WebSocket connected successfully");
            Connected?.Invoke(this, EventArgs.Empty);
            
            _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));
            
            await ConfigureSession();
            ErrorOccurred?.Invoke(this, $"Session configured successfully");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection error: {ex.Message}\nStack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task ConfigureSession()
    {
        var sessionConfig = new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions = "You are a helpful assistant that transcribes audio to text accurately. Only transcribe what you hear, do not add any commentary or additional text.",
                input_audio_format = "pcm16",
                input_audio_transcription = new
                {
                    model = "gpt-4o-mini-transcribe"
                },
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = 0.5,
                    prefix_padding_ms = 300,
                    silence_duration_ms = 500
                },
                tools = Array.Empty<object>(),
                tool_choice = "none"
            }
        };

        await SendMessageAsync(JsonConvert.SerializeObject(sessionConfig));
    }

    public async Task SendAudioDataAsync(byte[] audioData)
    {
        if (!IsConnected) return;

        var base64Audio = Convert.ToBase64String(audioData);
        var audioMessage = new
        {
            type = "input_audio_buffer.append",
            audio = base64Audio
        };

        await SendMessageAsync(JsonConvert.SerializeObject(audioMessage));
    }

    public async Task CommitAudioAsync()
    {
        if (!IsConnected) return;

        var commitMessage = new
        {
            type = "input_audio_buffer.commit"
        };

        await SendMessageAsync(JsonConvert.SerializeObject(commitMessage));
    }

    public async Task CreateResponseAsync()
    {
        if (!IsConnected) return;

        var responseMessage = new
        {
            type = "response.create",
            response = new
            {
                modalities = new[] { "text" },
                instructions = "Please transcribe the audio accurately."
            }
        };

        await SendMessageAsync(JsonConvert.SerializeObject(responseMessage));
    }

    private async Task SendMessageAsync(string message)
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);
        var messageBuilder = new StringBuilder();

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                    messageBuilder.Append(message);

                    if (result.EndOfMessage)
                    {
                        ProcessMessage(messageBuilder.ToString());
                        messageBuilder.Clear();
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
        }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            var json = JObject.Parse(message);
            var type = json["type"]?.ToString();
            
            // Log all message types for debugging
            ErrorOccurred?.Invoke(this, $"Received message type: {type}");

            switch (type)
            {
                case "session.created":
                    ErrorOccurred?.Invoke(this, "Session created successfully");
                    break;
                    
                case "session.updated":
                    ErrorOccurred?.Invoke(this, "Session updated successfully");
                    break;
                    
                case "conversation.item.input_audio_transcription.completed":
                    var transcription = json["transcript"]?.ToString();
                    if (!string.IsNullOrEmpty(transcription))
                    {
                        TranscriptionReceived?.Invoke(this, transcription);
                    }
                    break;

                case "error":
                    var error = json["error"]?.ToString() ?? json.ToString();
                    ErrorOccurred?.Invoke(this, $"API Error: {error}");
                    break;
                    
                default:
                    // Log any unhandled message types
                    if (type != null && !type.StartsWith("input_audio_buffer"))
                    {
                        ErrorOccurred?.Invoke(this, $"Unhandled message type: {type}");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Message processing error: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Disconnect error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}