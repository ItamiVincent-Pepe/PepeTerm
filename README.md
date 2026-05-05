# 🐸 PepeTerm

**PepeTerm** — мощный эмулятор терминала для сетевого оборудования.  
Бесплатная open-source альтернатива MobaXterm и SecureCRT на WPF/.NET 10.

---

## 🚀 Возможности

- [x] Подключение по **Telnet** с ANSI-обработкой (стрелки, Backspace, escape-коды)
- [x] Подключение по **SSH** (ssh2, shell-сессия)
- [x] Подключение по **Serial / COM-порт** (RS-232, настройки baud rate, parity, stop bits)
- [x] Вкладки с крестиком и горячей клавишей `Ctrl+D`
- [x] Боковая панель с деревом сохранённых хостов
- [x] Папки и вложенные папки
- [x] Сохранение и загрузка хостов в JSON-файл
- [x] Иконки протоколов в дереве: 🖧 Telnet, 🔒 SSH, 🔌 Serial
- [x] Иконка в системном трее (сворачивание вместо закрытия)
- [x] Тёмная тема в отдельном файле стилей
- [x] Чистая архитектура: Dialogs, Models, Services, Controls, Styles
- [x] XML-комментарии к коду

---

## 🛠 Технологии

| Технология | Назначение |
|------------|------------|
| .NET 10 | Платформа |
| WPF | Интерфейс |
| SSH.NET | SSH-подключения |
| System.IO.Ports | COM-порт |
| System.Windows.Forms | Иконка в трее |

---

## 📦 Установка

### Требования

- Windows 10/11
- .NET 10 SDK

### Сборка

```bash
git clone https://github.com/ItamiVincent-Pepe/PepeTerm.git
cd PepeTerm
dotnet restore
dotnet build
dotnet run


## 📁 Структура проекта

PepeTerm/
├── Controls/         # Терминалы (Telnet, SSH, Serial)
├── Dialogs/          # Диалоговые окна
├── Models/           # Классы данных
├── Services/         # Сохранение/загрузка JSON
├── Styles/           # Тёмная тема
├── MainWindow.xaml   # Главное окно
└── MainWindow.xaml.cs

MIT © 2026 PepeTerm