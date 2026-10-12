# Roadmap y estado real

Leyenda: ✅ hecho y verificado · 🟡 en curso · ⏳ pendiente

## FASE 1: Preparación del proyecto ✅ (2026-10-12)
| Tarea | Resultado verificado |
|---|---|
| Conexión MCP con Unity | ❌ **No existe** en esta sesión. Se decidió usar Unity en batchmode por línea de comandos (compilación, generadores y pruebas). |
| Proyecto | No existía ningún proyecto EclipseGenesis. Se creó en `C:\Users\ibarr\EclipseGenesis` a partir de la plantilla HDRP oficial (`3d-high-end 17.0.7`). Se eliminaron la escena y el tutorial de ejemplo. |
| Versión de Unity | 6000.4.8f1 (es el único Editor instalado) |
| Render pipeline | HDRP 17.4.0, espacio de color lineal, 3 niveles de calidad. Verificado con una prueba EditMode. |
| Paquetes | Input System 1.19.0 (único backend de entrada), AI Navigation 2.0.12, Test Framework, Timeline y uGUI. Se quitaron Visual Scripting y Collab. |
| Input Actions | `EclipseGenesisControls.inputactions` con 13 acciones y 2 esquemas. Verificado con una prueba EditMode. |
| Git | Repositorio propio en la carpeta del proyecto, con `.gitignore` para Unity |
| Consola | Compilación sin errores ni advertencias de C# (`Logs/build-sandbox.log`) |

## FASE 2: Controlador FPS y cámara 🟡
✅ **Verificado con 9 pruebas PlayMode** usando teclado y ratón simulados sobre los bindings reales:
aterrizar y quedar en el suelo · aceleración hasta 4,6 m/s y frenado · sprint (6,6) y sprint táctico con doble toque (8,2) · agacharse y no poder levantarse bajo un techo de 1,3 m · deslizamiento con impulso que termina agachado · salto de 1,15 m ± 0,12 · no atravesar paredes ni acumular velocidad contra ellas · subir escaleras de 0,2 m · mirar con el ratón con el pitch limitado a ±85°.
✅ La escena `Sandbox_Movement` se genera por código y se renderiza en HDRP (captura en `Logs/sandbox-capture.png`).
⏳ **Pendiente: tu prueba manual en el Editor** para la *sensación* (aceleración, balanceo y FOV). Las pruebas automáticas validan la física, no si los controles se sienten bien.
⏳ Las pendientes de 20°, 35° y 55° no tienen todavía una prueba automática (la escena las incluye para probarlas a mano).

## FASE 3: Armas ⏳
WeaponData (SO), WeaponController, disparo por raycast, munición y recarga, ADS (`IsAiming` + FOV + sensibilidad), retroceso con `PlayerLook.AddRotation`, EG-47 primero.
## FASE 4: Salud, escudo y daño ⏳
## FASE 5: Primera IA (Stalker) ⏳
## FASE 6: Laboratorio jugable (geometría provisional + NavMesh) ⏳
## FASE 7: HUD y pausa ⏳
## FASE 8: Misión "Primer Contacto" y checkpoints ⏳
## FASE 9: Animaciones y efectos audiovisuales ⏳
## FASE 10: Optimización y build de Windows ⏳ (aquí se definirá y medirá la PC objetivo)
