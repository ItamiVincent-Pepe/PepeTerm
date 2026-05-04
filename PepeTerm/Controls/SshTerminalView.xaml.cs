using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Renci.SshNet;

namespace PepeTerm.Controls
{
    public partial class SshTerminalView : UserControl
    {
        private SshClient? _sshClient;
        private ShellStream? _shellStream;
        private CancellationTokenSource? _cts;

        public SshTerminalView()
        {
            InitializeComponent();
        }

        public async Task ConnectAsync(string host, int port, string username, string password)
        {
            _sshClient = new SshClient(host, port, username, password);
            await Task.Run(() => _sshClient.Connect());
            _shellStream = _sshClient.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoop(_cts.Token));
        }

        private async Task ReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested && _sshClient is not null && _sshClient.IsConnected)
                {
                    int bytesRead = await _shellStream!.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                    if (bytesRead == 0) break;
                    string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await Dispatcher.InvokeAsync(() => { TerminalTextBox.AppendText(text); TerminalTextBox.ScrollToEnd(); TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length; });
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private async void TerminalTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_shellStream is null || _sshClient is null || !_sshClient.IsConnected) return;
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
            if (!string.IsNullOrEmpty(charToSend)) { byte[] data = Encoding.UTF8.GetBytes(charToSend); await _shellStream.WriteAsync(data.AsMemory(0, data.Length)); }
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

        public void Disconnect() { _cts?.Cancel(); _shellStream?.Close(); _sshClient?.Disconnect(); _sshClient?.Dispose(); }
    }
}