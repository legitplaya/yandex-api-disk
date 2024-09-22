using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace yandex_api_test
{
    class Program
    {
        private static readonly string telegramToken = "";
        private static readonly string yandexDiskToken = "";
        private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramToken);

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
            if (update.Message is Message message && message.Type == MessageType.Document)
            {
                await HandleDocumentMessage(message);
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

        private static async Task HandleDocumentMessage(Message message)
        {
            var fileId = message.Document.FileId;
            var file = await botClient.GetFileAsync(fileId);

            var filePath = file.FilePath;
            var fileUrl = $"https://api.telegram.org/file/bot{telegramToken}/{filePath}";

            var localFilePath = Path.Combine(Path.GetTempPath(), message.Document.FileName);
            using (var httpClient = new HttpClient())
            {
                var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                await System.IO.File.WriteAllBytesAsync(localFilePath, fileBytes);
            }

            Console.WriteLine($"Файл {message.Document.FileName} скачан и сохранен в {localFilePath}");

            await UploadToYandexDisk(localFilePath, message.Document.FileName);

            System.IO.File.Delete(localFilePath);
            Console.WriteLine("Временный файл удален");
        }

        private static async Task UploadToYandexDisk(string localFilePath, string fileName)
        {
            using (var httpClient = new HttpClient())
            {
                var uploadUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={fileName}&overwrite=true";
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
                        Console.WriteLine($"Файл {fileName} успешно загружен на Yandex Disk.");
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка загрузки файла на Yandex Disk: {uploadResponse.StatusCode}");
                    }
                }
            }
        }

        private class UploadLinkResponse
        {
            public string Href { get; set; }
            public string Method { get; set; }
            public bool Templated { get; set; }
        }
    }
}