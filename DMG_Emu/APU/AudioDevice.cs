using SDL2;

public class AudioDevice
{
    private uint _deviceId;
    public const int SampleRate = 44100;

    public AudioDevice()
    {

        if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_AUDIO) < 0)
        {
            Console.WriteLine($"Failed to initialize SDL audio: {SDL.SDL_GetError()}");
            return;
        }

        SDL.SDL_AudioSpec desired = new SDL.SDL_AudioSpec
        {
            freq = SampleRate,
            format = SDL.AUDIO_S16SYS,
            channels = 2,
            samples = 1024,
            callback = null,
        };

        string deviceName = SDL.SDL_GetNumAudioDevices(0) > 0 ? SDL.SDL_GetAudioDeviceName(0, 0) : "";
        _deviceId = SDL.SDL_OpenAudioDevice(deviceName, 0, ref desired, out SDL.SDL_AudioSpec obtained, 0);

        if (_deviceId == 0)
        {
            Console.WriteLine($"Failed to open audio device: {SDL.SDL_GetError()}");
            return;
        }

        SDL.SDL_PauseAudioDevice(_deviceId, 0);
    }

    public void QueueAudio(short[] sampleBuffer, int count)
    {
        if (_deviceId == 0) return;

        unsafe
        {
            fixed (short* ptr = sampleBuffer)
            {
                SDL.SDL_QueueAudio(_deviceId, (IntPtr)ptr, (uint)(count * sizeof(short)));
            }
        }
    }

    public void Dispose()
    {
        if (_deviceId != 0)
        {
            SDL.SDL_CloseAudioDevice(_deviceId);
            _deviceId = 0;
        }
    }
}