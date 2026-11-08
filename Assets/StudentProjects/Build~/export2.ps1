param([string]$OutPdfDir, [string]$Filter = "*", [switch]$Sheets)
# PowerPoint 로 pptx -> pdf, 그리고 (선택) 슬라이드 PNG 를 모아 한 장짜리 미리보기를 만든다
[void][Reflection.Assembly]::LoadWithPartialName("System.Drawing")
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here "out2"
$png = Join-Path $here "png2"
New-Item -ItemType Directory -Force $png | Out-Null
if ($OutPdfDir) { New-Item -ItemType Directory -Force $OutPdfDir | Out-Null }
$pp = New-Object -ComObject PowerPoint.Application
try {
  Get-ChildItem $out -Filter "$Filter.pptx" | ForEach-Object {
    $pres = $pp.Presentations.Open($_.FullName, $true, $false, $false)
    if ($OutPdfDir) { $pres.SaveAs((Join-Path $OutPdfDir ($_.BaseName + ".pdf")), 32) }
    if ($Sheets) {
      $files = @()
      for ($i = 1; $i -le $pres.Slides.Count; $i++) {
        $f = Join-Path $png ("{0}-{1:D2}.png" -f $_.BaseName, $i)
        $pres.Slides.Item($i).Export($f, "PNG", 1280, 720); $files += $f
      }
      $cols = 3; $tw = 640; $th = 360; $rows = [math]::Ceiling($files.Count / $cols)
      for ($part = 0; $part -lt [math]::Ceiling($rows / 3); $part++) {
        $bmp = New-Object System.Drawing.Bitmap ($cols * $tw), (3 * $th)
        $gr = [System.Drawing.Graphics]::FromImage($bmp); $gr.Clear([System.Drawing.Color]::White)
        for ($j = 0; $j -lt 9; $j++) {
          $idx = $part * 9 + $j; if ($idx -ge $files.Count) { break }
          $img = [System.Drawing.Image]::FromFile($files[$idx])
          $gr.DrawImage($img, ($j % $cols) * $tw, [math]::Floor($j / $cols) * $th, $tw - 4, $th - 4); $img.Dispose()
        }
        $bmp.Save((Join-Path $png ("{0}-grid{1}.png" -f $_.BaseName, ($part + 1)))); $gr.Dispose(); $bmp.Dispose()
      }
    }
    $pres.Close()
    Write-Output $_.BaseName
  }
} finally { $pp.Quit() }

