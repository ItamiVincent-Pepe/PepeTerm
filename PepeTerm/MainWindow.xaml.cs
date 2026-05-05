using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PepeTerm.Dialogs;
using PepeTerm.Models;
using PepeTerm.Services;

namespace PepeTerm
{
    public partial class MainWindow : Window
    {
        private readonly System.Collections.ObjectModel.ObservableCollection<TreeItem> _treeItems = [];
        private readonly SerialConfig _serialConfig = new();
        private readonly System.Windows.Forms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            this.Icon = new System.Windows.Media.Imaging.BitmapImage(
    new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pepe.ico")));

            var loaded = HostStorageService.Load();
            foreach (var item in loaded) _treeItems.Add(item);

            if (_treeItems.Count == 0)
                _treeItems.Add(new TreeItem { Name = "PepeHome", IsFolder = true });

            RefreshTreeView();

            Closed += (_, _) =>
            {
                if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
                HostStorageService.Save(_treeItems);
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (SessionsTabControl.SelectedItem is TabItem tabItem)
                    {
                        if (tabItem.Content is Controls.TerminalView tv) tv.Disconnect();
                        else if (tabItem.Content is Controls.SshTerminalView sv) sv.Disconnect();
                        else if (tabItem.Content is Controls.SerialTerminalView srv) srv.Disconnect();
                        SessionsTabControl.Items.Remove(tabItem);
                    }
                    e.Handled = true;
                }
            };

            // ===== ИКОНКА В ТРЕЕ =====
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pepe.ico");
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.IO.File.Exists(iconPath)
                    ? new System.Drawing.Icon(iconPath)
                    : System.Drawing.SystemIcons.Application,
                Text = "🐸 PepeTerm",
                Visible = false
            };

            _trayIcon.DoubleClick += (_, _) => ShowFromTray();

            var trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add("Развернуть", null, (_, _) => ShowFromTray());
            trayMenu.Items.Add("Выход", null, (_, _) => { _trayIcon!.Visible = false; System.Windows.Forms.Application.Exit(); });
            _trayIcon.ContextMenuStrip = trayMenu;

            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    Hide();
                    _trayIcon!.Visible = true;
                }
            };
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _trayIcon!.Visible = false;
        }

        private TreeViewItem CreateTreeViewItem(TreeItem data)
        {
            string icon = data.HostData?.Protocol switch { "SSH" => "🔒", "Serial" => "🔌", _ => "🖧" };
            var item = new TreeViewItem
            {
                Header = data.IsFolder ? $"📁 {data.Name}" : $"{icon} {data.Name}",
                Tag = data,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC)),
                Background = System.Windows.Media.Brushes.Transparent,
                IsExpanded = data.IsFolder
            };

            var menu = new System.Windows.Controls.ContextMenu();
            var rename = new System.Windows.Controls.MenuItem
            {
                Header = "✏️ Переименовать",
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x2D))
            };
            rename.Click += (_, _) => RenameItem(data);

            var delete = new System.Windows.Controls.MenuItem
            {
                Header = "🗑 Удалить",
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x2D))
            };
            delete.Click += (_, _) => { RemoveFromParent(_treeItems[0], data); RefreshTreeView(); HostStorageService.Save(_treeItems); };

            menu.Items.Add(rename);
            menu.Items.Add(delete);
            item.ContextMenu = menu;

            foreach (var child in data.Children)
                item.Items.Add(CreateTreeViewItem(child));

            return item;
        }

        private void RefreshTreeView()
        {
            var expanded = new System.Collections.Generic.HashSet<string>();
            foreach (TreeViewItem item in TreeView.Items) CollectExpanded(item, expanded);
            TreeView.Items.Clear();
            foreach (var item in _treeItems) { var tvi = CreateTreeViewItem(item); TreeView.Items.Add(tvi); RestoreExpanded(tvi, expanded); }
        }

        private static void CollectExpanded(TreeViewItem item, System.Collections.Generic.HashSet<string> expanded)
        {
            if (item.IsExpanded && item.Tag is TreeItem d) expanded.Add(d.Name + "_" + d.IsFolder);
            foreach (TreeViewItem child in item.Items) CollectExpanded(child, expanded);
        }

        private static void RestoreExpanded(TreeViewItem item, System.Collections.Generic.HashSet<string> expanded)
        {
            if (item.Tag is TreeItem d && expanded.Contains(d.Name + "_" + d.IsFolder)) item.IsExpanded = true;
            foreach (TreeViewItem child in item.Items) RestoreExpanded(child, expanded);
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (RadioSerial.IsChecked == true) { await CreateSerialTab(HostBox.Text.Trim()); return; }
            string host = HostBox.Text.Trim(), portText = PortBox.Text.Trim();
            string user = UsernameBox.Text.Trim(), pass = PasswordBox.Password;
            string proto = RadioTelnet.IsChecked == true ? "Telnet" : "SSH";
            if (string.IsNullOrEmpty(host)) { System.Windows.MessageBox.Show("Введите адрес хоста"); return; }
            if (!int.TryParse(portText, out int port)) { System.Windows.MessageBox.Show("Неверный порт"); return; }
            await CreateSessionTab(host, port, proto, user, pass);
        }

        private async System.Threading.Tasks.Task CreateSessionTab(string host, int port, string protocol, string user = "", string pass = "")
        {
            var (tab, closeBtn) = CreateTabHeader($"[{protocol}] {host}:{port}");
            if (protocol == "SSH")
            {
                var term = new Controls.SshTerminalView(); tab.Content = term;
                closeBtn.Click += (_, _) => { term.Disconnect(); SessionsTabControl.Items.Remove(tab); };
                tab.Unloaded += (_, _) => term.Disconnect();
                SessionsTabControl.Items.Add(tab); SessionsTabControl.SelectedItem = tab;
                try { await term.ConnectAsync(host, port == 0 ? 22 : port, user, pass); }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Ошибка: {ex.Message}"); SessionsTabControl.Items.Remove(tab); }
            }
            else
            {
                var term = new Controls.TerminalView(); tab.Content = term;
                closeBtn.Click += (_, _) => { term.Disconnect(); SessionsTabControl.Items.Remove(tab); };
                tab.Unloaded += (_, _) => term.Disconnect();
                SessionsTabControl.Items.Add(tab); SessionsTabControl.SelectedItem = tab;
                try { await term.ConnectAsync(host, port == 0 ? 23 : port); }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Ошибка: {ex.Message}"); SessionsTabControl.Items.Remove(tab); }
            }
        }

        private async System.Threading.Tasks.Task CreateSerialTab(string portName)
        {
            var (tab, closeBtn) = CreateTabHeader($"[Serial] {portName}");
            var term = new Controls.SerialTerminalView(); tab.Content = term;
            closeBtn.Click += (_, _) => { term.Disconnect(); SessionsTabControl.Items.Remove(tab); };
            tab.Unloaded += (_, _) => term.Disconnect();
            SessionsTabControl.Items.Add(tab); SessionsTabControl.SelectedItem = tab;
            var p = _serialConfig.Parity switch { "Odd" => Parity.Odd, "Even" => Parity.Even, _ => Parity.None };
            var sb = _serialConfig.StopBits == "Two" ? StopBits.Two : StopBits.One;
            try { await term.ConnectAsync(portName, _serialConfig.BaudRate, _serialConfig.DataBits, p, sb); }
            catch (Exception ex) { System.Windows.MessageBox.Show($"Ошибка: {ex.Message}"); SessionsTabControl.Items.Remove(tab); }
        }

        private static (TabItem tab, System.Windows.Controls.Button closeBtn) CreateTabHeader(string title)
        {
            var header = new DockPanel();
            var text = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
            var btn = new System.Windows.Controls.Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            DockPanel.SetDock(btn, Dock.Right);
            header.Children.Add(btn);
            header.Children.Add(text);
            return (new TabItem { Header = header }, btn);
        }

        private void Protocol_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            bool s = RadioSerial.IsChecked == true;
            SerialSettingsBtn.Visibility = s ? Visibility.Visible : Visibility.Collapsed;
            HostLabel.Text = s ? "COM:" : "Хост:";
            PortLabel.Visibility = PortBox.Visibility = s ? Visibility.Collapsed : Visibility.Visible;
            UsernameBox.Visibility = PasswordBox.Visibility = LoginLabel.Visibility = PasswordLabel.Visibility = s ? Visibility.Collapsed : Visibility.Visible;
            if (s) HostBox.Text = "COM3";
            else if (RadioSSH.IsChecked == true) { HostBox.Text = ""; PortBox.Text = "22"; }
            else { HostBox.Text = "192.168.1.1"; PortBox.Text = "23"; }
        }

        private void SerialSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SerialSettingsDialog(_serialConfig.BaudRate, _serialConfig.DataBits,
                _serialConfig.Parity switch { "Odd" => Parity.Odd, "Even" => Parity.Even, _ => Parity.None },
                _serialConfig.StopBits == "Two" ? StopBits.Two : StopBits.One);
            if (dlg.ShowDialog() == true)
            {
                _serialConfig.BaudRate = dlg.BaudRate; _serialConfig.DataBits = dlg.DataBits;
                _serialConfig.Parity = dlg.Parity.ToString(); _serialConfig.StopBits = dlg.StopBits == StopBits.Two ? "Two" : "One";
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog("🐸 Новая папка", "Введите имя папки:") { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                var folder = new TreeItem { Name = dlg.Result, IsFolder = true };
                if (TreeView.SelectedItem is TreeViewItem tvi && tvi.Tag is TreeItem sel && sel.IsFolder) sel.Children.Add(folder);
                else _treeItems[0].Children.Add(folder);
                RefreshTreeView(); HostStorageService.Save(_treeItems);
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (TreeView.SelectedItem is TreeViewItem tvi && tvi.Tag is TreeItem sel)
            { RemoveFromParent(_treeItems[0], sel); RefreshTreeView(); HostStorageService.Save(_treeItems); }
        }

        private void RenameItem(TreeItem target)
        {
            var cur = target.IsFolder ? target.Name : target.Name.Replace("[Telnet] ", "").Replace("[SSH] ", "").Replace("[Serial] ", "");
            var dlg = new InputDialog("🐸 Переименовать", target.IsFolder ? "Новое имя папки:" : "Новое имя подключения:") { Owner = this, Result = cur };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                if (!target.IsFolder) { string pfx = target.Name.StartsWith("[SSH]") ? "[SSH] " : target.Name.StartsWith("[Serial]") ? "[Serial] " : "[Telnet] "; target.Name = pfx + dlg.Result; target.HostData!.Name = dlg.Result; }
                else target.Name = dlg.Result;
                RefreshTreeView(); HostStorageService.Save(_treeItems);
            }
        }

        private static bool RemoveFromParent(TreeItem parent, TreeItem target)
        {
            if (parent.Children.Remove(target)) return true;
            foreach (var child in parent.Children) if (RemoveFromParent(child, target)) return true;
            return false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim(), host = HostBox.Text.Trim();
            if (string.IsNullOrEmpty(host) || !int.TryParse(PortBox.Text.Trim(), out int port)) return;
            string proto = RadioTelnet.IsChecked == true ? "Telnet" : RadioSSH.IsChecked == true ? "SSH" : "Serial";
            var item = new TreeItem { Name = $"[{proto}] {(string.IsNullOrEmpty(name) ? host : name)}", IsFolder = false, HostData = new SavedHost { Name = name, Host = host, Port = port, Protocol = proto, Username = UsernameBox.Text.Trim() } };
            if (TreeView.SelectedItem is TreeViewItem tvi && tvi.Tag is TreeItem sel && sel.IsFolder) sel.Children.Add(item);
            else _treeItems[0].Children.Add(item);
            RefreshTreeView(); HostStorageService.Save(_treeItems);
        }

        private async void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TreeView.SelectedItem is TreeViewItem tvi && tvi.Tag is TreeItem { IsFolder: false, HostData: not null } item)
            {
                var h = item.HostData;
                HostBox.Text = h.Host; PortBox.Text = h.Port.ToString(); UsernameBox.Text = h.Username; NameBox.Text = h.Name;
                if (h.Protocol == "SSH") RadioSSH.IsChecked = true; else if (h.Protocol == "Serial") RadioSerial.IsChecked = true; else RadioTelnet.IsChecked = true;
                if (h.Protocol == "Serial") await CreateSerialTab(h.Host);
                else await CreateSessionTab(h.Host, h.Port, h.Protocol, h.Username, PasswordBox.Password);
            }
        }
    }
}