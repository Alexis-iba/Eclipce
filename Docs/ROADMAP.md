# Roadmap y estado real

Leyenda: ✅ hecho y verificado · 🟡 implementado, falta verificación manual · ⏳ pendiente

## FASE 1: Preparación del proyecto ✅ (2026-10-12)
| Tarea | Resultado verificado |
|---|---|
| Conexión MCP con Unity | ❌ **No existe.** Comprobado de nuevo el 2026-10-12: la sesión solo tiene conectados phaser-game-agent, Claude Docs, visualize y scheduled-tasks, y no hay ningún servidor de Unity en la configuración de Claude ni en el proyecto. Unity se controla con `Unity.exe -batchmode` (generadores con `-executeMethod`, Play Mode con `-runTests`). |
| Proyecto | `C:\Users\ibarr\EclipseGenesis`, creado desde la plantilla HDRP oficial. El código y los assets propios están en `Assets/_EclipseGenesis/`. |
| Versión de Unity | 6000.4.8f1 |
| Render pipeline | HDRP 17.4.0, espacio de color lineal, 3 niveles de calidad (prueba EditMode) |
| Paquetes | Input System 1.19.0 (único backend de entrada), AI Navigation 2.0.12, Test Framework |
| Git | Repositorio propio en la carpeta del proyecto |

## FASE 2: Controlador FPS táctico ✅ automático · 🟡 pendiente de tu prueba de sensación (2026-10-12)

### Sistemas terminados
| Sistema | Archivo | Verificación |
|---|---|---|
| Jugador `EG_Player` (CharacterController + Input System, cámara FPS, `WeaponHolder`) | `Prefabs/Player/EG_Player.prefab` | EditMode: sin referencias vacías y con WeaponHolder. PlayMode: el jugador está en la escena y su cámara es `Camera.main` |
| Lectura de la entrada | `PlayerInputReader.cs` | EditMode: las 16 acciones tienen bindings y las teclas coinciden con el diseño (Ctrl, F, 1/2/3, G, Esc, Shift, R) |
| Movimiento con aceleración y frenado suaves | `PlayerMovement.cs` | PlayMode: llega a 4,6 m/s y frena hasta detenerse |
| Sprint y sprint táctico | `PlayerMovement.cs` | PlayMode: 6,6 m/s y, con doble toque, 8,2 m/s; se corta al soltar "adelante" |
| Agacharse (Ctrl) | `PlayerMovement.cs` | PlayMode: la cápsula baja a 1,15 m y no se levanta bajo un techo de 1,3 m |
| Deslizamiento con impulso y fricción | `PlayerMovement.cs` | PlayMode: arranca por encima de la velocidad de sprint y termina agachado por sí solo |
| Salto y gravedad | `PlayerMovement.cs` | PlayMode: salta 1,15 m ± 0,12 y aterriza |
| Colisiones | CharacterController + recorte de velocidad | PlayMode: no atraviesa paredes ni acumula velocidad contra ellas; sube escaleras de 0,2 m |
| Cámara con el ratón | `PlayerLook.cs` | PlayMode: el giro horizontal es exacto y el vertical se detiene en ±85° |
| Sensibilidad configurable | `PlayerLook.SensitivityMultiplier` (PlayerPrefs) | PlayMode: ×0,5 reduce el giro a la mitad y el valor queda guardado |
| FOV dinámico, balanceo sutil, caída al aterrizar e inclinación al deslizarse | `PlayerCameraEffects.cs` | PlayMode, en la escena real: el balanceo al andar está entre 0,5 y 5 cm |
| Cámara estable al apuntar | `PlayerCameraEffects` (`aimMotionScale`) | PlayMode: con `IsAiming` el balanceo baja a menos del 40 % (lo activará el arma en la Fase 3) |
| Restricción de movimientos incompatibles | `PlayerMovement.cs` | PlayMode: no se puede iniciar el sprint en el aire. Por diseño, además: no se esprinta agachado, apuntando ni hacia atrás; solo se desliza desde un sprint en el suelo; no se levanta sin espacio; saltar agachado pone de pie |
| Escena `EG_FPS_Test` (suelo, paredes, rampas de 20°/35°/55°, escaleras, túnel bajo, cajas, columnas, iluminación HDRP) | `Scenes/EG_FPS_Test.unity`, generada por `FpsTestSceneBuilder` | PlayMode: la escena se abre y se juega sin errores en consola; el jugador camina más de 4 m |

**Resultado de la última ejecución:** 14/14 pruebas EditMode y 13/13 PlayMode, con 0 errores y 0 advertencias de compilación.

### Falta (no está verificado)
- 🟡 **Tu prueba manual de sensación** en el Editor o en el ejecutable: si la aceleración, el balanceo y el FOV se sienten naturales. Las pruebas automáticas validan la física, no la sensación.
- ⏳ No hay prueba automática de las rampas (20° subible y 55° bloqueada) ni del sprint táctico agotándose a los 4 s.
- ⏳ El apuntado real (clic derecho) llega con el arma en la Fase 3. Hoy solo existe el mecanismo de la cámara.

## FASE 3: Armas ⏳
WeaponData (ScriptableObject), WeaponController, disparo por raycast, munición y recarga, ADS (`IsAiming`, FOV y sensibilidad), retroceso con `PlayerLook.AddRotation`, ranuras 1/2/3 y granada. Se empieza por el EG-47.
## FASE 4: Salud, escudo y daño ⏳
## FASE 5: Primera IA (Stalker) ⏳
## FASE 6: Laboratorio jugable (geometría provisional y NavMesh) ⏳
## FASE 7: HUD y pausa ⏳
## FASE 8: Misión "Primer Contacto" y checkpoints ⏳
## FASE 9: Animaciones y efectos audiovisuales ⏳
## FASE 10: Optimización y build de Windows ⏳ (ya existe un build de prueba; la PC objetivo está por definir y medir)
