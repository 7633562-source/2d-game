# Copy generated dog part PNGs into Resources/Art/Dog and write .meta.
# Punches near-white to alpha. Physics is not touched.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$srcDir = "C:\Users\Admin\.cursor\projects\e-Games-2D-Game-My-project\assets"
$genDir = "e:\Games\2D Game\My project\ArtIncoming\Dog\gen"
$resDir = "e:\Games\2D Game\My project\Assets\Resources\Art\Dog"
New-Item -ItemType Directory -Force -Path $genDir | Out-Null
New-Item -ItemType Directory -Force -Path $resDir | Out-Null

$map = [ordered]@{
  "dog_chest.png"       = "chest.png"
  "dog_pelvis.png"      = "pelvis.png"
  "dog_neck.png"        = "neck.png"
  "dog_head.png"        = "head.png"
  "dog_tail.png"        = "tail.png"
  "dog_front_upper.png" = "front_upper.png"
  "dog_front_lower.png" = "front_lower.png"
  "dog_thigh.png"       = "thigh.png"
  "dog_shin.png"        = "shin.png"
  "dog_paw.png"         = "paw.png"
}

$punchType = Add-Type -PassThru -ReferencedAssemblies System.Drawing.dll -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class DogArtPunch {
  public static void WhiteToAlpha(string srcPath, string dstPath) {
    using (var src = new Bitmap(srcPath))
    using (var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
    {
      var srcRect = new Rectangle(0, 0, src.Width, src.Height);
      using (var g = Graphics.FromImage(dst))
        g.DrawImage(src, srcRect);
      var data = dst.LockBits(srcRect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
      int bytes = Math.Abs(data.Stride) * dst.Height;
      byte[] buf = new byte[bytes];
      Marshal.Copy(data.Scan0, buf, 0, bytes);
      for (int i = 0; i < buf.Length; i += 4) {
        byte b = buf[i], gch = buf[i+1], r = buf[i+2];
        int min = r < gch ? (r < b ? r : b) : (gch < b ? gch : b);
        int max = r > gch ? (r > b ? r : b) : (gch > b ? gch : b);
        if (min > 235 && (max - min) < 18) buf[i+3] = 0;
      }
      Marshal.Copy(buf, 0, data.Scan0, bytes);
      dst.UnlockBits(data);
      dst.Save(dstPath, ImageFormat.Png);
    }
  }
}
"@

function Write-DogMeta([string]$pngPath) {
  $guid = ([guid]::NewGuid()).ToString("N")
  $sid = ([guid]::NewGuid()).ToString("N").Substring(0, 16) + "0800000000000000"
  $meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: $sid
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"@
  Set-Content -Path ($pngPath + ".meta") -Value $meta -Encoding ASCII
}

foreach ($srcName in $map.Keys) {
  $src = Join-Path $srcDir $srcName
  if (-not (Test-Path $src)) { throw "missing $src" }
  $dstName = $map[$srcName]
  $tmp = Join-Path $genDir $dstName
  [DogArtPunch]::WhiteToAlpha($src, $tmp)
  $dst = Join-Path $resDir $dstName
  Copy-Item -Force $tmp $dst
  Write-DogMeta $dst
  $info = New-Object System.Drawing.Bitmap $dst
  "{0} -> {1} {2}x{3} A00={4}" -f $srcName, $dstName, $info.Width, $info.Height, $info.GetPixel(0,0).A
  $info.Dispose()
}
