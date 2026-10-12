# ECLIPSE GENESIS: documento de diseño (GDD)

> Documento vivo. Las secciones marcadas *(planificado)* todavía no están implementadas. El estado real está en [ROADMAP.md](ROADMAP.md).

## 1. Ficha
| | |
|---|---|
| Género | FPS táctico de acción, supervivencia y ciencia ficción militar |
| Plataforma | Windows PC (teclado y ratón; mando como secundario) |
| Motor | Unity 6 (6000.4.8f1), HDRP, C# |
| Modos | Campaña · Supervivencia · Extracción (un jugador; arquitectura preparada para multijugador posterior) |
| Referencia de sensación | Shooters tácticos modernos (respuesta de controles, claridad del HUD). Sin copiar nombres, mapas, interfaces ni assets protegidos. |

## 2. Pilares
1. **Controles que responden:** aceleración natural, sprint táctico, deslizamiento con inercia, retroceso controlable.
2. **Tensión de supervivencia:** recursos limitados, enemigos que cazan por percepción, escudo regenerable y salud que no se regenera.
3. **Ciencia ficción militar creíble:** tecnología desgastada, iluminación física, contraste entre lo humano (frío, industrial) y lo alienígena (orgánico, bioluminiscente).
4. **Legibilidad:** el jugador siempre sabe de dónde viene el daño, cuánta munición tiene y cuál es el objetivo.

## 3. Historia
Año **2187**. La organización militar **GENESIS** detecta una señal procedente de un planeta remoto. Al investigarla descubre una especie alienígena capaz de infectar organismos y controlar tecnología avanzada. Una base de investigación queda incomunicada.

El jugador es **ALEX RYDER**, operador de fuerzas especiales enviado a investigar. Descubre que el planeta alberga una entidad llamada **ECLIPSE PRIME**, el nodo que coordina la infección.

Arco de la campaña: investigar instalaciones → rescatar supervivientes → eliminar amenazas → recuperar tecnología experimental (PLASMA-X) → frenar la expansión → derrotar a Eclipse Prime.

## 4. Modos de juego
- **Campaña:** misiones lineales con objetivos, checkpoints, combate, exploración, cinemáticas y jefes. Primera misión: *Operación Primer Contacto* (§9).
- **Supervivencia** *(planificado)*: oleadas crecientes, puntos de abastecimiento, mejoras entre oleadas.
- **Extracción** *(planificado)*: mapa amplio, recolección, objetivos opcionales y zona de extracción con temporizador. Un solo jugador. Los sistemas se diseñan sin estado global oculto para facilitar el multijugador más adelante (no hay networking hasta que el modo individual sea estable).

## 5. Jugador
### 5.1 Movimiento (implementado, Fase 2)
| Acción | Valor por defecto | Notas |
|---|---|---|
| Caminar | 2,4 m/s | Al apuntar (ADS) o con el stick poco inclinado |
| Correr | 4,6 m/s | Velocidad base |
| Sprint | 6,6 m/s | Shift. Se mantiene activo hasta soltar "adelante" |
| Sprint táctico | 8,2 m/s | Doble toque de Shift. 4 s de duración, 6 s de recarga |
| Agachado | 2,2 m/s | Cápsula de 1,15 m. No se levanta si no hay espacio encima |
| Deslizamiento | impulso de 9,2 m/s | Agacharse durante el sprint. Cápsula de 0,9 m, fricción, aceleración en pendientes, salto para cancelarlo |
| Salto | 1,15 m | Coyote time de 0,12 s y buffer de 0,12 s. Saltar agachado pone de pie al jugador |
| Escalones / pendientes | 0,35 m / 45° | Por encima de 45° no se puede subir |

FOV vertical de 68° (≈103° horizontal en 16:9): +4° al esprintar, +8° en sprint táctico y +6° al deslizarse. El balanceo de cabeza y la caída al aterrizar son sutiles, con amortiguación crítica.

### 5.2 Salud y escudo *(planificado, Fase 4)*
Vida de 100 sin regeneración (solo con botiquines). Escudo de 100 que se regenera tras 4 s sin recibir daño. Multiplicadores por zona: cabeza ×2,0, torso ×1,0, extremidades ×0,75. Indicadores direccionales de daño, viñeta con la salud baja y respawn en el último checkpoint.

### 5.3 Armas *(planificado, Fase 3)*
Todas comparten un `WeaponController` y se diferencian por su `WeaponData` (ScriptableObject).

| Arma | Tipo | Daño | Cadencia (RPM) | Cargador / reserva | Recarga | Alcance efectivo | Balística |
|---|---|---|---|---|---|---|---|
| **EG-47** | Rifle de asalto | 28 | 650 | 30 / 120 | 2,3 s | 45 m | Raycast |
| **VX-9** | Subfusil | 20 | 900 | 32 / 160 | 1,9 s | 20 m | Raycast |
| **BR-12** | Escopeta | 9 × 9 perdigones | 70 | 8 / 32 | 0,5 s por cartucho | 10 m | Raycast múltiple |
| **NS-50** | Rifle de precisión | 120 | 45 | 5 / 25 | 3,2 s | 150 m | Raycast con caída simulada |
| **PLASMA-X** | Rifle de energía | 35 + daño en área | 300 | batería de 40 / se recarga sola | sobrecalentamiento | 35 m | Proyectil físico |

ADS con su propio FOV y sensibilidad por arma. El retroceso combina un patrón (vertical y horizontal) con una recuperación de cámara. La dispersión depende de la postura y del movimiento. Fogonazo, impactos por material, casquillos en las armas balísticas y puntos de montaje para accesorios.

## 6. Enemigos *(planificado, Fase 5 en adelante)*
Máquina de estados común: **Patrulla → Sospecha → Persecución → Ataque → Búsqueda → Retorno**. La percepción combina un cono de visión con raycast de oclusión y oído (disparos y sprint). La navegación usa NavMesh (AI Navigation).

| Especie | Rol | Vida | Velocidad | Ataque | Rasgo |
|---|---|---|---|---|---|
| **Stalker** | Cazador cuerpo a cuerpo | 120 | rápida | Zarpazo de 18 a 2 m | Detecta el movimiento y es más lento en detectar al jugador agachado |
| **Ravager** | Tanque | 600 + placas | lenta | Embestida de 45 | Placas destructibles y punto débil en la espalda |
| **Spitter** | Artillería | 150 | media | Proyectil de plasma de 22 con arco | Mantiene la distancia y se reposiciona |
| **Phantom** | Emboscador | 180 | muy rápida (con dash) | Golpe de 30 | Camuflaje parcial y ataque por los flancos |
| **Eclipse Prime** | Jefe final | 3 fases | — | — | Fase 1: invoca enemigos · Fase 2: barrido de energía · Fase 3: el escenario colapsa y aparecen núcleos expuestos |

## 7. Niveles
| # | Nivel | Identidad visual | Lecciones jugables |
|---|---|---|---|
| 1 | **Genesis Research Facility** | Laboratorio oscuro con luces rojas de emergencia, metal desgastado, puertas automáticas y terminales | Combate en pasillos, puertas y terminales |
| 2 | **Infected Forest** | Niebla volumétrica, flora bioluminiscente cian y verde | Visibilidad reducida, Phantoms |
| 3 | **Fallen Colony** | Colonia en ruinas, vehículos volcados, espacios abiertos | Combate a distancia, Spitters, coberturas |
| 4 | **Eclipse Core** | Arquitectura orgánica alienígena, pulsos de luz | Jefe final |

Construcción modular sobre una rejilla de 1 m (los módulos de pared, suelo y puerta de 4 m se reutilizan entre niveles).

## 8. Interfaz *(planificado, Fase 7)*
Paleta: negro, grafito, blanco y rojo para el peligro, con azul eléctrico como acento tecnológico. HUD: vida y escudo, munición y arma, retícula dinámica, brújula, objetivo, indicadores de daño y de enemigos, avisos de interacción y notificaciones. Escala con Canvas Scaler (referencia 1920×1080, adaptación por ancho y alto).
Menú principal: Campaña, Supervivencia (*no disponible*), Extracción (*no disponible*), Armamento, Configuración y Salir. Los modos no implementados se muestran deshabilitados.
Pausa (Escape): Continuar, Reiniciar misión, Configuración y Menú principal.
Configuración: resolución, pantalla completa, calidad (High Fidelity / Balanced / Performant), sensibilidad, volúmenes (general, música y efectos), FOV y subtítulos. Se guarda en JSON en `Application.persistentDataPath`.

## 9. Misión 1: OPERACIÓN PRIMER CONTACTO *(planificado, Fases 6 a 8)*
1. Ryder aparece en el laboratorio. Objetivo: *Investigar las instalaciones*.
2. Encuentra el **EG-47** (30 balas por cargador) en un armero.
3. Abre la puerta de seguridad.
4. Aparecen **3 Stalkers**. Objetivo: *Eliminar las amenazas (0/3)*.
5. Activa la **terminal de emergencia** y se abre la salida.
6. Llega a la zona de salida y aparece la pantalla **Misión completada**.
- **Derrota:** la vida llega a 0. Pantalla de fallo con opciones Reintentar (último checkpoint) y Menú.
- **Checkpoints:** al recoger el arma y al activar la terminal.

## 10. Audio *(planificado, Fase 9)*
AudioMixer con los grupos Master → Music, SFX (Weapons, Footsteps, Aliens, Impacts, UI) y Ambience, y snapshots de Exploración, Combate y Pausa. Audio espacial 3D para enemigos, impactos y alarmas. Pasos según el material de la superficie. Respiración al esprintar y con la salud baja.

## 11. Rendimiento
Objetivo inicial: **60 FPS a 1080p en calidad Balanced** en una PC de referencia **que aún hay que definir y medir** (propuesta: GPU de la clase RTX 3060, CPU de 6 núcleos y 16 GB de RAM). No se considerará alcanzado sin mediciones con el Profiler en un build de Windows.
