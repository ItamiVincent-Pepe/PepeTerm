//using System.IO.Ports;
//using System.Windows;

//namespace PepeTerm.Dialogs
//{
//    public partial class SerialSettingsDialog : Window
//    {
//        // Результаты настроек (заполняются при нажатии OK)
//        public int BaudRate { get; private set; }
//        public int DataBits { get; private set; }
//        public Parity Parity { get; private set; }
//        public StopBits StopBits { get; private set; }

//        /// <summary>
//        /// Диалог настроек COM-порта
//        /// </summary>
//        public SerialSettingsDialog(int baudRate, int dataBits, Parity parity, StopBits stopBits)
//        {
//            InitializeComponent();
//            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pepe.ico");
//            if (System.IO.File.Exists(iconPath))
//                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
//            Owner = System.Windows.Application.Current.MainWindow; // Привязываем к главному окну

//            // Заполняем выпадающие списки значениями
//            BaudRateBox.Items.Add(9600);
//            BaudRateBox.Items.Add(19200);
//            BaudRateBox.Items.Add(38400);
//            BaudRateBox.Items.Add(57600);
//            BaudRateBox.Items.Add(115200);
//            BaudRateBox.SelectedItem = baudRate; // Выбираем текущее значение

//            DataBitsBox.Items.Add(7);
//            DataBitsBox.Items.Add(8);
//            DataBitsBox.SelectedItem = dataBits;

//            ParityBox.Items.Add("None");
//            ParityBox.Items.Add("Odd");
//            ParityBox.Items.Add("Even");
//            ParityBox.SelectedItem = parity.ToString();

//            StopBitsBox.Items.Add("One");
//            StopBitsBox.Items.Add("Two");
//            StopBitsBox.SelectedItem = stopBits == StopBits.One ? "One" : "Two";
//        }

//        /// <summary>
//        /// Кнопка OK — сохраняем выбранные значения и закрываем окно
//        /// </summary>
//        private void OkButton_Click(object sender, RoutedEventArgs e)
//        {
//            BaudRate = (int)BaudRateBox.SelectedItem;
//            DataBits = (int)DataBitsBox.SelectedItem;
//            Parity = ParityBox.SelectedItem.ToString() switch
//            {
//                "Odd" => Parity.Odd,
//                "Even" => Parity.Even,
//                _ => Parity.None
//            };
//            StopBits = StopBitsBox.SelectedItem.ToString() == "Two" ? StopBits.Two : StopBits.One;

//            DialogResult = true; // Успех
//            Close();
//        }

//        /// <summary>
//        /// Кнопка Отмена — закрываем без сохранения
//        /// </summary>
//        private void CancelButton_Click(object sender, RoutedEventArgs e)
//        {
//            DialogResult = false; // Отмена
//            Close();
//        }

//        private void BaudRateBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
//        {

//        }

//        private void StopBitsBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
//        {

//        }
//    }
//}