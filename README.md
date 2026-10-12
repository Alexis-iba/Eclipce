# ECLIPSE GENESIS

Shooter FPS táctico de ciencia ficción para Windows PC, en Unity 6 + HDRP.
Estado actual: **Fase 2 en curso**. El controlador FPS está jugable en una escena de pruebas con geometría provisional. Todavía no hay armas, enemigos ni misión. Consulta [Docs/ROADMAP.md](Docs/ROADMAP.md).

## Requisitos

| Elemento | Versión |
|---|---|
| Unity Editor | **6000.4.8f1** (Unity 6) con el módulo Windows Build Support |
| Unity Hub | 3.x, con sesión iniciada (lo exige la licencia, también en batchmode) |
| Render pipeline | HDRP 17.4.0 (High Definition RP) |
| Paquetes clave | Input System 1.19.0, AI Navigation 2.0.12, Test Framework |
| GPU | Compatible con DX12/DX11 y HDRP |

## Abrir y jugar la escena de pruebas

1. En Unity Hub, pulsa **Add → Add project from disk** y elige `C:\Users\ibarr\EclipseGenesis`.
2. Abre el proyecto con 6000.4.8f1. La primera importación tarda varios minutos porque compila los shaders de HDRP.
3. Abre `Assets/_EclipseGenesis/Scenes/EG_FPS_Test.unity` y pulsa **Play**.
4. Haz clic en la vista Game para capturar el ratón. Controles en [Docs/CONTROLS.md](Docs/CONTROLS.md).
5. **F1** muestra u oculta el panel de depuración (estado, velocidad, altura, FOV, FPS).

Para regenerar la escena y el prefab del jugador desde código usa el menú **Eclipse Genesis → Build → EG_FPS_Test Scene**.

## Línea de comandos (sin abrir el Editor)

El Editor no puede tener el proyecto abierto mientras se ejecutan estos comandos, y Unity Hub debe estar en marcha.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe"
$proj  = "C:\Users\ibarr\EclipseGenesis"

# Compilar y regenerar escena + prefab
& $unity -batchmode -nographics -projectPath $proj -executeMethod EclipseGenesis.EditorTools.FpsTestSceneBuilder.BuildFromCommandLine -logFile "$proj\Logs\build-sandbox.log"

# Pruebas (EditMode y PlayMode)
& $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode -assemblyNames EclipseGenesis.Tests.EditMode -testResults "$proj\Logs\tests-EditMode.xml"
& $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform PlayMode -assemblyNames EclipseGenesis.Tests.PlayMode -testResults "$proj\Logs\tests-PlayMode.xml"

# Captura de la cámara del jugador a PNG (necesita GPU: sin -nographics)
& $unity -batchmode -projectPath $proj -executeMethod EclipseGenesis.EditorTools.SceneCaptureTool.CaptureFromCommandLine -captureOut "$proj\Logs\sandbox-capture.png"
```

## Documentación

- [CLAUDE.md](CLAUDE.md): convenciones y reglas de trabajo
- [Docs/GDD.md](Docs/GDD.md): documento de diseño
- [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md): sistemas y dependencias
- [Docs/CONTROLS.md](Docs/CONTROLS.md): controles
- [Docs/ROADMAP.md](Docs/ROADMAP.md): progreso por fase
- [Docs/ASSETS_REQUIRED.md](Docs/ASSETS_REQUIRED.md): modelos, animaciones y sonidos pendientes
