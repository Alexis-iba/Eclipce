param([string]$Res = '2k')
# Downloads the selected CC0 Poly Haven assets into the Unity project (Art/Imported/...).
$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\ibarr\EclipseGenesis\Assets\_EclipseGenesis\Art\Imported'

$hdris = @('industrial_sunset_puresky')
$textures = @('sparse_grass','dry_mud_field_001','rocky_terrain_02','rocky_trail','asphalt_02','weathered_plank_siding','wood_floor_worn','white_plaster_02','roof_09','corrugated_iron_02','cracked_concrete_wall','concrete_floor_worn_001','metal_plate','rust_coarse_01')
$models = @{
  'Environment' = @('rock_moss_set_01','boulder_01','Barrel_01','wooden_crate_01','modular_chainlink_fence','street_lamp_01','old_tyre','covered_car','small_lpg_tank')
  'Vegetation'  = @('fir_tree_01','dead_tree_trunk_02','dry_branches_medium_01')
  'Props'       = @('sofa_03','wooden_table_02','old_bed_frame','steel_frame_shelves_01','vintage_cabinet_01','painted_wooden_chair_01')
}

function Get-File($url, $dest) {
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    if (Test-Path $dest) { return 0 }
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
    return (Get-Item $dest).Length
}

$total = 0
foreach ($id in $hdris) {
    $f = Invoke-RestMethod "https://api.polyhaven.com/files/$id"
    $total += Get-File $f.hdri.'4k'.hdr.url "$root\Sky\$id`_4k.hdr"
    "HDRI $id ok"
}
foreach ($id in $textures) {
    $f = Invoke-RestMethod "https://api.polyhaven.com/files/$id"
    $maps = @{ 'Diffuse' = 'diff'; 'nor_gl' = 'nor_gl'; 'arm' = 'arm' }
    foreach ($k in $maps.Keys) {
        $entry = $f.$k
        if ($null -eq $entry) { continue }
        $r = if ($entry.$Res) { $entry.$Res } else { $entry.'1k' }
        $url = if ($r.jpg) { $r.jpg.url } else { $r.png.url }
        $ext = [IO.Path]::GetExtension($url)
        $total += Get-File $url "$root\Textures\$id\$id`_$($maps[$k])$ext"
    }
    "TEX $id ok"
}
foreach ($cat in $models.Keys) {
    foreach ($id in $models[$cat]) {
        $f = Invoke-RestMethod "https://api.polyhaven.com/files/$id"
        $fbx = $f.fbx.$Res.fbx
        if ($null -eq $fbx) { $fbx = $f.fbx.'1k'.fbx }
        $dir = "$root\$cat\PolyHaven\$id"
        $total += Get-File $fbx.url "$dir\$id.fbx"
        if ($fbx.include) {
            foreach ($p in $fbx.include.PSObject.Properties) { $total += Get-File $p.Value.url "$dir\$($p.Name -replace '/','\')" }
        }
        "MODEL $cat/$id ok"
    }
}
"TOTAL descargado: {0:N0} MB" -f ($total / 1MB)
