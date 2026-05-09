using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace PepeTerm.Controls
{
    /// <summary>
    /// Универсальный терминал для Telnet, SSH и Serial через ConPTY.
    /// Запускает консольное приложение и встраивает его вывод в WPF-окно.
    /// </summary>
    public partial class ConPtyTerminal : System.Windows.Controls.UserControl
    {
        private Process? _process;

        public ConPtyTerminal()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Запускает терминал с указанным протоколом и параметрами.
        /// </summary>
        /// <param name="protocol">"SSH", "Telnet" или "Serial"</param>
        /// <param name="host">Адрес хоста или COM-порт</param>
        /// <param name="port">Порт (для SSH/Telnet)</param>
        /// <param name="username">Имя пользователя (для SSH)</param>
        public Task ConnectAsync(string protocol, string host, int port = 0, string username = "")
        {
            string arguments = protocol switch
            {
                "SSH" => $"{username}@{host} -p {port}",
                "Telnet" => $"{host} {port}",
                "Serial" => $"{host}", // COM-порт
                _ => throw new ArgumentException("Неизвестный протокол")
            };

            string executable = protocol switch
            {
                "SSH" => "ssh.exe",
                "Telnet" => "telnet.exe",
                "Serial" => "mode.com", // Для Serial понадобится дополнительная настройка
                _ => "cmd.exe"
            };

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            _process.Start();

            // Здесь будет код для подключения ConPTY
            // (в следующем шаге)

            return Task.CompletedTask;
        }

        /// <summary>
        /// Отключает терминал и закрывает процесс.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_process is not null)
                {
                    if (!_process.HasExited)
                        _process.Kill();
                    _process.Dispose();
                    _process = null;
                }
            }
            catch (InvalidOperationException)
            {
                // Процесс ещё не запущен — игнорируем
            }
        }
    }
}