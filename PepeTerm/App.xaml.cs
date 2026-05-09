using System;
using System.Windows;
using System.Windows.Threading;

namespace PepeTerm
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Не закрывать приложение, когда сплеш закроется
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var splash = new Dialogs.SplashWindow();
            splash.Show();

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            timer.Tick += (s, args) =>
            {
                timer.Stop();
                splash.Close();

                // Возвращаем обычный режим завершения
                ShutdownMode = ShutdownMode.OnLastWindowClose;

                // Запускаем главное окно
                var main = new MainWindow();
                main.Show();
            };

            timer.Start();
        }
    }
}