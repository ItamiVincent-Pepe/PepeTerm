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