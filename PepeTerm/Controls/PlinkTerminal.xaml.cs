using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PepeTerm.Controls
{
    /// <summary>
    /// Терминал на основе plink.exe (PuTTY CLI).
    /// Портативный: не требует установки компонентов Windows.
    /// </summary>
    public partial class PlinkTerminal : System.Windows.Controls.UserControl
    {
        private Process? _process;
        private CancellationTokenSource? _cts;

        public PlinkTerminal()
        {
            InitializeComponent();
            TerminalTextBox.IsReadOnly = false;
        }

        /// <summary>
        /// Подключается к хосту через plink.exe
        /// </summary>
        public async Task ConnectAsync(string protocol, string host, int port, string username = "", string password = "")
        {
            string plinkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plink.exe");

            if (!File.Exists(plinkPath))
            {
                TerminalTextBox.AppendText("[Ошибка] plink.exe не найден\r\n");
                return;
            }

            string args = protocol switch
            {
                "Telnet" => $"-telnet {host} -P {port}",
                "SSH" => string.IsNullOrEmpty(password)
                    ? $"-ssh {username}@{host} -P {port}"
                    : $"-ssh {username}@{host} -P {port} -pw {password}",
                _ => throw new ArgumentException("Протокол не поддерживается")
            };

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = plinkPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            _process.Start();
            _cts = new CancellationTokenSource();

            // Для SSH — подтверждаем ключ
            if (protocol == "SSH")
            {
                await _process.StandardInput.WriteAsync("y\ny\n");
                await _process.StandardInput.FlushAsync();
            }

            _ = Task.Run(() => ReadOutput(_cts.Token));
            _ = Task.Run(() => ReadError(_cts.Token));
        }

        /// <summary>Обрабатывает ANSI escape-последовательности</summary>
        private static string ProcessAnsiText(string text)
        {
            int pos = 0;
            var result = new StringBuilder();
            while (pos < text.Length)
            {
                if (text[pos] == '\x1b' && pos + 1 < text.Length && text[pos + 1] == '[')
                {
                    int end = pos + 2;
                    while (end < text.Length && !char.IsLetter(text[end])) end++;
                    if (end < text.Length) { pos = end + 1; continue; }
                }
                char c = text[pos];
                if (c == '\b' || c == '\x7f')
                {
                    if (result.Length > 0) result.Length--;
                }
                else
                {
                    result.Append(c);
                }
                pos++;
            }
            return result.ToString();
        }

        /// <summary>Читает стандартный вывод plink и показывает в терминале</summary>
        private async Task ReadOutput(CancellationToken token)
        {
            try
            {
                var reader = _process!.StandardOutput;
                char[] buffer = new char[8192];
                while (!token.IsCancellationRequested && !_process.HasExited)
                {
                    int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    string rawText = new(buffer, 0, read);
                    string text = ProcessAnsiText(rawText);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TerminalTextBox.AppendText(text);
                        TerminalTextBox.ScrollToEnd();
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        /// <summary>Читает ошибки plink и показывает в терминале</summary>
        private async Task ReadError(CancellationToken token)
        {
            try
            {
                var reader = _process!.StandardError;
                char[] buffer = new char[8192];
                while (!token.IsCancellationRequested && !_process.HasExited)
                {
                    int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    string rawText = new(buffer, 0, read);
                    string text = ProcessAnsiText(rawText);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TerminalTextBox.AppendText(text);
                        TerminalTextBox.ScrollToEnd();
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        /// <summary>Отправляет нажатия клавиш в plink</summary>
        private async void TerminalTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_process is null || _process.HasExited) return;

            string? text = e.Key switch
            {
                Key.Enter => "\r",
                Key.Back => "\b",
                Key.Tab => "\t",
                Key.Escape => "\x1b",
                _ => null
            };

            if (text is not null)
            {
                await _process.StandardInput.WriteAsync(text);

                if (e.Key == Key.Back && TerminalTextBox.Text.Length > 0)
                {
                    TerminalTextBox.Text = TerminalTextBox.Text[..^1];
                    TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
                }

                e.Handled = true;
                return;
            }

            string? ch = GetCharFromKey(e.Key, Keyboard.Modifiers);
            if (ch is not null)
            {
                await _process.StandardInput.WriteAsync(ch);
                e.Handled = true;
            }
        }

        private static string? GetCharFromKey(Key key, ModifierKeys modifiers)
        {
            if (key >= Key.A && key <= Key.Z)
            {
                string l = key.ToString();
                if (modifiers.HasFlag(ModifierKeys.Control)) return ((char)(l[0] - 'A' + 1)).ToString();
                return modifiers.HasFlag(ModifierKeys.Shift) ? l : l.ToLower();
            }
            if (key >= Key.D0 && key <= Key.D9)
            {
                int d = key - Key.D0;
                if (modifiers.HasFlag(ModifierKeys.Shift))
                {
                    string[] sd = [")", "!", "@", "#", "$", "%", "^", "&", "*", "("];
                    return sd[d];
                }
                return d.ToString();
            }
            return key switch
            {
                Key.Space => " ",
                Key.OemPeriod => modifiers.HasFlag(ModifierKeys.Shift) ? ">" : ".",
                Key.OemMinus => modifiers.HasFlag(ModifierKeys.Shift) ? "_" : "-",
                Key.OemPlus => modifiers.HasFlag(ModifierKeys.Shift) ? "+" : "=",
                Key.OemQuestion => modifiers.HasFlag(ModifierKeys.Shift) ? "?" : "/",
                Key.OemSemicolon => modifiers.HasFlag(ModifierKeys.Shift) ? ":" : ";",
                _ => null
            };
        }

        /// <summary>Отключает терминал</summary>
        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                if (_process is not null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.Dispose();
                    _process = null;
                }
            }
            catch { }
        }
    }
}