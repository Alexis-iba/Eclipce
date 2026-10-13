param([string]$Res = '2k')
# Residential street pack (CC0, polyhaven.com), authorised 2026-10-12. Downloads into Assets/_EclipseGenesis/Art/Imported.
# After it finishes: Unity -> Eclipse Genesis -> Art -> Build Poly Haven Materials.
$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\..\Assets\_EclipseGenesis\Art\Imported'

$textures = @('grey_roof_01','red_brick_03','painted_brick','distressed_painted_planks','blue_painted_planks','white_planks_clean',
              'concrete_pavement_02','worn_asphalt','leafy_grass','grass_ground','peeling_painted_wall','plank_flooring_02','bark_brown_02')
# Only the leaf/branch texture sets of these trees (their meshes have millions of triangles).
$treeMaps = @{ 'tree_small_02' = @('leaves','branch') }
$models = @{
  'Environment' = @('concrete_road_barrier_02','fire_hydrant','water_manhole_cover','metal_trash_can','utility_box_01','street_lamp_02','cardboard_box_01','rusted_wheel_rim_01')
  'Props'       = @('Sofa_01','ArmChair_01','dining_table','painted_wooden_chair_02','wooden_bookshelf_worn','drawer_cabinet')
}

function Get-File($url, $dest) {
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    if (Test-Path $dest) { return 0 }
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
    return (Get-Item $dest).Length
}
function Pick($entry) {
    $r = if ($entry.$Res) { $entry.$Res } else { $entry.'1k' }
    if ($r.png) { return $r.png.url } elseif ($r.jpg) { return $r.jpg.url } else { return $r.exr.url }
}

$total = 0
foreach ($id in $textures) {
    $f = Invoke-RestMethod "https://api.polyhaven.com/files/$id"
    $maps = @{ 'Diffuse' = 'diff'; 'nor_gl' = 'nor_gl'; 'arm' = 'arm'; 'Rough' = 'rough' }
    foreach ($k in $maps.Keys) {
        if ($null -eq $f.$k) { continue }
        if ($k -eq 'Rough' -and $f.arm) { continue }
        $url = Pick $f.$k
        $total += Get-File $url "$root\Textures\$id\$id`_$($maps[$k])$([IO.Path]::GetExtension($url))"
    }
    "TEX $id ok"
}
foreach ($id in $treeMaps.Keys) {
    $f = Invoke-RestMethod "https://api.polyhaven.com/files/$id"
    foreach ($part in $treeMaps[$id]) {
        $surface = "$id`_$part"
        foreach ($map in @('diff','alpha','nor_gl','arm')) {
            $entry = $f."$part`_$map"
            if ($null -eq $entry) { continue }
            $url = Pick $entry
            $total += Get-File $url "$root\Textures\$surface\$surface`_$map$([IO.Path]::GetExtension($url))"
        }
        "TREE $surface ok"
    }
}
foreach ($cat in $models.Keys) {
    foreach ($id in $models[$cat]) {
        $f = Invoke-RestMethod "https://api.polyhaven.com/files/$id"
        $fbx = $f.fbx.$Res.fbx
        if ($null -eq $fbx) { $fbx = $f.fbx.'1k'.fbx }
        $dir = "$root\$cat\PolyHaven\$id"
        $total += Get-File $fbx.url "$dir\$id.fbx"
        if ($fbx.include) { foreach ($p in $fbx.include.PSObject.Properties) { $total += Get-File $p.Value.url "$dir\$($p.Name -replace '/','\')" } }
        "MODEL $cat/$id ok"
    }
}
"TOTAL descargado: {0:N0} MB" -f ($total / 1MB)
