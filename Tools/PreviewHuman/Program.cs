// Preview the human with the same recipe as HumanSegment + Human. Not physics.
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

string root = FindProjectRoot();
string art = Path.Combine(root, "Assets", "Resources", "Art");
string dest = Path.Combine(root, "Tools", "preview-human.png");

const float Ppm = 360f;
const int Soft = 128;

float[] Skin = { 0.92f, 0.74f, 0.60f };
float[] Shirt = { 0.24f, 0.46f, 0.78f };
float[] Pants = { 0.20f, 0.30f, 0.50f };
float[] Socks = { 0.84f, 0.84f, 0.88f };
float[] Shoes = { 0.22f, 0.14f, 0.12f };
float[] Neck = { 0.86f, 0.68f, 0.55f };
float[] Sky = { 0.07f, 0.09f, 0.16f };

var soft = new Dictionary<string, float[,,]>();
foreach (string key in new[] { "skin", "fabric", "denim", "dirt", "grass" })
{
    string path = Path.Combine(art, key + ".png");
    if (File.Exists(path))
        soft[key] = LoadSoft(path, Soft);
}

(float, float) VisualSize(float sx, float sy) =>
    sx >= sy ? (sx * 1.18f, sy * 1.16f) : (sx * 1.18f, sy * 1.26f);

float[] DimFar(float[] c) => new[] { c[0] * 0.52f, c[1] * 0.52f, c[2] * 0.52f };

var pelvis = (0.24f, 0.16f);
var torso = (0.24f, 0.34f);
var head = (0.20f, 0.23f);
var neck = (0.12f, 0.09f);
var upper = (0.10f, 0.33f);
var lower = (0.08f, 0.32f);
var hand = (0.06f, 0.24f);
var thigh = (0.16f, 0.43f);
var shin = (0.11f, 0.43f);
var foot = (0.26f, 0.07f);

float lat = MathF.Cos(80f * MathF.PI / 180f);
const float HipHalf = 0.085f;
const float ShoulderHalf = 0.185f;
const float Vis = 0.045f;
const float Heel = 0.065f;
const float ShoulderDrop = 0.125f;

float trunkH = pelvis.Item2 + torso.Item2;
float pelvisCy = -trunkH * 0.5f + pelvis.Item2 * 0.5f;
float torsoCy = -trunkH * 0.5f + pelvis.Item2 + torso.Item2 * 0.5f;
float neckCy = torsoCy + torso.Item2 * 0.5f + neck.Item2 * 0.5f;
float headCy = torsoCy + torso.Item2 * 0.5f + neck.Item2 + head.Item2 * 0.5f;
float hipRootY = pelvisCy - pelvis.Item2 * 0.5f;
float shoulderY = torso.Item2 * 0.5f - ShoulderDrop;
float shoulderRootY = torsoCy + shoulderY;

var layers = new List<Layer>();

void Add(string name, (float, float) size, string shape, string? albedo, float[] color,
    float px, float py, float ox, int order, float ax, float ay)
{
    var v = VisualSize(size.Item1, size.Item2);
    layers.Add(new Layer
    {
        Order = order,
        Name = name,
        Vx = v.Item1,
        Vy = v.Item2,
        Shape = shape,
        Albedo = albedo,
        Color = color,
        Cx = px + ox,
        Cy = py,
        Ax = ax,
        Ay = ay
    });
}

Add("Pelvis", pelvis, "capsule", "denim", Pants, 0, pelvisCy, 0, 0, 0, pelvis.Item2 * 0.5f);
Add("Torso", torso, "capsule", "fabric", Shirt, 0, torsoCy, 0, 0, 0, -torso.Item2 * 0.5f);
Add("Neck", neck, "capsule", "skin", Neck, 0, neckCy, 0, 1, 0, -neck.Item2 * 0.5f);
Add("Head", head, "ellipse", "skin", Skin, 0, headCy, 0, 2, 0, -head.Item2 * 0.5f);

foreach (var side in new[] { -1f, 1f })
{
    bool far = side < 0;
    float[] D(float[] c) => far ? DimFar(c) : c;
    float hipX = side * HipHalf * lat;
    float shX = side * ShoulderHalf * lat;
    float shift = side * Vis;
    int thighO = far ? -6 : 3;
    int shinO = far ? -5 : 4;
    int footO = far ? -4 : 5;
    int uo = far ? -3 : 6;
    int lo = far ? -2 : 7;
    int ho = far ? -1 : 8;

    float thighCy = hipRootY - thigh.Item2 * 0.5f;
    float shinCy = hipRootY - thigh.Item2 - shin.Item2 * 0.5f;
    float footHalf = foot.Item1 * 0.5f;
    float footCx = hipX - Heel + footHalf;
    float footCy = hipRootY - thigh.Item2 - shin.Item2 - foot.Item2 * 0.5f;
    float ankleAx = hipX - footCx;

    Add(far ? "LThigh" : "RThigh", thigh, "capsule", "denim", D(Pants), hipX, thighCy, shift, thighO, 0, thigh.Item2 * 0.5f);
    Add(far ? "LShin" : "RShin", shin, "capsule", "fabric", D(Socks), hipX, shinCy, shift, shinO, 0, shin.Item2 * 0.5f);
    Add(far ? "LFoot" : "RFoot", foot, "capsule", "denim", D(Shoes), footCx, footCy, shift, footO, ankleAx, foot.Item2 * 0.5f);

    float upperCy = shoulderRootY - upper.Item2 * 0.5f;
    float lowerCy = shoulderRootY - upper.Item2 - lower.Item2 * 0.5f;
    float handCy = shoulderRootY - upper.Item2 - lower.Item2 - hand.Item2 * 0.5f;
    Add(far ? "LUpper" : "RUpper", upper, "capsule", "fabric", D(Shirt), shX, upperCy, shift, uo, 0, upper.Item2 * 0.5f);
    Add(far ? "LLower" : "RLower", lower, "capsule", "skin", D(Skin), shX, lowerCy, shift, lo, 0, lower.Item2 * 0.5f);
    Add(far ? "LHand" : "RHand", hand, "capsule", "skin", D(Skin), shX, handCy, shift, ho, 0, hand.Item2 * 0.5f);
}

float xmin = float.MaxValue, ymin = float.MaxValue, xmax = float.MinValue, ymax = float.MinValue;
foreach (Layer L in layers)
{
    xmin = Math.Min(xmin, L.Cx - L.Vx * 0.5f);
    ymin = Math.Min(ymin, L.Cy - L.Vy * 0.5f);
    xmax = Math.Max(xmax, L.Cx + L.Vx * 0.5f);
    ymax = Math.Max(ymax, L.Cy + L.Vy * 0.5f);
}
xmin -= 0.18f;
ymin -= 0.12f;
xmax += 0.18f;
ymax += 0.12f;

int cw = (int)Math.Ceiling((xmax - xmin) * Ppm);
int ch = (int)Math.Ceiling((ymax - ymin) * Ppm);
var canvas = new float[ch, cw, 4];
for (int y = 0; y < ch; y++)
{
    for (int x = 0; x < cw; x++)
    {
        canvas[y, x, 0] = Sky[0];
        canvas[y, x, 1] = Sky[1];
        canvas[y, x, 2] = Sky[2];
        canvas[y, x, 3] = 1f;
    }
}

float groundY = hipRootY - thigh.Item2 - shin.Item2 - foot.Item2;
if (soft.TryGetValue("dirt", out float[,,]? dirt))
{
    int gy0 = (int)Math.Round((groundY - 0.22f - ymin) * Ppm);
    int gh = (int)(0.22f * Ppm);
    for (int y = Math.Max(0, gy0); y < Math.Min(ch, gy0 + gh); y++)
    {
        float my = (y + 0.5f) / Ppm + ymin;
        for (int x = 0; x < cw; x++)
        {
            float mx = (x + 0.5f) / Ppm + xmin;
            float[] c = Bilerp(dirt, mx / 2.5f, my / 2.5f);
            canvas[y, x, 0] = c[0];
            canvas[y, x, 1] = c[1];
            canvas[y, x, 2] = c[2];
            canvas[y, x, 3] = 1f;
        }
    }
}
if (soft.TryGetValue("grass", out float[,,]? grass))
{
    int gy1 = (int)Math.Round((groundY - ymin) * Ppm);
    int gh2 = (int)(0.045f * Ppm);
    for (int y = Math.Max(0, gy1); y < Math.Min(ch, gy1 + gh2); y++)
    {
        float my = (y + 0.5f) / Ppm + ymin;
        for (int x = 0; x < cw; x++)
        {
            float mx = (x + 0.5f) / Ppm + xmin;
            float[] c = Bilerp(grass, mx / 0.5f, my / 0.5f);
            canvas[y, x, 0] = c[0];
            canvas[y, x, 1] = c[1];
            canvas[y, x, 2] = c[2];
            canvas[y, x, 3] = 1f;
        }
    }
}

var baked = new Dictionary<string, (float[,,] px, float ww, float wh)>();
layers.Sort((a, b) => a.Order.CompareTo(b.Order));
foreach (Layer L in layers)
{
    string key = $"{L.Shape}:{L.Vx:F4}x{L.Vy:F4}:{L.Albedo}";
    if (!baked.TryGetValue(key, out var sprite))
    {
        sprite = Bake(L.Vx, L.Vy, L.Shape, L.Albedo, soft);
        baked[key] = sprite;
    }
    Blit(canvas, xmin, ymin, Ppm, sprite.px, sprite.ww, sprite.wh, L.Cx, L.Cy, L.Color);
    BlitMarker(canvas, xmin, ymin, Ppm, L.Cx + L.Ax, L.Cy + L.Ay);
}

using Bitmap bmp = new(cw, ch, PixelFormat.Format32bppArgb);
var data = bmp.LockBits(new Rectangle(0, 0, cw, ch), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
int stride = Math.Abs(data.Stride);
byte[] row = new byte[stride];
for (int y = 0; y < ch; y++)
{
    int srcY = ch - 1 - y;
    for (int x = 0; x < cw; x++)
    {
        int o = x * 4;
        row[o + 0] = ToByte(canvas[srcY, x, 2]);
        row[o + 1] = ToByte(canvas[srcY, x, 1]);
        row[o + 2] = ToByte(canvas[srcY, x, 0]);
        row[o + 3] = ToByte(canvas[srcY, x, 3]);
    }
    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, stride);
}
bmp.UnlockBits(data);
bmp.Save(dest, ImageFormat.Png);
Console.WriteLine($"wrote {dest} {cw}x{ch}");

static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

static string FindProjectRoot()
{
    static bool IsRoot(string dir) => File.Exists(Path.Combine(dir, "Tools", "run-trial.ps1"));
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        string? dir = start;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (IsRoot(dir))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
    }
    return @"e:\Games\2D Game\My project";
}

static float CapsuleDistance(float px, float py, float rx, float ry)
{
    float radius = MathF.Min(rx, ry) * 0.42f;
    float qx = MathF.Abs(px) - (rx - radius);
    float qy = MathF.Abs(py) - (ry - radius);
    float ox = MathF.Max(qx, 0f);
    float oy = MathF.Max(qy, 0f);
    return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f) - radius;
}

static float EllipseDistance(float px, float py, float rx, float ry)
{
    float nx = px / MathF.Max(0.001f, rx);
    float ny = py / MathF.Max(0.001f, ry);
    float len = MathF.Sqrt(nx * nx + ny * ny);
    if (len < 0.0001f)
        return -MathF.Min(rx, ry);
    float edge = MathF.Sqrt((px * px + py * py) / (len * len));
    return (len - 1f) * edge;
}

static void ShadeEdge(float dist, out float shade, out float alpha)
{
    alpha = dist < 0f ? 1f : Math.Clamp(1f - dist, 0f, 1f);
    if (alpha <= 0.001f)
    {
        shade = 0f;
        return;
    }
    float outline = Math.Clamp((dist + 1.4f) / 2.2f, 0f, 1f);
    shade = 1f + (0.82f - 1f) * outline;
}

static float Wrap01(float t) => t - MathF.Floor(t);

static float[] Bilerp(float[,,] tex, float u, float v)
{
    int h = tex.GetLength(0);
    int w = tex.GetLength(1);
    float x = Wrap01(u) * w - 0.5f;
    float y = Wrap01(v) * h - 0.5f;
    int x0 = (int)MathF.Floor(x);
    int y0 = (int)MathF.Floor(y);
    float fx = x - x0;
    float fy = y - y0;
    int x1 = x0 + 1;
    int y1 = y0 + 1;
    float[] At(int ix, int iy)
    {
        ix = ((ix % w) + w) % w;
        iy = ((iy % h) + h) % h;
        return new[] { tex[iy, ix, 0], tex[iy, ix, 1], tex[iy, ix, 2] };
    }
    float[] a = At(x0, y0);
    float[] b = At(x1, y0);
    float[] c = At(x0, y1);
    float[] d = At(x1, y1);
    var r = new float[3];
    for (int i = 0; i < 3; i++)
        r[i] = a[i] * (1 - fx) * (1 - fy) + b[i] * fx * (1 - fy) + c[i] * (1 - fx) * fy + d[i] * fx * fy;
    return r;
}

static float[,,] LoadSoft(string path, int size)
{
    using var src = new Bitmap(path);
    var tmp = new float[size, size, 3];
    for (int y = 0; y < size; y++)
    {
        float v = (y + 0.5f) / size;
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size;
            // Unity v=0 is at the bottom; Bitmap y=0 is at the top. Flip when sampling.
            float sx = u * (src.Width - 1);
            float sy = (1f - v) * (src.Height - 1);
            Color c = BilinearBitmap(src, sx, sy);
            tmp[y, x, 0] = c.R / 255f;
            tmp[y, x, 1] = c.G / 255f;
            tmp[y, x, 2] = c.B / 255f;
        }
    }
    return tmp;
}

static Color BilinearBitmap(Bitmap src, float x, float y)
{
    int w = src.Width;
    int h = src.Height;
    x = Math.Clamp(x, 0, w - 1.001f);
    y = Math.Clamp(y, 0, h - 1.001f);
    int x0 = (int)MathF.Floor(x);
    int y0 = (int)MathF.Floor(y);
    int x1 = Math.Min(x0 + 1, w - 1);
    int y1 = Math.Min(y0 + 1, h - 1);
    float fx = x - x0;
    float fy = y - y0;
    Color a = src.GetPixel(x0, y0);
    Color b = src.GetPixel(x1, y0);
    Color c = src.GetPixel(x0, y1);
    Color d = src.GetPixel(x1, y1);
    float Lerp(float p, float q, float t) => p + (q - p) * t;
    int ch(Func<Color, byte> sel)
    {
        float v = Lerp(Lerp(sel(a), sel(b), fx), Lerp(sel(c), sel(d), fx), fy);
        return Math.Clamp((int)MathF.Round(v), 0, 255);
    }
    return Color.FromArgb(255, ch(p => p.R), ch(p => p.G), ch(p => p.B));
}

static (float[,,] px, float ww, float wh) Bake(float sx, float sy, string shape, string? albedo,
    Dictionary<string, float[,,]> maps)
{
    int w = Math.Max(1, (int)MathF.Round(sx * 256f));
    int h = Math.Max(1, (int)MathF.Round(sy * 256f));
    float scale = 1f;
    if (w > 512 || h > 512)
        scale = 512f / Math.Max(w, h);
    float minSide = Math.Min(w, h) * scale;
    if (minSide < 64f)
        scale *= 64f / Math.Max(1f, minSide);
    w = Math.Max(8, (int)MathF.Round(w * scale));
    h = Math.Max(8, (int)MathF.Round(h * scale));
    var pixels = new float[h, w, 4];
    float cx = (w - 1) * 0.5f;
    float cy = (h - 1) * 0.5f;
    float rx = w * 0.5f - 0.5f;
    float ry = h * 0.5f - 0.5f;
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            float px = x - cx;
            float py = y - cy;
            float dist = shape == "ellipse"
                ? EllipseDistance(px, py, rx, ry)
                : CapsuleDistance(px, py, rx, ry);
            ShadeEdge(dist, out float shade, out float alpha);
            if (alpha <= 0.001f)
                continue;
            float mx = (x + 0.5f) / w * sx;
            float my = (y + 0.5f) / h * sy;
            float[] albedoC = { 1, 1, 1 };
            if (!string.IsNullOrEmpty(albedo) && maps.TryGetValue(albedo, out float[,,]? tex))
                albedoC = Bilerp(tex, mx / 0.38f, my / 0.38f);
            pixels[y, x, 0] = shade * (1f + (albedoC[0] - 1f) * 0.78f);
            pixels[y, x, 1] = shade * (1f + (albedoC[1] - 1f) * 0.78f);
            pixels[y, x, 2] = shade * (1f + (albedoC[2] - 1f) * 0.78f);
            pixels[y, x, 3] = alpha;
        }
    }
    float ppu = w / sx;
    return (pixels, w / ppu, h / ppu);
}

static void Blit(float[,,] dst, float xmin, float ymin, float ppm,
    float[,,] src, float ww, float wh, float cx, float cy, float[] color)
{
    int sh = src.GetLength(0);
    int sw = src.GetLength(1);
    int dh = dst.GetLength(0);
    int dw = dst.GetLength(1);
    float left = cx - ww * 0.5f;
    float bottom = cy - wh * 0.5f;
    int x0 = (int)MathF.Round((left - xmin) * ppm);
    int y0 = (int)MathF.Round((bottom - ymin) * ppm);
    int x1 = Math.Max(0, x0);
    int y1 = Math.Max(0, y0);
    int x2 = Math.Min(dw, x0 + sw);
    int y2 = Math.Min(dh, y0 + sh);
    for (int y = y1; y < y2; y++)
    {
        int sy = y - y0;
        for (int x = x1; x < x2; x++)
        {
            int sx = x - x0;
            float a = src[sy, sx, 3];
            if (a <= 0.001f)
                continue;
            float r = src[sy, sx, 0] * color[0];
            float g = src[sy, sx, 1] * color[1];
            float b = src[sy, sx, 2] * color[2];
            dst[y, x, 0] = r * a + dst[y, x, 0] * (1f - a);
            dst[y, x, 1] = g * a + dst[y, x, 1] * (1f - a);
            dst[y, x, 2] = b * a + dst[y, x, 2] * (1f - a);
            dst[y, x, 3] = a + dst[y, x, 3] * (1f - a);
        }
    }
}

static void BlitMarker(float[,,] dst, float xmin, float ymin, float ppm, float cx, float cy)
{
    const int n = 32;
    var tex = new float[n, n, 4];
    float center = (n - 1) / 2f;
    float radius = n / 2f - 1f;
    for (int y = 0; y < n; y++)
    {
        for (int x = 0; x < n; x++)
        {
            float d = MathF.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) - radius;
            float a = Math.Clamp(0.7f - d, 0f, 1f);
            if (a <= 0)
                continue;
            tex[y, x, 0] = 0.08f;
            tex[y, x, 1] = 0.07f;
            tex[y, x, 2] = 0.06f;
            tex[y, x, 3] = a;
        }
    }
    Blit(dst, xmin, ymin, ppm, tex, 0.03f, 0.03f, cx, cy, new[] { 1f, 1f, 1f });
}

sealed class Layer
{
    public int Order;
    public string Name = "";
    public float Vx, Vy;
    public string Shape = "";
    public string? Albedo;
    public float[] Color = Array.Empty<float>();
    public float Cx, Cy, Ax, Ay;
}
