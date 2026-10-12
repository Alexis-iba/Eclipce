# ECLIPSE GENESIS: convenciones del proyecto

## Entorno
- Unity **6000.4.8f1**, HDRP 17.4.0, Input System 1.19.0 (solo el nuevo Input System: `activeInputHandler: 1`), espacio de color lineal.
- No hay conexión MCP con Unity. El Editor se controla con `Unity.exe -batchmode` (`-executeMethod`, `-runTests`). Unity Hub tiene que estar abierto y con sesión iniciada. Si el proyecto está abierto en un Editor con interfaz, el batchmode no puede usarlo.
- Lanza `Unity.exe` con `Start-Process -PassThru` y `WaitForExit(timeout)`, nunca con `-Wait`.
- En las pruebas con `InputTestFixture`, envía **un evento de teclado por frame** (`yield return null` entre `Press` sucesivos). Dos `Press` en el mismo frame sobrescriben el estado del teclado.
- Las pruebas PlayMode que cargan una escena deben descargarla en `[UnityTearDown]`; si no, su jugador y su geometría contaminan las pruebas siguientes.

## Estructura
```
Assets/_EclipseGenesis/
  Scripts/            EclipseGenesis.Runtime.asmdef  (namespace EclipseGenesis.*)
    Player/           input, movimiento, cámara
    Core/             (reservado: GameManager, eventos, servicios)
    Editor/           EclipseGenesis.Editor.asmdef: generadores de escenas y herramientas de QA
  Input/              EclipseGenesisControls.inputactions (mapa "Player")
  Data/               ScriptableObjects de balance (MovementSettings, y más adelante WeaponData…)
  Prefabs/  Scenes/  Materials/
  Tests/EditMode  Tests/PlayMode
Assets/Settings/      assets de HDRP (High Fidelity / Balanced / Performant) y perfil de cielo y niebla
Docs/                 GDD, arquitectura, controles, roadmap, assets pendientes
```

## Reglas de código
- Clases pequeñas con una sola responsabilidad. Namespaces `EclipseGenesis.<Sistema>`.
- Los valores de balance van en **ScriptableObjects** (`Data/`), no hardcodeados en los componentes.
- La entrada se lee **solo** a través de `PlayerInputReader`. Ningún sistema toca `InputAction` directamente.
- Las referencias se asignan como campos `[SerializeField] private`. No uses `Find*` ni `GetComponent` en `Update`.
- En código de Editor usa `TryGetComponent`, no `GetComponent() ?? AddComponent()` (por los "fake null" de Unity).
- Las escenas y prefabs provisionales se **generan por código** (`FpsTestSceneBuilder`) para que sean reproducibles. Si cambias el layout, cambia el generador.
- Para los materiales HDRP creados por script: `HDMaterial.ValidateMaterial`. Para la emisión: `SetUseEmissiveIntensity(true)` antes de `SetEmissiveIntensity`.
- Orden de ejecución: `PlayerInputReader` (-100) → `PlayerLook` (-80) → `PlayerMovement` (-50) → `PlayerCameraEffects` (100, LateUpdate).

## Definición de terminado
Un sistema solo está terminado si: compila sin errores ni advertencias de C#, tiene prueba EditMode o PlayMode cuando es posible, las pruebas pasan en batchmode y [Docs/ROADMAP.md](Docs/ROADMAP.md) recoge lo que se verificó realmente. No declares objetivos de rendimiento sin haberlos medido.
