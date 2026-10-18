using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

string src = args.Length > 0
    ? args[0]
    : @"e:\Games\2D Game\My project\ArtIncoming\Human\human.jfif";
string dest = args.Length > 1
    ? args[1]
    : @"e:\Games\2D Game\My project\ArtIncoming\Human\crops";
Directory.CreateDirectory(dest);

using Bitmap srcBmp = new(src);
int w = srcBmp.Width;
int h = srcBmp.Height;
Console.WriteLine($"sheet {w}x{h}");

var data = srcBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
int stride = Math.Abs(data.Stride);
byte[] px = new byte[stride * h];
Marshal.Copy(data.Scan0, px, 0, px.Length);
srcBmp.UnlockBits(data);

bool[] ink = new bool[w * h];
int InkAt(int x, int y)
{
    int o = y * stride + x * 4;
    byte b = px[o], g = px[o + 1], r = px[o + 2];
    return (r + g + b < 720 && !(r > 240 && g > 240 && b > 240)) ? 1 : 0;
}

for (int y = 0; y < h; y++)
{
    for (int x = 0; x < w; x++)
        ink[y * w + x] = InkAt(x, y) == 1;
}

int[] label = new int[w * h];
var boxes = new List<(int minx, int miny, int maxx, int maxy, int count)>();
var stack = new Stack<(int x, int y)>();

for (int y = 0; y < h; y++)
{
    for (int x = 0; x < w; x++)
    {
        int i = y * w + x;
        if (!ink[i] || label[i] != 0) continue;
        int id = boxes.Count + 1;
        int minx = x, maxx = x, miny = y, maxy = y, count = 0;
        stack.Push((x, y));
        label[i] = id;
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            count++;
            if (cx < minx) minx = cx;
            if (cx > maxx) maxx = cx;
            if (cy < miny) miny = cy;
            if (cy > maxy) maxy = cy;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (!ink[ni] || label[ni] != 0) continue;
                    label[ni] = id;
                    stack.Push((nx, ny));
                }
            }
        }
        boxes.Add((minx, miny, maxx, maxy, count));
    }
}

var keep = new List<(int idx, int minx, int miny, int maxx, int maxy, int count)>();
for (int i = 0; i < boxes.Count; i++)
{
    var b = boxes[i];
    int bw = b.maxx - b.minx + 1;
    int bh = b.maxy - b.miny + 1;
    if (b.count < 400 || bw < 12 || bh < 12) continue;
    keep.Add((i + 1, b.minx, b.miny, b.maxx, b.maxy, b.count));
}

keep.Sort((a, b) => a.miny != b.miny ? a.miny.CompareTo(b.miny) : a.minx.CompareTo(b.minx));
Console.WriteLine($"blobs {keep.Count}");

using Bitmap overview = new(srcBmp);
using var g = Graphics.FromImage(overview);
using var pen = new Pen(Color.Red, 2);
using var font = new Font("Consolas", 14, FontStyle.Bold);
using var brush = new SolidBrush(Color.Red);
for (int n = 0; n < keep.Count; n++)
{
    var b = keep[n];
    var rect = new Rectangle(b.minx, b.miny, b.maxx - b.minx + 1, b.maxy - b.miny + 1);
    g.DrawRectangle(pen, rect);
    g.DrawString(n.ToString(), font, brush, b.minx, Math.Max(0, b.miny - 18));
    Console.WriteLine($"{n}: {rect.Width}x{rect.Height} @ {b.minx},{b.miny} px={b.count}");

    int pad = 2;
    int x0 = Math.Max(0, b.minx - pad);
    int y0 = Math.Max(0, b.miny - pad);
    int x1 = Math.Min(w - 1, b.maxx + pad);
    int y1 = Math.Min(h - 1, b.maxy + pad);
    int cw = x1 - x0 + 1;
    int ch = y1 - y0 + 1;
    using Bitmap crop = new(cw, ch, PixelFormat.Format32bppArgb);
    for (int yy = 0; yy < ch; yy++)
    {
        for (int xx = 0; xx < cw; xx++)
        {
            int sx = x0 + xx;
            int sy = y0 + yy;
            int o = sy * stride + sx * 4;
            byte bb = px[o], gg = px[o + 1], rr = px[o + 2];
            bool white = rr > 232 && gg > 232 && bb > 232;
            crop.SetPixel(xx, yy, white
                ? Color.FromArgb(0, rr, gg, bb)
                : Color.FromArgb(255, rr, gg, bb));
        }
    }
    crop.Save(Path.Combine(dest, $"crop_{n:00}.png"), ImageFormat.Png);
}

overview.Save(Path.Combine(dest, "_overview.png"), ImageFormat.Png);
Console.WriteLine($"wrote {dest}");
