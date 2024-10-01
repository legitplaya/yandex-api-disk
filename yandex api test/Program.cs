using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace yandex_api_test
{
    class Program
    {
        private static readonly string telegramToken = "";
        private static readonly string yandexDiskToken = "";
        private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramToken);

        // Словарь для хранения выбранной папки для каждого пользователя
        private static Dictionary<long, string> userFolders = new Dictionary<long, string>();

        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущен... Нажмите любую клавишу для завершения.");
            Console.ReadLine();

            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message != null)
            {
                var message = update.Message;

                // Проверяем тип сообщения
                if (message.Text == "/start")
                {
                    await HandleStartCommand(message);
                }
                else if (message.Type == MessageType.Photo)
                {
                    await HandlePhotoMessage(message);
                }
                else if (message.Type == MessageType.Video)
                {
                    await HandleVideoMessage(message);
                }
                else if (message.Type == MessageType.Document)
                {
                    await HandleDocumentMessage(message);
                }
                else if (message.Type == MessageType.Animation)
                {
                    await HandleGifMessage(message);
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Отправь мне файл");
                }
            }
        }
        private static async Task HandleStartCommand(Message message)
        {
            var chatId = message.Chat.Id;

            // Приветственное сообщение и описание функций
            var welcomeText = @"Привет! Я бот для загрузки файлов на Яндекс.Диск.
Вот мои функции:
1. Вы можете отправить фотографию, видео, GIF или документ, и я загружу их на Яндекс.Диск.
2. Поддерживаются следующие типы файлов:
   - Фотографии
   - Видео
   - Анимации (GIF)
   - Документы
3. Я загружу ваши файлы в корневую папку.
   
Просто отправьте мне файл, и я загружу на Яндекс.Диск!";

            await botClient.SendTextMessageAsync(chatId, welcomeText);
        }
        private static async Task HandlePhotoMessage(Message message)
        {
            var chatId = message.Chat.Id;

            await botClient.SendTextMessageAsync(chatId, "Подождите, идет загрузка вашей фотографии...");

            // Проверяем, установлена ли папка для пользователя, если нет — по умолчанию корневая
            if (!userFolders.ContainsKey(chatId))
            {
                userFolders[chatId] = "/"; // Корневая папка по умолчанию
            }

            var folder = userFolders[chatId]; // Получаем папку пользователя

            // Обработка фотографий
            if (message.Photo != null && message.Photo.Length > 0)
            {
                var photo = message.Photo.Last(); // Берем фотографию с наибольшим разрешением
                var fileId = photo.FileId;
                var file = await botClient.GetFileAsync(fileId);

                var fileUrl = $"https://api.telegram.org/file/bot{telegramToken}/{file.FilePath}";

                // Загружаем фотографию напрямую на Яндекс.Диск
                using (var httpClient = new HttpClient())
                {
                    using (var fileStream = await httpClient.GetStreamAsync(fileUrl))
                    {
                        await UploadStreamToYandexDisk(fileStream, folder, "photo_" + photo.FileUniqueId + ".jpg");
                    }
                }
                await botClient.SendTextMessageAsync(chatId, "Фотография успешно загружена на Яндекс Диск.");
                Console.WriteLine($"Фотография загружена на Яндекс.Диск в папку {folder}.");
            }
        }
        private static async Task HandleGifMessage(Message message)
        {
            var chatId = message.Chat.Id;

            // Уведомляем пользователя о начале загрузки GIF
            await botClient.SendTextMessageAsync(chatId, "Подождите, идет загрузка вашего GIF...");

            // Проверяем, установлена ли папка для пользователя, если нет — по умолчанию корневая
            if (!userFolders.ContainsKey(chatId))
            {
                userFolders[chatId] = "/"; // Корневая папка по умолчанию
            }

            var folder = userFolders[chatId]; // Получаем папку пользователя

            // Обработка GIF-анимаций
            if (message.Animation != null)
            {
                var gif = message.Animation;
                var fileId = gif.FileId;
                var file = await botClient.GetFileAsync(fileId);

                var fileUrl = $"https://api.telegram.org/file/bot{telegramToken}/{file.FilePath}";

                // Загружаем GIF напрямую на Яндекс.Диск
                using (var httpClient = new HttpClient())
                {
                    using (var fileStream = await httpClient.GetStreamAsync(fileUrl))
                    {
                        await UploadStreamToYandexDisk(fileStream, folder, "gif_" + gif.FileUniqueId + ".gif");
                    }
                }

                // Уведомляем пользователя, что GIF загружен
                await botClient.SendTextMessageAsync(chatId, "GIF успешно загружен на Яндекс.Диск.");
            }
        }
        private static async Task HandleVideoMessage(Message message)
        {
            var chatId = message.Chat.Id;

            await botClient.SendTextMessageAsync(chatId, "Подождите, идет загрузка вашего видео...");
            // Проверяем, установлена ли папка для пользователя, если нет — по умолчанию корневая
            if (!userFolders.ContainsKey(chatId))
            {
                userFolders[chatId] = "/"; // Корневая папка по умолчанию
            }

            var folder = userFolders[chatId]; // Получаем папку пользователя

            // Обработка видео
            if (message.Video != null)
            {
                var video = message.Video;
                var fileId = video.FileId;
                var file = await botClient.GetFileAsync(fileId);

                var fileUrl = $"https://api.telegram.org/file/bot{telegramToken}/{file.FilePath}";

                // Загружаем видео напрямую на Яндекс.Диск
                using (var httpClient = new HttpClient())
                {
                    using (var fileStream = await httpClient.GetStreamAsync(fileUrl))
                    {
                        await UploadStreamToYandexDisk(fileStream, folder, "video_" + video.FileUniqueId + ".mp4");
                    }
                }
                await botClient.SendTextMessageAsync(chatId, "Видео успешно загружено на Яндекс Диск.");
                Console.WriteLine($"Видео загружено на Яндекс.Диск в папку {folder}.");
            }
        }
        private static async Task HandleDocumentMessage(Message message)
        {
            var chatId = message.Chat.Id;
            await botClient.SendTextMessageAsync(chatId, "Подождите, идет загрузка вашего документа...");

            // Проверяем, установлена ли папка для пользователя, если нет — по умолчанию корневая
            if (!userFolders.ContainsKey(chatId))
            {
                userFolders[chatId] = "/"; // Корневая папка по умолчанию
            }

            var folder = userFolders[chatId]; // Получаем папку пользователя

            // Обработка документов
            if (message.Document != null)
            {
                var document = message.Document;
                var fileId = document.FileId;
                var file = await botClient.GetFileAsync(fileId);

                var fileUrl = $"https://api.telegram.org/file/bot{telegramToken}/{file.FilePath}";

                // Загружаем документ напрямую на Яндекс.Диск
                using (var httpClient = new HttpClient())
                {
                    using (var fileStream = await httpClient.GetStreamAsync(fileUrl))
                    {
                        await UploadStreamToYandexDisk(fileStream, folder, document.FileName);
                    }
                }
                await botClient.SendTextMessageAsync(chatId, "Документ успешно загружен на Яндекс Диск.");
                Console.WriteLine($"Документ загружен на Яндекс.Диск в папку {folder}.");
            }
        }
        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }

        private static async Task HandleTextMessage(Message message)
        {
            var chatId = message.Chat.Id;

            // Команда /start приветствует пользователя и предлагает команды
            if (message.Text == "/start")
            {
                await botClient.SendTextMessageAsync(chatId, "Добро пожаловать! Отправьте документ");
            }
        }

        private static async Task UploadStreamToYandexDisk(Stream fileStream, string folder, string fileName)
        {
            using (var httpClient = new HttpClient())
            {
                // Если выбранная папка - корневая, не добавляем её в путь
                var uploadPath = folder == "/" ? fileName : $"{folder}/{fileName}";
                var uploadUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={uploadPath}&overwrite=true";
                httpClient.DefaultRequestHeaders.Add("Authorization", $"OAuth {yandexDiskToken}");

                // Запрашиваем ссылку для загрузки на Яндекс.Диск
                var response = await httpClient.GetAsync(uploadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Не удалось получить ссылку для загрузки: {response.StatusCode}");
                    return;
                }

                var uploadLink = await response.Content.ReadAsAsync<UploadLinkResponse>();

                // Загружаем поток файла напрямую на Яндекс.Диск
                using (var uploadStream = new StreamContent(fileStream))
                {
                    var uploadResponse = await httpClient.PutAsync(uploadLink.Href, uploadStream);
                    if (uploadResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Файл {fileName} успешно загружен в папку {folder} на Yandex Disk.");
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка загрузки файла на Yandex Disk: {uploadResponse.StatusCode}");
                    }
                }
            }
        }

        private static async Task UploadToYandexDisk(string localFilePath, string folder, string fileName)
        {
            using (var httpClient = new HttpClient())
            {
                // Если выбранная папка - корневая, не добавляем её в путь
                var uploadPath = folder == "/" ? fileName : $"{folder}/{fileName}";
                var uploadUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={uploadPath}&overwrite=true";
                httpClient.DefaultRequestHeaders.Add("Authorization", $"OAuth {yandexDiskToken}");

                var response = await httpClient.GetAsync(uploadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Не удалось получить ссылку для загрузки: {response.StatusCode}");
                    return;
                }

                var uploadLink = await response.Content.ReadAsAsync<UploadLinkResponse>();

                using (var fileStream = new FileStream(localFilePath, FileMode.Open))
                {
                    var uploadResponse = await httpClient.PutAsync(uploadLink.Href, new StreamContent(fileStream));
                    if (uploadResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Файл {fileName} успешно загружен в папку {folder} на Yandex Disk.");
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка загрузки файла на Yandex Disk: {uploadResponse.StatusCode}");
                    }
                }
            }
        }

        private class YandexDiskFolderResponse
        {
            public YandexDiskEmbedded Embedded { get; set; }
        }

        private class YandexDiskEmbedded
        {
            public List<YandexDiskItem> Items { get; set; }
        }

        private class YandexDiskItem
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        private class UploadLinkResponse
        {
            public string Href { get; set; }
            public string Method { get; set; }
            public bool Templated { get; set; }
        }
    }
}