using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PepeTerm.Controls
{
    /// <summary>
    /// Терминал для Serial-подключений (COM-порт).
    /// Подключается к устройству через последовательный порт (RS-232).
    /// </summary>
    public partial class SerialTerminalView : System.Windows.Controls.UserControl
    {
        /// <summary>Последовательный порт для подключения к устройству</summary>
        private SerialPort? _serialPort;
        /// <summary>Токен для отмены асинхронных операций при отключении</summary>
        private CancellationTokenSource? _cts;

        public SerialTerminalView()
        {
            InitializeComponent();
            TerminalTextBox.IsReadOnly = true; // Блокируем прямой ввод — всё через PreviewKeyDown
        }

        /// <summary>
        /// Подключается к устройству через COM-порт.
        /// Параметры по умолчанию: 9600 бод, 8 бит, без чётности, 1 стоп-бит (стандарт Cisco).
        /// </summary>
        public async Task ConnectAsync(string portName, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                Encoding = Encoding.UTF8,
                ReadTimeout = 500,  // Таймаут чтения — чтобы не зависать навсегда
                WriteTimeout = 500  // Таймаут записи
            };

            await Task.Run(() => _serialPort.Open()); // Открываем порт в фоне
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoop(_cts.Token)); // Запускаем чтение в фоне
        }

        /// <summary>
        /// Бесконечный цикл чтения данных с COM-порта.
        /// Принимает байты и выводит их в терминал.
        /// </summary>
        private async Task ReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested && _serialPort is not null && _serialPort.IsOpen)
                {
                    try
                    {
                        int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                        if (bytesRead == 0) break;
                        string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            TerminalTextBox.AppendText(text);
                            TerminalTextBox.ScrollToEnd();
                            TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
                        });
                    }
                    catch (TimeoutException) { continue; } // Нет данных — ждём дальше
                }
            }
            catch (OperationCanceledException) { /* Нормальное завершение */ }
            catch { /* Порт отключён */ }
        }

        /// <summary>
        /// Перехватывает нажатия клавиш и отправляет их в COM-порт.
        /// Блокирует стандартную обработку TextBox'ом.
        /// </summary>
        private async void TerminalTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_serialPort is null || !_serialPort.IsOpen) return;

            // Преобразуем клавишу в escape-последовательность или символ
            string? charToSend = e.Key switch
            {
                Key.Enter => "\r",
                Key.Back => "\b",
                Key.Delete => "\x1b[3~",
                Key.Tab => "\t",
                Key.Escape => "\x1b",
                Key.Up => "\x1b[A",
                Key.Down => "\x1b[B",
                Key.Right => "\x1b[C",
                Key.Left => "\x1b[D",
                Key.Home => "\x1b[H",
                Key.End => "\x1b[F",
                _ => GetCharFromKey(e.Key, Keyboard.Modifiers)
            };

            if (!string.IsNullOrEmpty(charToSend))
            {
                byte[] data = Encoding.UTF8.GetBytes(charToSend);
                await _serialPort.BaseStream.WriteAsync(data.AsMemory(0, data.Length));
            }
            e.Handled = true; // Блокируем стандартную обработку клавиши
        }

        /// <summary>
        /// Преобразует Key и модификаторы в строку символа.
        /// Учитывает Shift и Ctrl.
        /// </summary>
        private static string? GetCharFromKey(Key key, ModifierKeys modifiers)
        {
            if (key >= Key.A && key <= Key.Z) { string l = key.ToString(); if (modifiers.HasFlag(ModifierKeys.Control)) return ((char)(l[0] - 'A' + 1)).ToString(); return modifiers.HasFlag(ModifierKeys.Shift) ? l : l.ToLower(); }
            if (key >= Key.D0 && key <= Key.D9) { int d = key - Key.D0; if (modifiers.HasFlag(ModifierKeys.Shift)) { string[] sd = [")", "!", "@", "#", "$", "%", "^", "&", "*", "("]; return sd[d]; } return d.ToString(); }
            string? s = key switch { Key.Space => " ", Key.OemPeriod => ".", Key.OemComma => ",", Key.OemMinus => "-", Key.OemPlus => "=", Key.OemQuestion => "/", Key.OemSemicolon => ";", Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]", Key.OemQuotes => "'", Key.OemPipe => "\\", Key.OemTilde => "`", _ => null };
            if (s is not null && modifiers.HasFlag(ModifierKeys.Shift)) s = s switch { "." => ">", "," => "<", "-" => "_", "=" => "+", "/" => "?", ";" => ":", "[" => "{", "]" => "}", "'" => "\"", "\\" => "|", "`" => "~", _ => s };
            return s;
        }

        /// <summary>Закрывает COM-порт и освобождает ресурсы</summary>
        public void Disconnect() { _cts?.Cancel(); _serialPort?.Close(); _serialPort?.Dispose(); }
    }
}