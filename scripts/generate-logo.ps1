param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
function New-WintechIcon([int]$Size, [string]$Path) {
    $image = [System.Drawing.Bitmap]::new($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(255, 255, 181, 21))
    $navy = [System.Drawing.Color]::FromArgb(255, 11, 27, 58)
    $pen = [System.Drawing.Pen]::new($navy, [single]($Size * .09))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $brush = [System.Drawing.SolidBrush]::new($navy)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $points = [System.Drawing.PointF[]]@([System.Drawing.PointF]::new($Size*.211,$Size*.297),[System.Drawing.PointF]::new($Size*.322,$Size*.703),[System.Drawing.PointF]::new($Size*.5,$Size*.449),[System.Drawing.PointF]::new($Size*.678,$Size*.703),[System.Drawing.PointF]::new($Size*.789,$Size*.297))
    $graphics.DrawLines($pen, $points)
    [System.IO.Directory]::CreateDirectory((Split-Path $Path -Parent)) | Out-Null
    $image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $brush.Dispose(); $pen.Dispose(); $graphics.Dispose(); $image.Dispose()
}
foreach ($entry in @{mdpi=48; hdpi=72; xhdpi=96; xxhdpi=144; xxxhdpi=192}.GetEnumerator()) {
    New-WintechIcon $entry.Value (Join-Path $Root "mobile/android/app/src/main/res/mipmap-$($entry.Key)/ic_launcher.png")
}
New-WintechIcon 512 (Join-Path $Root 'mobile/branding/wintech-pos-512.png')
