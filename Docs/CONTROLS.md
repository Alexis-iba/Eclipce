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
| Clic izquierdo | Disparar | RT / R2 | ⏳ Asignada, sin sistema (Fase 3) |
| Clic derecho | Apuntar | LT / L2 | ⏳ Asignada, sin sistema (Fase 3). La cámara ya se estabiliza cuando el arma active `IsAiming` |
| R | Recargar | X / Cuadrado | ⏳ Asignada, sin sistema (Fase 3) |
| 1 / 2 / 3 | Cambiar de arma (ranura directa) | Cruceta izquierda / derecha · LB | ⏳ Asignada, sin sistema (Fase 3) |
| Rueda del ratón | Arma siguiente / anterior | Y / Triángulo | ⏳ Asignada, sin sistema (Fase 3) |
| F | Interactuar | Cruceta arriba | ⏳ Asignada, sin sistema (Fase 6) |
| G | Granada | RB / R1 | ⏳ Asignada, sin sistema (Fase 3) |
| T | Inspeccionar el arma | Cruceta abajo | ⏳ Asignada, sin sistema (Fase 9) |
| Escape | Pausa | Start | ⏳ Asignada, sin sistema (Fase 7) |

Solo en la escena de pruebas `EG_FPS_Test`:

| Tecla | Acción |
|---|---|
| F1 | Mostrar u ocultar el panel de depuración |
| [ y ] | Bajar o subir la sensibilidad del ratón (de ×0,1 a ×5; se guarda en PlayerPrefs) |

Las acciones ⏳ están definidas y asignadas en el asset, pero ningún sistema las consume todavía: no simulan nada.
En el Editor, **Esc** libera el ratón (comportamiento estándar de la vista Game). En el ejecutable, mientras no exista el menú de pausa, el juego se cierra con **Alt+F4**.
