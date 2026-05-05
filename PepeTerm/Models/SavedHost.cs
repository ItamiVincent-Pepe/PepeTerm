namespace PepeTerm.Models
{
    /// <summary>
    /// Модель данных для сохранённого подключения.
    /// Хранит всю информацию о хосте: адрес, порт, протокол, учётные данные.
    /// </summary>
    public class SavedHost
    {
        /// <summary>Человеческое имя подключения (например, "Коммутатор в гараже")</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>IP-адрес или COM-порт (например, "192.168.1.1" или "COM3")</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>Порт подключения (23 — Telnet, 22 — SSH, 0 — для Serial)</summary>
        public int Port { get; set; }

        /// <summary>Протокол: "Telnet", "SSH" или "Serial"</summary>
        public string Protocol { get; set; } = "Telnet";

        /// <summary>Имя пользователя (для SSH/Telnet)</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Отображение в списке: [Протокол] Имя (Хост:Порт)</summary>
        public override string ToString() => $"[{Protocol}] {Name} ({Host}:{Port})";
    }
}