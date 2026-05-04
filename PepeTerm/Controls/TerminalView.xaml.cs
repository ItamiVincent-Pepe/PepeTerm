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
    public partial class TerminalView : UserControl
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        public TerminalView()
        {
            InitializeComponent();
            TerminalTextBox.IsReadOnly = true;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _stream = _tcpClient.GetStream();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoop(_cts.Token));
        }

        private async Task ReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested && _tcpClient is not null && _tcpClient.Connected)
                {
                    int bytesRead = await _stream!.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                    if (bytesRead == 0) break;

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
            catch (OperationCanceledException) { }
            catch { }
        }

        private void ProcessAnsiText(string text)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                if (text[pos] == '\x1b' && pos + 1 < text.Length && text[pos + 1] == '[')
                {
                    int end = pos + 2;
                    while (end < text.Length && !char.IsLetter(text[end])) end++;
                    if (end < text.Length) { ExecuteAnsiCommand(text.Substring(pos + 2, end - pos - 2), text[end]); pos = end + 1; continue; }
                }
                char c = text[pos];
                switch (c)
                {
                    case '\b' or '\x7f': if (TerminalTextBox.Text.Length > 0) TerminalTextBox.Text = TerminalTextBox.Text[..^1]; break;
                    case '\r': break;
                    case '\n': TerminalTextBox.AppendText("\r\n"); break;
                    default: TerminalTextBox.AppendText(c.ToString()); break;
                }
                pos++;
            }
            TerminalTextBox.ScrollToEnd();
            TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
        }

        private void ExecuteAnsiCommand(string code, char cmd)
        {
            if (!int.TryParse(code, out int n)) n = 1;
            switch (cmd)
            {
                case 'D': if (TerminalTextBox.Text.Length >= n) TerminalTextBox.Text = TerminalTextBox.Text[..^n]; break;
                case 'K': int ln = TerminalTextBox.Text.LastIndexOf('\n'); TerminalTextBox.Text = ln >= 0 ? TerminalTextBox.Text[..(ln + 1)] : ""; break;
                case 'A' or 'B' or 'H' or 'F' or 'J': break;
            }
        }

        private async void TerminalTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_stream is null || _tcpClient is null || !_tcpClient.Connected) return;
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
            if (!string.IsNullOrEmpty(charToSend)) { byte[] data = Encoding.UTF8.GetBytes(charToSend); await _stream.WriteAsync(data.AsMemory(0, data.Length)); }
            e.Handled = true;
        }

        private static string? GetCharFromKey(Key key, ModifierKeys modifiers)
        {
            if (key >= Key.A && key <= Key.Z) { string l = key.ToString(); if (modifiers.HasFlag(ModifierKeys.Control)) return ((char)(l[0] - 'A' + 1)).ToString(); return modifiers.HasFlag(ModifierKeys.Shift) ? l : l.ToLower(); }
            if (key >= Key.D0 && key <= Key.D9) { int d = key - Key.D0; if (modifiers.HasFlag(ModifierKeys.Shift)) { string[] sd = [")", "!", "@", "#", "$", "%", "^", "&", "*", "("]; return sd[d]; } return d.ToString(); }
            string? s = key switch { Key.Space => " ", Key.OemPeriod => ".", Key.OemComma => ",", Key.OemMinus => "-", Key.OemPlus => "=", Key.OemQuestion => "/", Key.OemSemicolon => ";", Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]", Key.OemQuotes => "'", Key.OemPipe => "\\", Key.OemTilde => "`", _ => null };
            if (s is not null && modifiers.HasFlag(ModifierKeys.Shift)) s = s switch { "." => ">", "," => "<", "-" => "_", "=" => "+", "/" => "?", ";" => ":", "[" => "{", "]" => "}", "'" => "\"", "\\" => "|", "`" => "~", _ => s };
            return s;
        }

        public void Disconnect() { _cts?.Cancel(); _stream?.Close(); _tcpClient?.Close(); }
    }
}