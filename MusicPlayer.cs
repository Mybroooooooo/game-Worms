using System;
using System.Windows.Media;

namespace Worms
{
    public static class MusicPlayer
    {
        private static MediaPlayer _player = new MediaPlayer();

        static MusicPlayer()
        {
            _player.MediaEnded += (s, e) => _player.Position = TimeSpan.Zero;
            _player.Volume = 0.3;
        }

        public static void Play(string fileName)
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = System.IO.Path.Combine(basePath, "Sounds", fileName);

                _player.Open(new Uri(fullPath));
                _player.Play();
            }
            catch (Exception ex)
            {
            }
        }

        public static void Stop()
        {
            _player.Stop();
        }

        public static void SetVolume(double volume)
        {
            _player.Volume = volume;
        }
    }
}