using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Worms
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MusicPlayer.Play("background_music.mp3");
            new MainWindow().Show();
        }
        protected override void OnExit(ExitEventArgs e)
        {
            MusicPlayer.Stop();

            base.OnExit(e);
        }
    }
}