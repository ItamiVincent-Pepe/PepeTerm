using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using PepeTerm.Models;

namespace PepeTerm.Services
{
    /// <summary>
    /// Сервис для сохранения и загрузки списка хостов в JSON-файл.
    /// Файл хранится в %LocalAppData%\PepeTerm\pepehosts.json
    /// </summary>
    public static class HostStorageService
    {
        /// <summary>Путь к файлу с сохранёнными хостами</summary>
        private static readonly string SaveFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PepeTerm",
            "pepehosts.json");

        /// <summary>Настройки JSON-сериализации (красивый формат с отступами)</summary>
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Сохраняет список хостов в JSON-файл.
        /// Автоматически создаёт папку, если её нет.
        /// </summary>
        /// <param name="treeItems">Список элементов дерева для сохранения</param>
        public static void Save(ObservableCollection<TreeItem> treeItems)
        {
            try
            {
                // Создаём папку PepeTerm, если её ещё нет
                var dir = Path.GetDirectoryName(SaveFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir!);

                // Сериализуем в JSON и записываем в файл
                var json = JsonSerializer.Serialize(treeItems, JsonOptions);
                File.WriteAllText(SaveFilePath, json);
            }
            catch
            {
                // Ничего не делаем — файл не сохранился, но программа не падает
            }
        }

        /// <summary>
        /// Загружает список хостов из JSON-файла.
        /// Если файла нет или он повреждён — возвращает пустой список.
        /// </summary>
        /// <returns>Список элементов дерева из файла</returns>
        public static ObservableCollection<TreeItem> Load()
        {
            try
            {
                // Если файла нет — возвращаем пустой список
                if (!File.Exists(SaveFilePath))
                    return [];

                // Читаем JSON и десериализуем
                var json = File.ReadAllText(SaveFilePath);
                var items = JsonSerializer.Deserialize<ObservableCollection<TreeItem>>(json);

                return items ?? [];
            }
            catch
            {
                // Файл повреждён или не читается — начинаем с чистого листа
                return [];
            }
        }
    }
}