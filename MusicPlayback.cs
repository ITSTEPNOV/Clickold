using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer;

namespace Clickold;

internal sealed class MusicPlayback : IDisposable
{
    private readonly WaveOutEvent output = new();
    private readonly MpegSamples samples;

    public MusicPlayback(float volume = 0.5f)
    {
        var stream = typeof(MusicPlayback).Assembly.GetManifestResourceStream("Clickold.Art.Cuck_Chair.mp3")
            ?? throw new FileNotFoundException("Missing embedded audio resource.");
        try
        {
            samples = new MpegSamples(stream);
        }
        catch
        {
            stream.Dispose();
            output.Dispose();
            throw;
        }

        try
        {
            var volumeProvider = new VolumeSampleProvider(samples) { Volume = volume };
            output.Init(new SampleToWaveProvider16(volumeProvider));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public event EventHandler<StoppedEventArgs> Stopped
    {
        add => output.PlaybackStopped += value;
        remove => output.PlaybackStopped -= value;
    }

    public void Play() => output.Play();

    public void Stop() => output.Stop();

    public void Dispose()
    {
        try
        {
            output.Dispose();
        }
        finally
        {
            samples.Dispose();
        }
    }

    private sealed class MpegSamples(Stream stream) : ISampleProvider, IDisposable
    {
        private readonly MpegFile decoder = new(stream);
        private readonly object sync = new();
        private bool disposed;

        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(decoder.SampleRate, decoder.Channels);

        public int Read(float[] buffer, int offset, int count)
        {
            lock (sync)
                return disposed ? 0 : decoder.ReadSamples(buffer, offset, count);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                decoder.Dispose();
                stream.Dispose();
            }
        }
    }
}
