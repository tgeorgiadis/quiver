using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: IcoGen <input.png> <output.ico>");
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input not found: {inputPath}");
    return 1;
}

using var source = new Bitmap(inputPath);
int[] sizes = [16, 32, 48, 64, 128, 256];
var pngImages = new List<byte[]>(sizes.Length);

foreach (var size in sizes)
{
    using var square = RenderSquareIcon(source, size);
    using var ms = new MemoryStream();
    square.Save(ms, ImageFormat.Png);
    pngImages.Add(ms.ToArray());
}

WriteIco(outputPath, sizes, pngImages);
Console.WriteLine($"Wrote {outputPath} ({sizes.Length} sizes from {source.Width}x{source.Height})");
return 0;

static Bitmap RenderSquareIcon(Bitmap source, int size)
{
    var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(result);
    graphics.Clear(Color.Transparent);
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.SmoothingMode = SmoothingMode.HighQuality;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    graphics.CompositingQuality = CompositingQuality.HighQuality;

    var scale = Math.Min((float)size / source.Width, (float)size / source.Height) * 0.92f;
    var drawWidth = source.Width * scale;
    var drawHeight = source.Height * scale;
    var x = (size - drawWidth) / 2f;
    var y = (size - drawHeight) / 2f;
    graphics.DrawImage(source, x, y, drawWidth, drawHeight);
    return result;
}

static void WriteIco(string path, IReadOnlyList<int> sizes, IReadOnlyList<byte[]> pngImages)
{
    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(stream);

    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Count);

    var dataOffset = 6 + (16 * sizes.Count);
    for (var i = 0; i < sizes.Count; i++)
    {
        var size = sizes[i];
        var dimension = (byte)(size >= 256 ? 0 : size);
        writer.Write(dimension);
        writer.Write(dimension);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(pngImages[i].Length);
        writer.Write(dataOffset);
        dataOffset += pngImages[i].Length;
    }

    foreach (var png in pngImages)
    {
        writer.Write(png);
    }
}
