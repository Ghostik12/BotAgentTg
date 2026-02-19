using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace ParserFlightTickets.Services.Telegram
{
    public static class ImageHelper
    {
        
         private static readonly string[] BackgroundImages = Directory.GetFiles("images", "city.jpg")
        .Concat(Directory.GetFiles("images", "*.png"))
        .ToArray();

        public static byte[] AddTextToLocalImage(string text, string cityName = null)
        {
            try
            {
                if (BackgroundImages.Length == 0)
                {
                    throw new FileNotFoundException("Нет фоновых картинок в папке images");
                }

                // Выбираем случайную картинку
                string randomBg = BackgroundImages[new Random().Next(BackgroundImages.Length)];

                using var bitmap = new Bitmap(randomBg);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Шрифт и цвет (можно настроить)
                using var font = new Font("Arial", 48, FontStyle.Bold);
                using var brush = new SolidBrush(Color.White);
                using var outlineBrush = new SolidBrush(Color.Black); // для обводки

                // Текст
                string overlayText = $"{cityName ?? ""}\n{text}";

                // Измеряем размер текста
                var textSize = graphics.MeasureString(overlayText, font);

                // Позиция — внизу по центру
                float x = (bitmap.Width - textSize.Width) / 2;
                float y = bitmap.Height - textSize.Height - 60;

                // Обводка (чёрная тень)
                for (int dx = -2; dx <= 2; dx += 2)
                    for (int dy = -2; dy <= 2; dy += 2)
                    {
                        if (dx == 0 && dy == 0) continue;
                        graphics.DrawString(overlayText, font, outlineBrush, x + dx, y + dy);
                    }

                // Основной текст
                graphics.DrawString(overlayText, font, brush, x, y);

                // Сохраняем в память и возвращаем байты
                using var memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, ImageFormat.Jpeg);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка наложения текста: {ex.Message}");
                return null; // fallback — без фото
            }
        }
    }
}
