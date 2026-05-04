using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PepeTerm
{
    public partial class MainWindow : Window
    {
        private readonly List<SavedHost> _savedHosts = [];

        public MainWindow()
        {
            InitializeComponent();

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (SessionsTabControl.SelectedItem is TabItem tabItem)
                    {
                        if (tabItem.Content is Controls.TerminalView tv)
                            tv.Disconnect();
                        else if (tabItem.Content is Controls.SshTerminalView sv)
                            sv.Disconnect();
                        SessionsTabControl.Items.Remove(tabItem);
                    }
                    e.Handled = true;
                }
            };
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string host = HostBox.Text.Trim();
            string portText = PortBox.Text.Trim();
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;
            string protocol = RadioTelnet.IsChecked == true ? "Telnet" : "SSH";

            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Введите адрес хоста", "PepeTerm", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(portText, out int port))
            {
                MessageBox.Show("Неверный номер порта", "PepeTerm", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await CreateSessionTab(host, port, protocol, username, password);
        }

        private async System.Threading.Tasks.Task CreateSessionTab(string host, int port, string protocol, string username = "", string password = "")
        {
            var headerPanel = new DockPanel();
            var headerText = new TextBlock { Text = $"[{protocol}] {host}:{port}", VerticalAlignment = VerticalAlignment.Center };
            var closeButton = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = Brushes.Gray,
                FontSize = 12,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            DockPanel.SetDock(closeButton, Dock.Right);
            headerPanel.Children.Add(closeButton);
            headerPanel.Children.Add(headerText);

            var tabItem = new TabItem { Header = headerPanel };

            if (protocol == "SSH")
            {
                var sshTerm = new Controls.SshTerminalView();
                tabItem.Content = sshTerm;
                closeButton.Click += (s2, args2) => { sshTerm.Disconnect(); SessionsTabControl.Items.Remove(tabItem); };
                tabItem.Unloaded += (s2, args2) => sshTerm.Disconnect();
                SessionsTabControl.Items.Add(tabItem);
                SessionsTabControl.SelectedItem = tabItem;
                try { await sshTerm.ConnectAsync(host, port == 0 ? 22 : port, username, password); }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "🐸 PepeTerm", MessageBoxButton.OK, MessageBoxImage.Error); SessionsTabControl.Items.Remove(tabItem); }
            }
            else
            {
                var telnetTerm = new Controls.TerminalView();
                tabItem.Content = telnetTerm;
                closeButton.Click += (s2, args2) => { telnetTerm.Disconnect(); SessionsTabControl.Items.Remove(tabItem); };
                tabItem.Unloaded += (s2, args2) => telnetTerm.Disconnect();
                SessionsTabControl.Items.Add(tabItem);
                SessionsTabControl.SelectedItem = tabItem;
                try { await telnetTerm.ConnectAsync(host, port == 0 ? 23 : port); }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "🐸 PepeTerm", MessageBoxButton.OK, MessageBoxImage.Error); SessionsTabControl.Items.Remove(tabItem); }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string host = HostBox.Text.Trim();
            if (!int.TryParse(PortBox.Text.Trim(), out int port)) return;
            string protocol = RadioTelnet.IsChecked == true ? "Telnet" : "SSH";
            if (_savedHosts.Exists(h => h.Host == host && h.Port == port)) return;
            _savedHosts.Add(new SavedHost { Host = host, Port = port, Protocol = protocol, Username = UsernameBox.Text.Trim() });
            RefreshSavedHostsList();
        }

        private void SavedHostsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SavedHostsList.SelectedItem is SavedHost saved)
            {
                HostBox.Text = saved.Host; PortBox.Text = saved.Port.ToString();
                UsernameBox.Text = saved.Username;
                if (saved.Protocol == "SSH") RadioSSH.IsChecked = true; else RadioTelnet.IsChecked = true;
            }
        }

        private void RefreshSavedHostsList()
        {
            SavedHostsList.Items.Clear();
            foreach (var h in _savedHosts) SavedHostsList.Items.Add(h);
        }
    }

    public class SavedHost
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Protocol { get; set; } = "Telnet";
        public string Username { get; set; } = string.Empty;
        public override string ToString() => $"[{Protocol}] {Host}:{Port}";
    }
}