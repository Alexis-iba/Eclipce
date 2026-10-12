# Pipeline Blender ↔ Unity (ECLIPSE GENESIS)

## Conexiones MCP (verificadas el 2026-10-12)
| Servidor | Transporte | Comando / URL | Ámbito | Requisito |
|---|---|---|---|---|
| **UnityMCP** (MCP for Unity 10.3.0, CoplayDev) | HTTP | `http://127.0.0.1:8080/mcp` | local, proyecto `C:\Users\ibarr\EclipseGenesis` | Unity abierto con EclipseGenesis. El servidor arranca solo ("Auto-start on load") |
| **blender** (MCP for Blender, complemento protocolo 13) | stdio | `C:\Users\ibarr\.local\bin\uvx.exe mcp-for-blender` | local, proyecto `C:\Users\ibarr\EclipseGenesis` | Blender 5.2 abierto. El complemento escucha en `localhost:9876` y arranca solo al abrir Blender |

- `claude mcp list` solo muestra estos servidores si lo ejecutas **dentro de `C:\Users\ibarr\EclipseGenesis`**: son de ámbito local. Desde `C:\WINDOWS\System32` no aparecen.
- Blender se registra con la **ruta completa** de `uvx.exe`. Con `uvx` a secas falla siempre que el PATH de la ventana no incluye `C:\Users\ibarr\.local\bin` (error "uvx no se reconoce"). Ese era el motivo de *Connection closed*.
- Sigue existiendo un registro antiguo de `blender` en el ámbito de `C:\WINDOWS\System32`, con `uvx` sin ruta. No se tocó.

## Carpetas
```
ArtSource/Blender/<Categoría>/     archivos .blend de trabajo (fuera de Assets: Unity no los importa)
Assets/_EclipseGenesis/Art/Imported/<Categoría>/   FBX exportados (Unity los importa con reglas automáticas)
Assets/_EclipseGenesis/Prefabs/Weapons/Imported/   viewmodels generados desde los FBX de armas
```
Categorías: `Weapons`, `Aliens`, `Characters`, `Environment`, `Vehicles`.

## Escala y orientación
- **1 unidad de Blender = 1 metro.** Aplica la escala antes de exportar (Ctrl+A → Scale).
- **El modelo mira hacia +Y en Blender**, con **+Z arriba** y su **lado derecho en +X**. Medido con un FBX real el 2026-10-12: la conversión es Blender (x, y, z) → Unity (x, z, y). +Y pasa a ser +Z en Unity (hacia delante), sin rotación en la raíz y sin invertir izquierda y derecha. Si el modelo mira hacia −Y, llega al revés: el cañón apunta hacia atrás.
- **Exportación FBX:** Up Y, *Apply Transform*, *FBX Units Scale* (el script lo hace todo). Unity aplica *Bake Axis Conversion* (`ImportedModelConventions`). En esta combinación, el ajuste *Forward* no cambia el resultado.
- Comprueba la orientación del primer modelo de cada categoría al importarlo: debe mirar a +Z (la flecha azul de Unity) sin rotación en la raíz.

Medidas de referencia del juego actual:

| Elemento | Medida |
|---|---|
| Altura del jugador | 1,8 m (ojos a 1,68 m) |
| Puerta | 2,2 × 2,6 m (hueco 2,6 × 2,9) |
| Escalón | 0,2–0,24 m de alto y 0,3 m de huella |
| EG-47 | unos 0,85 m de largo; origen en la empuñadura |
| Stalker | unos 2 m de largo y 1,3 m de alto |
| Ravager (previsto) | unos 3 m de alto |
| Contenedor de 20 ft | 6,06 × 2,6 × 2,44 m |

## Nombres
| Prefijo / nombre | Uso |
|---|---|
| `SM_` | malla estática (escenario, armas) |
| `SK_` | malla con esqueleto (aliens, personajes) |
| `SK_Nombre@Accion` | clip de animación (por ejemplo `SK_Stalker@Run`) |
| `_LOD0`, `_LOD1`… | niveles de detalle |
| `UCX_` | malla de colisión simple |
| `Socket_Muzzle`, `Socket_Sight`, `Socket_ShellEject` | empties obligatorios en armas (la mira es el punto que queda en el centro de la pantalla al apuntar) |
| `Magazine`, `Arm_L` | partes del arma que se mueven en la recarga procedural |

Para las texturas PBR de HDRP: `_BaseColor`, `_Normal` y `_MaskMap` (R metálico, G AO, B máscara de detalle, A suavidad).

## Flujo de un arma (por ejemplo la EG-47 final)
1. Modela en `ArtSource/Blender/Weapons/EG47.blend` con el cañón hacia **+Y**, el origen en la empuñadura y los empties `Socket_*`.
2. Exporta desde Blender (o por Blender MCP con `execute_blender_code`):
   ```python
   exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read())
   print(check_weapon_sockets())   # [] = listo
   export("Weapons", "SM_EG47")
   ```
3. En Unity, selecciona el FBX → **Eclipse Genesis → Art → Create Weapon Viewmodel From Selected FBX**. Se crea `Prefabs/Weapons/Imported/VM_SM_EG47.prefab`, con `WeaponModel`, `WeaponViewModel`, fogonazo y luz.
4. Asigna ese prefab en `Data/Weapons/EG47.asset → viewModelPrefab`. El disparo, el ADS, el retroceso, la recarga y el HUD siguen funcionando igual.

## Aliens y personajes (preparado, falta el modelo)
- El Stalker actual usa `AlienBody`, una animación procedural que necesita referencias a torso, cabeza, patas y garras. Un modelo con esqueleto llevará `Animator` y clips (`SK_Stalker@Idle/Walk/Run/Attack/Death`). Hay que crear un adaptador `Animator` ↔ `AlienBrain` cuando exista el primer modelo; la IA (`AlienBrain`, `AlienPerception`, `AlienHealth`, hitboxes) no cambia.
- Los personajes humanoides se importan como *Humanoid* para poder reutilizar animaciones.

## Pendiente antes de modelar
- Decidir si se activa Git LFS para `.blend` y `.fbx`. Está instalado (`git-lfs 3.7.1`), pero no se ha activado en el repositorio porque cambia la configuración de Git.
- ✅ Orientación validada con un FBX real exportado por Blender MCP e importado por Unity MCP (2026-10-12). El FBX y el prefab de prueba se borraron después.
