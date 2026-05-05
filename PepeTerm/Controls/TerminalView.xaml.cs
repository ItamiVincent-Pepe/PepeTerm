using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PepeTerm.Controls
{
    /// <summary>
    /// Терминал для Telnet-подключений.
    /// Подключается к удалённому хосту по TCP и эмулирует терминал VT100/ANSI.
    /// </summary>
    public partial class TerminalView : System.Windows.Controls.UserControl
    {
        /// <summary>TCP-клиент для подключения к серверу</summary>
        private TcpClient? _tcpClient;
        /// <summary>Поток данных от сервера</summary>
        private NetworkStream? _stream;
        /// <summary>Токен для отмены асинхронных операций при отключении</summary>
        private CancellationTokenSource? _cts;

        public TerminalView()
        {
            InitializeComponent();
            TerminalTextBox.IsReadOnly = true; // Блокируем прямой ввод — всё через PreviewKeyDown
        }

        /// <summary>
        /// Подключается к удалённому хосту по Telnet (порт 23)
        /// </summary>
        public async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _stream = _tcpClient.GetStream();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoop(_cts.Token)); // Запускаем чтение в фоне
        }

        /// <summary>
        /// Бесконечный цикл чтения данных от сервера.
        /// Принимает байты, чистит Telnet-negotiation (IAC команды) и ANSI-escape последовательности.
        /// </summary>
        private async Task ReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested && _tcpClient is not null && _tcpClient.Connected)
                {
                    int bytesRead = await _stream!.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                    if (bytesRead == 0) break;

                    // Очищаем Telnet-команды (IAC — байт 0xFF)
                    var cleanBytes = new byte[bytesRead];
                    int cleanIndex = 0, i = 0;
                    while (i < bytesRead)
                    {
                        if (buffer[i] == 0xFF)
                        {
                            if (i + 2 < bytesRead)
                            {
                                byte command = buffer[i + 1], option = buffer[i + 2];
                                if (command == 0xFD) { byte[] wont = [0xFF, 0xFC, option]; await _stream.WriteAsync(wont.AsMemory(0, 3), token); }
                                else if (command == 0xFB) { byte[] dont = [0xFF, 0xFE, option]; await _stream.WriteAsync(dont.AsMemory(0, 3), token); }
                                i += 3;
                            }
                            else i = bytesRead;
                        }
                        else { cleanBytes[cleanIndex++] = buffer[i]; i++; }
                    }

                    if (cleanIndex > 0)
                    {
                        string rawText = Encoding.UTF8.GetString(cleanBytes, 0, cleanIndex);
                        await Dispatcher.InvokeAsync(() => ProcessAnsiText(rawText));
                    }
                }
            }
            catch (OperationCanceledException) { /* Нормальное завершение */ }
            catch { /* Соединение разорвано */ }
        }

        /// <summary>
        /// Обрабатывает текст с ANSI escape-последовательностями.
        /// Двигает курсор, удаляет символы по командам сервера.
        /// </summary>
        private void ProcessAnsiText(string text)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                // Обнаружена escape-последовательность — ESC[
                if (text[pos] == '\x1b' && pos + 1 < text.Length && text[pos + 1] == '[')
                {
                    int end = pos + 2;
                    while (end < text.Length && !char.IsLetter(text[end])) end++;
                    if (end < text.Length) { ExecuteAnsiCommand(text.Substring(pos + 2, end - pos - 2), text[end]); pos = end + 1; continue; }
                }

                char c = text[pos];
                switch (c)
                {
                    case '\b' or '\x7f': // Backspace — удаляем последний символ
                        if (TerminalTextBox.Text.Length > 0) TerminalTextBox.Text = TerminalTextBox.Text[..^1]; break;
                    case '\r': break; // Возврат каретки — игнорируем
                    case '\n': TerminalTextBox.AppendText("\r\n"); break; // Перевод строки
                    default: TerminalTextBox.AppendText(c.ToString()); break; // Обычный символ
                }
                pos++;
            }
            TerminalTextBox.ScrollToEnd();
            TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
        }

        /// <summary>
        /// Исполняет ANSI escape-команду (движение курсора, удаление символов и т.д.)
        /// </summary>
        private void ExecuteAnsiCommand(string code, char cmd)
        {
            if (!int.TryParse(code, out int n)) n = 1;
            switch (cmd)
            {
                case 'D': // Курсор влево — удалить n символов
                    if (TerminalTextBox.Text.Length >= n) TerminalTextBox.Text = TerminalTextBox.Text[..^n]; break;
                case 'K': // Очистить от курсора до конца строки
                    int ln = TerminalTextBox.Text.LastIndexOf('\n');
                    TerminalTextBox.Text = ln >= 0 ? TerminalTextBox.Text[..(ln + 1)] : ""; break;
                case 'A' or 'B' or 'H' or 'F' or 'J': break; // Навигация — не трогаем
            }
        }

        /// <summary>
        /// Перехватывает нажатия клавиш и отправляет их серверу.
        /// Блокирует стандартную обработку TextBox'ом.
        /// </summary>
        private async void TerminalTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_stream is null || _tcpClient is null || !_tcpClient.Connected) return;

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
                await _stream.WriteAsync(data.AsMemory(0, data.Length));
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

        /// <summary>Закрывает соединение и освобождает ресурсы</summary>
        public void Disconnect() { _cts?.Cancel(); _stream?.Close(); _tcpClient?.Close(); }
    }
}