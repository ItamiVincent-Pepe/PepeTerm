using System.Collections.ObjectModel;

namespace PepeTerm.Models
{
    /// <summary>
    /// Элемент дерева сохранённых подключений в боковой панели.
    /// Может быть папкой (содержит другие TreeItem) или подключением (содержит SavedHost).
    /// </summary>
    public class TreeItem
    {
        /// <summary>Имя папки или метка подключения (например, "Дача" или "[Telnet] Коммутатор")</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>true — это папка (может содержать вложенные элементы), false — это подключение</summary>
        public bool IsFolder { get; set; }

        /// <summary>Данные подключения (заполнено только если IsFolder == false)</summary>
        public SavedHost? HostData { get; set; }

        /// <summary>Вложенные элементы (папки или подключения внутри этой папки)</summary>
        public ObservableCollection<TreeItem> Children { get; set; } = [];
    }
}