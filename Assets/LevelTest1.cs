using System.Collections.Generic;
using UnityEngine;

// First authored yard. Continuous support, 5 cm curbs only — not a wall.
// GPT Sol: climb, dip, square, gate, finish, lookout; curb faces; back fence.
public static class LevelTest1
{
    public const string Title = "Двор";
    public const float SignX = 48f;
    public const float EndX = 96f;

    public struct Slab
    {
        public float left;
        public float width;
        public float surfaceY;
        public string name;
    }

    private struct Decor
    {
        public string name;
        public float x;
        public float centerY;
        public Vector2 size;
        public Color color;
        public string kind;
    }

    // Continuous tape −32…+96. Spawn at x=0, surface −2.00.
    public static readonly Slab[] Slabs =
    {
        new Slab { left = -32f, width = 32f, surfaceY = -2.00f, name = "Behind" },
        new Slab { left =   0f, width =  8f, surfaceY = -2.00f, name = "Spawn" },
        new Slab { left =   8f, width =  3f, surfaceY = -1.95f, name = "CurbEntry" },
        new Slab { left =  11f, width =  7f, surfaceY = -1.95f, name = "GardenWalk" },
        new Slab { left =  18f, width =  4f, surfaceY = -2.00f, name = "DrainDip" },
        new Slab { left =  22f, width =  6f, surfaceY = -1.95f, name = "CurbReturn" },
        new Slab { left =  28f, width = 12f, surfaceY = -1.90f, name = "CourtyardSquare" },
        new Slab { left =  40f, width =  2f, surfaceY = -1.95f, name = "CurbGateDip" },
        new Slab { left =  42f, width =  8f, surfaceY = -1.90f, name = "FinishPlaza" },
        new Slab { left =  50f, width =  6f, surfaceY = -1.85f, name = "EastRise" },
        new Slab { left =  56f, width =  4f, surfaceY = -1.90f, name = "RainGutter" },
        new Slab { left =  60f, width =  6f, surfaceY = -1.95f, name = "LowerWalk" },
        new Slab { left =  66f, width = 10f, surfaceY = -1.90f, name = "OrchardWalk" },
        new Slab { left =  76f, width =  4f, surfaceY = -1.85f, name = "CurbUpper" },
        new Slab { left =  80f, width =  8f, surfaceY = -1.90f, name = "UpperCourt" },
        new Slab { left =  88f, width =  4f, surfaceY = -1.85f, name = "CurbLookout" },
        new Slab { left =  92f, width =  2f, surfaceY = -1.80f, name = "Lookout" },
        new Slab { left =  94f, width =  2f, surfaceY = -1.85f, name = "AfterWalk" }
    };

    private static readonly Color PostTint = new Color(0.28f, 0.18f, 0.10f, 1f);
    private static readonly Color BenchTint = new Color(0.42f, 0.27f, 0.14f, 1f);
    private static readonly Color GrateTint = new Color(0.20f, 0.22f, 0.23f, 1f);
    private static readonly Color LookoutTint = new Color(0.52f, 0.39f, 0.20f, 1f);
    private static readonly Color CurbFaceTint = new Color(0.34f, 0.22f, 0.12f, 1f);
    private static readonly Color DrainFaceTint = new Color(0.18f, 0.22f, 0.23f, 1f);
    private static readonly Color LookoutFaceTint = new Color(0.43f, 0.41f, 0.36f, 1f);
    private static readonly Color FencePostTint = new Color(0.24f, 0.15f, 0.08f, 1f);
    private static readonly Color FenceRailTint = new Color(0.38f, 0.25f, 0.14f, 1f);
    private static readonly Vector2 CurbFaceSize = new Vector2(0.14f, 0.05f);
    private static readonly Vector2 FencePostSize = new Vector2(0.10f, 0.62f);

    // Picture only. A collider here would be a wall for the feet.
    private static readonly Decor[] Marks =
    {
        new Decor { name = "EntryPostWest", x = 8.35f, centerY = -1.79f, size = new Vector2(0.07f, 0.32f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "EntryPostEast", x = 10.65f, centerY = -1.79f, size = new Vector2(0.07f, 0.32f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "DrainGrate", x = 20f, centerY = -1.975f, size = new Vector2(1.20f, 0.05f), color = GrateTint, kind = LevelArt.FillKey },
        new Decor { name = "SquareBenchSeat", x = 34f, centerY = -1.41f, size = new Vector2(1.80f, 0.14f), color = BenchTint, kind = LevelArt.FillKey },
        new Decor { name = "SquareBenchLegL", x = 33.35f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "SquareBenchLegR", x = 34.65f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "GatePostWest", x = 40.25f, centerY = -1.63f, size = new Vector2(0.08f, 0.64f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "GatePostEast", x = 41.75f, centerY = -1.63f, size = new Vector2(0.08f, 0.64f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "FinishSign", x = SignX, centerY = -1.45f, size = new Vector2(0.18f, 0.90f), color = LevelArt.SignTint(), kind = LevelArt.SignKey },
        new Decor { name = "RainGutterGrate", x = 58f, centerY = -1.875f, size = new Vector2(1.60f, 0.05f), color = GrateTint, kind = LevelArt.FillKey },
        new Decor { name = "OrchardBenchSeat", x = 72f, centerY = -1.41f, size = new Vector2(1.50f, 0.14f), color = BenchTint, kind = LevelArt.FillKey },
        new Decor { name = "OrchardBenchLegL", x = 71.45f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "OrchardBenchLegR", x = 72.55f, centerY = -1.69f, size = new Vector2(0.10f, 0.42f), color = PostTint, kind = LevelArt.FillKey },
        new Decor { name = "LookoutMarker", x = 93f, centerY = -1.50f, size = new Vector2(0.09f, 0.60f), color = LookoutTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestPostL", x = -30.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestPostR", x = -24.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestRailLow", x = -27.50f, centerY = -1.82f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceWestRailHigh", x = -27.50f, centerY = -1.55f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddlePostL", x = -19.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddlePostR", x = -14.50f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddleRailLow", x = -17f, centerY = -1.82f, size = new Vector2(4.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddleRailHighL", x = -18.325f, centerY = -1.55f, size = new Vector2(2.25f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceMiddleRailHighR", x = -15.675f, centerY = -1.55f, size = new Vector2(2.25f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastPostL", x = -9f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastPostR", x = -3f, centerY = -1.69f, size = FencePostSize, color = FencePostTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastRailLow", x = -6f, centerY = -1.82f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "BackFenceEastRailHigh", x = -6f, centerY = -1.55f, size = new Vector2(5.90f, 0.07f), color = FenceRailTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceEntryRise", x = 8f, centerY = -1.975f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceDrainWest", x = 18f, centerY = -1.975f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceDrainEast", x = 22f, centerY = -1.975f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceSquareRise", x = 28f, centerY = -1.925f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceGateWest", x = 40f, centerY = -1.925f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceGateEast", x = 42f, centerY = -1.925f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceEastRise", x = 50f, centerY = -1.875f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceRainWest", x = 56f, centerY = -1.875f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceRainEast", x = 60f, centerY = -1.925f, size = CurbFaceSize, color = DrainFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceOrchardRise", x = 66f, centerY = -1.925f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceUpperRise", x = 76f, centerY = -1.875f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceUpperDrop", x = 80f, centerY = -1.875f, size = CurbFaceSize, color = CurbFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceLookoutApproach", x = 88f, centerY = -1.875f, size = CurbFaceSize, color = LookoutFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceLookoutRise", x = 92f, centerY = -1.825f, size = CurbFaceSize, color = LookoutFaceTint, kind = LevelArt.FillKey },
        new Decor { name = "CurbFaceLookoutExit", x = 94f, centerY = -1.825f, size = CurbFaceSize, color = LookoutFaceTint, kind = LevelArt.FillKey }
    };

    public static List<LevelPiece> PiecesForChunk(int chunkIndex, float chunkWidth)
    {
        float x0 = chunkIndex * chunkWidth;
        float x1 = x0 + chunkWidth;
        var pieces = new List<LevelPiece>(8);

        for (int i = 0; i < Slabs.Length; i++)
        {
            Slab slab = Slabs[i];
            float a = Mathf.Max(slab.left, x0);
            float b = Mathf.Min(slab.left + slab.width, x1);
            if (b - a < 0.02f)
                continue;
            pieces.Add(MakeSlab(a, b - a, slab.surfaceY, slab.name));
        }

        if (pieces.Count == 0)
        {
            float y = chunkIndex < 0
                ? LevelGenerator.DefaultSurfaceY
                : Slabs[Slabs.Length - 1].surfaceY;
            pieces.Add(MakeSlab(x0, chunkWidth, y, "Extend"));
        }

        return pieces;
    }

    public static void SpawnDecor(int chunkIndex, float chunkWidth, Transform parent)
    {
        float x0 = chunkIndex * chunkWidth;
        float x1 = x0 + chunkWidth;

        for (int i = 0; i < Marks.Length; i++)
        {
            Decor mark = Marks[i];
            if (mark.x < x0 || mark.x >= x1)
                continue;
            StaticPlatform.Spawn(new LevelPiece
            {
                name = mark.name,
                center = new Vector2(mark.x, mark.centerY),
                size = mark.size,
                rotationZ = 0f,
                color = mark.color,
                kind = mark.kind,
                hasCollider = false
            }, parent);
        }
    }

    public static float SurfaceAt(float x)
    {
        for (int i = 0; i < Slabs.Length; i++)
        {
            Slab slab = Slabs[i];
            if (x >= slab.left && x < slab.left + slab.width)
                return slab.surfaceY;
        }

        return x < Slabs[0].left ? Slabs[0].surfaceY : Slabs[Slabs.Length - 1].surfaceY;
    }

    private static LevelPiece MakeSlab(float leftX, float width, float surfaceY, string name)
    {
        return new LevelPiece
        {
            name = name,
            center = new Vector2(leftX + width * 0.5f, surfaceY - LevelGenerator.GroundThickness * 0.5f),
            size = new Vector2(width, LevelGenerator.GroundThickness),
            rotationZ = 0f,
            color = SlabTint(name),
            kind = LevelArt.FillKey,
            hasCollider = true
        };
    }

    private static Color SlabTint(string name)
    {
        Color baseTint = LevelArt.FillTint();
        if (name != null && name.StartsWith("Curb"))
            return baseTint * new Color(0.86f, 0.86f, 0.86f, 1f);
        if (name == "DrainDip" || name == "RainGutter")
            return baseTint * new Color(0.78f, 0.80f, 0.82f, 1f);
        return baseTint;
    }
}
