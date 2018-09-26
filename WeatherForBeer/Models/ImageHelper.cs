using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using SixLabors.Fonts;

// pre-release packages!
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace WeatherForBeer
{
    public class ImageHelper
    {
        public static Stream AddTextToImage(Stream imageStream, params (string text, (float x, float y) position)[] texts)
        {
            var memoryStream = new MemoryStream();

            var image = Image.Load(imageStream);

            image
                .Clone(img =>
                {
                    foreach (var (text, (x, y)) in texts)
                    {
                        img.DrawText(text, SystemFonts.CreateFont("Calibri",24), Rgba32.Navy, new PointF(x, y));
                    }
                })
                .SaveAsPng(memoryStream);

            memoryStream.Position = 0;

            return memoryStream;
        }
    }
}
