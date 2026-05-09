using System.Threading.Tasks;
using System.Windows;

namespace PepeTerm.Dialogs
{
    /// <summary>
    /// Окно загрузки PepeTerm.
    /// Показывается на 3 секунды при запуске приложения.
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>Показывает сплеш-скрин и ждёт 3 секунды</summary>
        public async Task ShowAndWaitAsync()
        {
            Show();
            await Task.Delay(3000); // 3 секунды
            Close();
        }
    }
}