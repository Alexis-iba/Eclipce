# Controles

Asset: `Assets/_EclipseGenesis/Input/EclipseGenesisControls.inputactions`, mapa **Player**, esquemas **Keyboard&Mouse** y **Gamepad**.

| Tecla | Acción | Mando | Estado |
|---|---|---|---|
| W A S D | Movimiento | Stick izquierdo | ✅ Funcional |
| Ratón | Cámara | Stick derecho (220°/s) | ✅ Funcional |
| Shift izquierdo | Sprint (una pulsación lo activa) · doble toque = sprint táctico | L3 | ✅ Funcional |
| Ctrl izquierdo (alternativa: C) | Agacharse / levantarse | B / Círculo | ✅ Funcional |
| Ctrl durante el sprint | Deslizarse | B durante el sprint | ✅ Funcional |
| Espacio | Saltar (estando agachado, se levanta) | A / Cruz | ✅ Funcional |
| Clic izquierdo | Disparar (automático) | RT / R2 | ✅ Funcional (EG-47) |
| Clic derecho | Apuntar (ADS) | LT / L2 | ✅ Funcional: mira centrada, zoom, menos dispersión y cámara estable |
| R | Recargar (también automática al vaciar el cargador) | X / Cuadrado | ✅ Funcional |
| 1 / 2 / 3 | Cambiar de arma (ranura directa) | Cruceta izquierda / derecha · LB | 🟡 Sistema listo; solo existe el EG-47 en la ranura 1 |
| Rueda del ratón | Arma siguiente / anterior | Y / Triángulo | 🟡 Sistema listo; con una sola arma no cambia |
| F | Interactuar (terminal de emergencia) | Cruceta arriba | ✅ Funcional |
| G | Granada | RB / R1 | ⏳ Asignada, sin sistema |
| T | Inspeccionar el arma | Cruceta abajo | ✅ Funcional (animación procedural) |
| Escape | Pausa | Start | ⏳ Asignada, sin sistema. En el ejecutable se sale con Alt+F4 o desde las pantallas de victoria y derrota |

Herramientas de desarrollo (en cualquier escena):

| Tecla | Acción |
|---|---|
| F1 | Mostrar u ocultar el panel de depuración |
| [ y ] | Bajar o subir la sensibilidad del ratón (de ×0,1 a ×5; se guarda en PlayerPrefs) |

Las acciones ⏳ están definidas y asignadas en el asset, pero ningún sistema las consume todavía: no simulan nada.
En el Editor, **Esc** libera el ratón (comportamiento estándar de la vista Game). En el ejecutable, mientras no exista el menú de pausa, el juego se cierra con **Alt+F4**.

Parámetros de desarrollo del ejecutable: `-egSpawn x,y,z,giro` coloca al jugador en un punto concreto y `-egTrace` registra cada segundo la posición y la entrada en `Player.log`.
