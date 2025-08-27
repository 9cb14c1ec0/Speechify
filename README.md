# Speechify - Real-time Speech to Text

A WPF application that provides real-time speech-to-text transcription using OpenAI's Realtime API with the GPT-4o Mini model.

## Features

- **Real-time Transcription**: Convert speech to text in real-time using OpenAI's latest API
- **Audio Level Monitoring**: Visual feedback showing microphone input levels
- **Persistent API Key**: Your OpenAI API key is automatically saved for future sessions
- **Editable Transcriptions**: Edit the transcribed text directly in the application
- **Clean, Modern UI**: Simple and intuitive interface with connection status indicators

## Requirements

- .NET 9.0 or later
- Windows OS (WPF application)
- OpenAI API key with access to the Realtime API
- Microphone/audio input device

## Setup

1. Clone or download this repository
2. Build the application:
   ```bash
   dotnet build
   ```
3. Run the application:
   ```bash
   dotnet run
   ```

## Usage

1. **Enter API Key**: On first launch, enter your OpenAI API key in the text field. The key will be saved automatically.

2. **Connect**: Click the "Connect" button to establish a connection to OpenAI's Realtime API.

3. **Start Recording**: Once connected (indicated by the green status light), click "Start Recording" to begin transcription.

4. **Stop Recording**: Click "Stop Recording" when finished. The transcription will appear in the text area below.

5. **Edit Text**: The transcribed text is fully editable - you can make corrections or additions as needed.

6. **Clear**: Use the "Clear" button to reset the transcription area.

## Configuration

The application saves your API key in `appsettings.json` in the application directory. This file is created automatically when you enter your API key.

## Technical Details

- **Model**: Uses OpenAI's `gpt-4o-mini-realtime-preview` model
- **Audio Format**: PCM 16-bit, 24kHz, mono channel
- **Voice Activity Detection**: Server-side VAD with configurable thresholds
- **WebSocket Connection**: Real-time bidirectional communication with OpenAI's API

## Troubleshooting

- **Connection Issues**: Check that your API key is valid and has access to the Realtime API
- **No Transcription**: Ensure your microphone is working and properly configured in Windows
- **Audio Level Not Showing**: Check microphone permissions for the application

## Dependencies

- NAudio - Audio capture and processing
- Newtonsoft.Json - JSON serialization
- System.Net.WebSockets - WebSocket communication
- Microsoft.Extensions.Configuration - Configuration management

## License

Copyright 2025 Edwin Zimmerman, released under MIT license