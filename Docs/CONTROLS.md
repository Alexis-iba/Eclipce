# Controles

Asset: `Assets/_Project/Input/EclipseGenesisControls.inputactions`, mapa **Player**, esquemas **Keyboard&Mouse** y **Gamepad**.

| Acción | Teclado y ratón | Mando | Estado |
|---|---|---|---|
| Moverse | W A S D | Stick izquierdo | ✅ Implementado |
| Mirar | Ratón | Stick derecho (220°/s) | ✅ Implementado |
| Saltar | Espacio | A / Cruz | ✅ Implementado |
| Sprint | Shift izquierdo (pulsar una vez) | L3 | ✅ Implementado |
| Sprint táctico | Doble toque de Shift mientras esprintas | Doble L3 | ✅ Implementado |
| Agacharse / levantarse | C o Ctrl izquierdo | B / Círculo | ✅ Implementado |
| Deslizarse | Agacharse durante el sprint | B durante el sprint | ✅ Implementado |
| Disparar | Clic izquierdo | RT / R2 | ⏳ Fase 3 |
| Apuntar (ADS) | Clic derecho | LT / L2 | ⏳ Fase 3 |
| Recargar | R | X / Cuadrado | ⏳ Fase 3 |
| Cambiar de arma | Rueda del ratón | Y / Triángulo | ⏳ Fase 3 |
| Lanzar granada | G | RB / R1 | ⏳ Fase 3 |
| Inspeccionar arma | T | Cruceta abajo | ⏳ Fase 9 |
| Interactuar | E | Cruceta arriba | ⏳ Fase 6 |
| Pausa | Escape | Start | ⏳ Fase 7 |
| Panel de depuración | F1 | — | ✅ Solo en la escena de pruebas |

Las acciones marcadas con ⏳ ya están definidas y enlazadas en el asset, pero ningún sistema las consume todavía.
Mientras no exista el menú de pausa, en el Editor se libera el ratón con **Esc** (comportamiento estándar de la vista Game).
