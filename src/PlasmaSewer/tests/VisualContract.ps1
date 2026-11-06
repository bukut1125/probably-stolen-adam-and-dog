param([Parameter(Mandatory=$true)][string]$FaucetSprite)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\PipeFxLayout.cs'),[Text.UTF8Encoding]::new($false,$true))
function Number($name) {
    $m=[regex]::Match($source,"\b$name\s*=\s*(\d+)f\s*/\s*(\d+)f")
    if(-not $m.Success){throw "Missing anchor $name"}
    return [double]$m.Groups[1].Value / [double]$m.Groups[2].Value
}
$minX=Number 'MinX';$maxX=Number 'MaxX';$minY=Number 'MinY';$maxY=Number 'MaxY'
$im=[Drawing.Bitmap]::new((Resolve-Path -LiteralPath $FaucetSprite).Path)
try {
    if($im.Width -ne 160 -or $im.Height -ne 80){throw 'Installed faucet sprite dimensions changed'}
    $found=0;$inside=0
    for($y=0;$y -lt 40;$y++) {for($x=0;$x -lt $im.Width;$x++) {
        $c=$im.GetPixel($x,$y)
        if($c.A -gt 200 -and $c.G -gt $c.R*1.15 -and $c.B -gt $c.R*1.05 -and $c.G -gt 45) {
            $found++
            if($x -ge $minX*$im.Width -and $x -lt $maxX*$im.Width -and $y -ge (1-$maxY)*$im.Height -and $y -lt (1-$minY)*$im.Height){$inside++}
        }
    }}
    if($found -lt 100 -or ($inside/$found) -lt .98){throw "Overlay misses glass pixels: $inside of $found"}
    Write-Output "PASS: overlay covers $inside of $found detected water-colored pixels in the real faucet sprite."
} finally {$im.Dispose()}
