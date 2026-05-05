using System.Windows;

namespace PepeTerm.Dialogs
{
    /// <summary>
    /// Простой диалог ввода текста (используется для создания папок, переименования).
    /// </summary>
    public partial class InputDialog : Window
    {
        /// <summary>Результат ввода (то, что ввёл пользователь)</summary>
        public string Result
        {
            get => InputBox.Text;
            set => InputBox.Text = value;
        }

        /// <summary>
        /// Создаёт диалог с заголовком и подписью
        /// </summary>
        public InputDialog(string title, string label)
        {
            InitializeComponent();
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pepe.ico");
            if (System.IO.File.Exists(iconPath))
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            Owner = System.Windows.Application.Current.MainWindow;
            Title = title;
            LabelBlock.Text = label;
            InputBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}