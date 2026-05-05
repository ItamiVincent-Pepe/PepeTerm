namespace PepeTerm.Models
{
    /// <summary>
    /// Конфигурация COM-порта для Serial-подключения.
    /// Хранит параметры: скорость, биты данных, чётность, стоп-биты.
    /// </summary>
    public class SerialConfig
    {
        /// <summary>Скорость передачи данных в бодах (по умолчанию 9600 — стандарт Cisco)</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>Количество бит данных (7 или 8, по умолчанию 8)</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>Чётность: "None", "Odd", "Even" (по умолчанию None)</summary>
        public string Parity { get; set; } = "None";

        /// <summary>Стоп-биты: "One" или "Two" (по умолчанию One)</summary>
        public string StopBits { get; set; } = "One";
    }
}