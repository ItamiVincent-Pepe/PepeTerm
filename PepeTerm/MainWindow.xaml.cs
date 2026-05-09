using System;
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
        private TreeItem? _draggedItem;

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
                        SessionsTabControl.Items.Remove(tabItem);
                    e.Handled = true;
                }
            };

            // Иконка в трее
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
                if (WindowState == WindowState.Minimized) { Hide(); _trayIcon!.Visible = true; }
            };
        }

        private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); _trayIcon!.Visible = false; }

        // ===== ДЕРЕВО =====

        private TreeViewItem CreateTreeViewItem(TreeItem data)
        {
            string icon = data.HostData?.Protocol switch { "SSH" => "🔒", _ => "🖧" };
            var lightGray = System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC);
            var darkBg = System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x2D);

            var item = new TreeViewItem
            {
                Header = data.IsFolder ? $"📁 {data.Name}" : $"{icon} {data.Name}",
                Tag = data,
                Foreground = new System.Windows.Media.SolidColorBrush(lightGray),
                Background = System.Windows.Media.Brushes.Transparent,
                IsExpanded = data.IsFolder
            };

            var menu = new System.Windows.Controls.ContextMenu();
            var rename = new System.Windows.Controls.MenuItem
            {
                Header = "✏️ Переименовать",
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(darkBg)
            };
            rename.Click += (_, _) => RenameItem(data);

            var delete = new System.Windows.Controls.MenuItem
            {
                Header = "🗑 Удалить",
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(darkBg)
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

        // ===== ПОДКЛЮЧЕНИЕ =====

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string protocol = RadioTelnet.IsChecked == true ? "Telnet" : "SSH";
            string host = HostBox.Text.Trim();
            _ = int.TryParse(PortBox.Text.Trim(), out int port);
            if (string.IsNullOrEmpty(host)) { System.Windows.MessageBox.Show("Введите адрес хоста"); return; }
            await CreateTerminalTab(protocol, host, port, UsernameBox.Text.Trim());
        }

        private async System.Threading.Tasks.Task CreateTerminalTab(string protocol, string host, int port, string username = "", string _ = "")
        {
            var header = new DockPanel();
            var text = new TextBlock { Text = $"[{protocol}] {host}" + (port > 0 ? $":{port}" : ""), VerticalAlignment = VerticalAlignment.Center };

            var closeBtn = new System.Windows.Controls.Button
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
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                ToolTip = "Закрыть вкладку"
            };

            DockPanel.SetDock(closeBtn, Dock.Right);
            header.Children.Add(closeBtn);
            header.Children.Add(text);

            var tab = new TabItem { Header = header };
            var term = new Controls.PlinkTerminal();
            tab.Content = term;

            closeBtn.Click += (_, _) => { term.Disconnect(); SessionsTabControl.Items.Remove(tab); };
            tab.Unloaded += (_, _) => term.Disconnect();

            SessionsTabControl.Items.Add(tab);
            SessionsTabControl.SelectedItem = tab;

            try
            {
                await term.ConnectAsync(protocol, host, port, username, PasswordBox.Password);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
                SessionsTabControl.Items.Remove(tab);
            }
        }

        private void Protocol_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            if (RadioSSH.IsChecked == true)
            {
                if (string.IsNullOrEmpty(HostBox.Text)) HostBox.Text = "";
                PortBox.Text = "22";
            }
            else
            {
                if (string.IsNullOrEmpty(HostBox.Text)) HostBox.Text = "192.168.1.1";
                PortBox.Text = "23";
            }
        }

        // ===== ДИАЛОГИ =====

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
                if (!target.IsFolder) { string pfx = target.Name.StartsWith("[SSH]") ? "[SSH] " : "[Telnet] "; target.Name = pfx + dlg.Result; target.HostData!.Name = dlg.Result; }
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
            string proto = RadioTelnet.IsChecked == true ? "Telnet" : "SSH";
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
                if (h.Protocol == "SSH") RadioSSH.IsChecked = true; else RadioTelnet.IsChecked = true;
                await CreateTerminalTab(h.Protocol, h.Host, h.Port, h.Username, PasswordBox.Password);
            }
        }

        // ===== DRAG & DROP =====

        private void TreeView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && TreeView.SelectedItem is TreeViewItem tvi && tvi.Tag is TreeItem item)
            {
                _draggedItem = item;
                System.Windows.DragDrop.DoDragDrop(TreeView, item, System.Windows.DragDropEffects.Move);
            }
        }
        private void TreeView_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(TreeItem)) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }
        private void TreeView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (_draggedItem is null) return;
            var hitResult = VisualTreeHelper.HitTest(TreeView, e.GetPosition(TreeView));
            if (hitResult?.VisualHit is not null)
            {
                var el = hitResult.VisualHit;
                while (el is not null)
                {
                    if (el is TreeViewItem tvi && tvi.Tag is TreeItem data)
                    {
                        var target = data.IsFolder ? data : FindParentFolder(_treeItems[0], data);
                        if (target is not null && target != _draggedItem)
                        {
                            RemoveFromParent(_treeItems[0], _draggedItem);
                            target.Children.Add(_draggedItem);
                            RefreshTreeView();
                            HostStorageService.Save(_treeItems);
                        }
                        break;
                    }
                    el = VisualTreeHelper.GetParent(el);
                }
            }
            _draggedItem = null;
        }
        private static TreeItem? FindParentFolder(TreeItem root, TreeItem child)
        {
            foreach (var item in root.Children)
            {
                if (item == child) return root;
                if (item.IsFolder) { var f = FindParentFolder(item, child); if (f is not null) return f; }
            }
            return null;
        }
    }
}