# Roadmap y estado real

Leyenda: ✅ hecho y verificado · 🟡 implementado, falta verificación manual · ⏳ pendiente

## FASE 1: Preparación del proyecto ✅ (2026-10-12)
| Tarea | Resultado verificado |
|---|---|
| Conexión MCP con Unity | ✅ **Conectada y verificada** (2026-10-12). MCP for Unity 10.3.0, transporte HTTP en `http://127.0.0.1:8080/mcp`, registrada con `claude mcp add --scope local`. Prueba real: lectura de la escena, la jerarquía y la consola, y creación de `EG_MCP_CONNECTION_TEST` en (0, 2, 0) guardado en `Scenes/Tests`. Prueba en frío: con Unity cerrado y el servidor detenido, al abrir Unity el servidor arrancó solo en 31 s y respondió por MCP en 32 s. |
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

## Mejora visual: Genesis_Research_Facility ✅ automático · 🟡 pendiente de tu recorrido (2026-10-12)
La escena se genera con **Eclipse Genesis → Build → Genesis Research Facility** (`FacilitySceneBuilder`). Antes de generar, `EG_FPS_Test` se copia en `Scenes/Backups`. Es la primera escena del ejecutable.

| Elemento | Estado verificado |
|---|---|
| 6 zonas a escala FPS: pasillo de acceso, armería, pasillo militar, laboratorio central, sala de experimentos alienígenas y zona de combate con coberturas | ✅ Capturas de las 6 vistas (`Logs/qa`). PlayMode: las 7 zonas son alcanzables por NavMesh desde el punto de inicio |
| 7 puertas automáticas (5 interiores y 2 selladas con luz roja) | ✅ PlayMode: la puerta se abre del todo, el jugador entra en la armería y la puerta sellada nunca se abre |
| Materiales PBR HDRP en la paleta pedida (grafito, gris metálico, acero, rojo oscuro, azul tecnológico) | ✅ EditMode: todos los renderers usan shaders HDRP. Texturas **procedurales y provisionales** |
| Iluminación: focos de techo, balizas rojas, exposición fija EV 4,4, niebla volumétrica, SSAO, SSR, ACES, bloom ≤ 0,2, reflection probes | ✅ EditMode: presupuesto de 59 luces (17 con sombra) y perfil de postproceso completo. Se corrigió que estuviera demasiado oscuro (reflector de los focos desactivado y metal en superficies pintadas) |
| Ambiente: humo local, chispas, alarmas con balizas, luces que parpadean, pantallas animadas y sonido ambiente | ✅ PlayMode: alarmas activas, las chispas disparan y hay luces con parpadeo |
| Colisiones | ✅ EditMode: toda la arquitectura tiene collider. PlayMode: el jugador camina, esprinta y se desliza dentro del mapa |
| Rendimiento | ✅ Medido en el ejecutable (RTX 4070 Laptop, 2560×1600, High Fidelity, DX11): **64–79 FPS** de media en el punto de inicio. ⚠️ Solo se midió en ese punto, no recorriendo el mapa |
| **DirectX 12** | ❌ El ejecutable **se cierra con un crash al cargar la instalación en D3D12**. En D3D11 funciona. El build usa D3D11 como API principal hasta investigarlo |

**Falta:** tu recorrido manual (sensación y legibilidad de la iluminación), medir el rendimiento recorriendo todo el mapa, investigar el crash en D3D12 y sustituir la geometría y las texturas provisionales (ver `ASSETS_REQUIRED.md`).

## "Fase 4" pedida: PRIMER COMBATE REAL (cubre las fases 3, 4, 5, 7 y 8 del plan original) ✅ automático · 🟡 pendiente de tu partida (2026-10-12)
Todo se regenera con **Eclipse Genesis → Build → Everything** (`FullBuild`). Se generó por MCP en el Editor abierto.

| Sistema | Qué hay | Verificación real |
|---|---|---|
| **Rifle EG-47** | Modelo **provisional** de más de 30 piezas (cajón de mecanismos, guardamanos con luces azules, freno de boca, cargador, culata, mira holográfica) con brazos y guantes, en `Prefabs/Weapons/EG_Weapon_EG47.prefab`. Datos en `Data/Weapons/EG47.asset` (28 de daño, 650 RPM, 30/120, recarga de 2,3 s) | PlayMode: visible en la mitad inferior derecha; dispara en automático; cada disparo gasta una bala; la recarga deja 30 y descuenta la reserva; con el ADS la mira queda centrada (±3 %), baja el FOV y la dispersión; el retroceso sube la cámara. Captura del ejecutable: arma y HUD visibles |
| Animación del arma | Procedural (no hay clips): cadera/ADS, inercia al mirar, balanceo, sprint y sprint táctico, retroceso, recarga con el cargador fuera y la mano siguiéndolo, desenfundado, inspección (T) y retirada junto a paredes | Verificación visual en el ejecutable. La sensación necesita tu partida |
| Efectos y sonido | Fogonazo con luz, trazadoras, chispas de impacto, sangre alienígena, casquillos físicos; sonidos sintetizados de disparo, recarga, gatillo en vacío y hitmarker | El código se ejecuta en las pruebas de disparo. Calidad audiovisual pendiente de tu valoración |
| **Salud y escudo** | `PlayerHealth`: 100 de salud y 100 de escudo, que se regenera tras 4 s sin daño | PlayMode: el daño baja primero el escudo y el HUD lo refleja; con 0 de salud llega la derrota |
| **Stalker** (`Prefabs/Aliens/EG_Alien_Stalker.prefab`) | Criatura original **provisional**: 4 patas articuladas, armadura biológica, 4 ojos bioluminiscentes con luz y garras. NavMeshAgent, 6 hitboxes (cabeza ×2,2, cuerpo ×1, patas ×0,7), 120 de vida | EditMode: prefab completo. PlayMode: muere a disparos con hitmarker y deja de navegar al morir |
| IA del Stalker | Idle → Patrol → Alert → Chase → Attack → Search, con visión (cono de 140°, 24 m, rayo de línea de visión), oído (disparos) y reacción al daño | PlayMode: detecta a 16 m, persigue y ataca; **no ve a través de paredes**; el golpe **solo conecta dentro de su alcance** (comprobado al impactar). Captura del ejecutable: un Stalker atacó y bajó el escudo a 28 |
| Sala de investigación | La zona de combate ahora tiene 4 cápsulas, 3 pantallas de seguridad, la terminal de emergencia, coberturas, contenedores, balizas rojas y un pasillo de salida con zona de extracción | EditMode: escena sin referencias rotas. Los 3 Stalker aparecen a más de 6 m del jugador |
| **HUD** | Escudo/salud, arma/munición/recarga, retícula dinámica, hitmarker (rojo al matar), indicadores de daño direccionales, viñeta con salud baja, objetivo con aviso, `[F]` para interactuar, pantallas de victoria y derrota | PlayMode: la munición, el escudo y la salud muestran valores reales; aparecen el hitmarker y las pantallas finales |
| **Misión OPERACIÓN PRIMER CONTACTO** | 7 objetivos: explorar el laboratorio → localizar la puerta de seguridad (se desbloquea) → entrar en la sala (se cierra detrás) → eliminar a los 3 Stalker (0/3) → activar la terminal (F) → abrir la salida → llegar a la extracción | PlayMode: **misión completada de principio a fin** con victoria; **derrota** al morir el jugador |

**Pruebas (última ejecución completa, 2026-10-12):** 26/26 EditMode y 27/27 PlayMode en verde, con 0 errores y 0 advertencias de compilación.

**Correcciones encontradas al verificar:**
- Unity recompilaba los scripts en mitad del Play Mode, lo que rompía el Input System y la lista de armas. Ahora recompila al salir de Play.
- `Run In Background` estaba desactivado: el Play Mode se pausaba cuando el Editor no tenía el foco.
- Un menú con "ó" no se ejecutaba por MCP por la codificación.

**Pendiente (no verificado o no hecho):**
- Tu partida para valorar sensación, dificultad, legibilidad y sonido.
- Modelos y animaciones reales del arma, de los brazos y del Stalker (ver `ASSETS_REQUIRED.md`).
- Granada (la G no hace nada todavía), menú de pausa (Esc), checkpoints.
- Las otras armas (VX-9, BR-12, NS-50, PLASMA-X).
- Medir el rendimiento con combate en toda la misión: el registro muestra unos 43–48 FPS de media a 2560×1600 en High Fidelity durante los primeros segundos, que incluyen la compilación de shaders.
- Investigar el crash en D3D12.

## Transformación hacia el concept art (mapa abierto, Blender + Poly Haven) 🟡 en curso (2026-10-12)

**Hecho y verificado** (compilación limpia, capturas QA en `Logs/qa/`, EditMode 26/26, PlayMode 32 superados + 2 omitidos + 1 fallo intermitente de salto que pasa al repetirlo solo):
- HUD rediseñado según la imagen: radar con brújula, rumbo y distancia al objetivo; lista completa de "Operación Primer Contacto"; escudo por placas; tarjeta de arma con silueta y huecos de granada.
- `Genesis_Outpost` (Build → Genesis Outpost): casas de madera de Blender amuebladas, pinos, rocas, suelos escaneados, montañas de fondo y atardecer bajo detrás de la base Genesis. La operación del mapa abierto está conectada: zona residencial → base → terminal de datos → energía → Stalkers → salida → extracción en el helipuerto.
- Corregido un error de terreno: `Mathf.SmoothStep` se usaba como un smoothstep de GLSL y el mapa salía plano.

**Pendiente:** EG-47 en Blender, aliens con IA 3D (clave de API), cúpula con haz y depósitos de agua, nave estrellada, helicóptero, fuego, coches quemados reales, granadas. También:
- Hacer de `Genesis_Outpost` la escena inicial: ahora lo es `Eclipse_OpenZone`, de Codex.
- Tu partida en el mapa abierto.
- Revisar `ProceduralTextures`: usa `Mathf.SmoothStep` igual que el terreno, pero define el aspecto ya validado de la instalación.

## FASE 6: Laboratorio jugable ✅ (cubierto por Genesis_Research_Facility y la sala de investigación)
## FASE 7: Menú de pausa y configuración ⏳ (el HUD ya está hecho)
## FASE 8: Checkpoints ⏳ (la misión ya está hecha)
## FASE 9: Animaciones y efectos audiovisuales ⏳
## FASE 10: Optimización y build de Windows ⏳ (ya existe un build de prueba; la PC objetivo está por definir y medir)
