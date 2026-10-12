# Eclipse OpenZone — primera implementación
Fecha: 12 de octubre de 2026.

## Escena y conservación
Escena: Assets/_EclipseGenesis/Scenes/Eclipse_OpenZone.unity.
Derivada de Genesis_Research_Facility.unity sin modificar la escena fuente ni el prefab del jugador, el arma o los Stalker.
OpenZone se añadió como primera escena habilitada en Build Settings; las escenas anteriores siguen disponibles.
Perfil HDRP y NavMesh independientes. No ejecutar FacilitySceneBuilder para regenerar esta escena.

## Implementado
- Exterior plano de 180 × 100 m conectado al laboratorio existente.
- Seis módulos explorables con entradas, ventanas abiertas, particiones, mesas, suministros y luces.
- Caminos, accesos laterales, barricadas, cercado, rocas, arbustos provisionales, vehículo militar provisional y ocho cápsulas orgánicas.
- Despliegue exterior en (0, 0.15, -88), entrada automática desbloqueada.
- Sol cálido, cielo GradientSky, niebla moderada, material de carretera y reflection probe exterior de 128 px.
- Tres Stalker adicionales en exterior; se conservan los tres interiores.
- EG-47, manos, ADS, retroceso, disparo, recarga, sonido, HUD y misión existentes reutilizados.
- Datos nuevos VX-9, BR-12, NS-50 y PLASMA-X. Sin modelo ni audio final; no equipados. PLASMA-X aún no tiene proyectil de plasma.
- NavMesh reconstruido excluyendo temporalmente jugador, aliens y colliders de puertas móviles. Colliders restaurados después; puertas conservan obstáculos de carving.

## Evidencia de verificación
Pruebas realizadas en Play Mode de OpenZone mediante Unity MCP:
- Movimiento W enviado al Input System: jugador avanzó desde z=-88 hasta z=-33.70.
- Disparo automático: ShotsFired aumentó; Magazine y HUD AmmoText coincidieron (4).
- ADS: AimAmount=1, FOV=53.04, mira en viewport (0.50, 0.51).
- Arma y brazos visibles en captura de Camera.main.
- TryReload iniciado después de disparar; comprobación posterior: Magazine=30, IsReloading=false.
- Stalker recibió daño por ApplyDamage y murió con estado Dead. Esta prueba usa el sistema de daño directamente; no prueba por sí sola un impacto hitscan.
- Seis agentes sobre NavMesh; observados Patrol y Search. No se comprobó cada transición de ataque.
- Door_BlastSealed abrió al acercar el jugador.
- Al entrar al trigger del laboratorio, objetivo cambió a Localiza la puerta de seguridad (índice 1).
- NavMesh.CalculatePath desde despliegue al interior: PathComplete después del horneado final.
- Consola consultada después de ejecución final: cero entradas de error o warning.
- No se modificó C#; no se ejecutó la suite completa de tests ni un build standalone.

Captura final: Captures/OpenZone/OpenZone_Final.png.

## Pendiente — no declarar transformación AAA terminada
- Terreno con relieve y texturas de suelo correctamente mezcladas; exterior actual es plano.
- Modelos finales de edificios, EG-47 y brazos riggeados. Geometría actual es placeholder y aún tiene aspecto de prototipo.
- Vegetación real, ventanas/cristales, puertas de módulos, decoración y decals exteriores.
- Ravager y Spitter: todavía no implementados.
- Nuevas animaciones y audios finales; sistemas existentes usan presentación procedural.
- Nueva secuencia de datos → energía → defensa → extracción exterior y checkpoints: no implementada. Actualmente sigue la misión interior original (eliminar Stalker, terminal, salida, extracción).
- HUD nuevo y minimapa: no implementados; se conserva el HUD funcional actual.
- Verificación completa de victoria, derrota, impactos, ataques, sprint y transiciones interiores/exteriores.
- Perfilado de FPS y memoria en build, distintas calidades y hardware objetivo.

## Rendimiento
Una captura de estadísticas del Editor informó ~1.07 millones de triángulos, 2317 shadow casters y ~3.62 GB en render textures. Draw calls y batches se reportaron como cero, por lo que esa captura no sirve para certificar FPS ni número total de llamadas.
Posibles cuellos de botella: sombras de geometría interior heredada, muchas luces/probes HDRP y alto uso de render textures. No se ha certificado el objetivo de rendimiento.
Exterior: sin partículas nuevas, una luz direccional con sombras, seis luces interiores sin sombras, un probe de baja resolución que actualiza al despertar.

## Prueba manual
1. Abrir Eclipse_OpenZone y Play. WASD/mouse; probar sprint, ADS con botón derecho, disparo izquierdo y R.
2. Recorrer los seis módulos y ambos lados de la carretera. Revisar colisiones y visibilidad del arma.
3. Combatir patrullas exteriores y acceder a Genesis al norte.
4. Seguir todos los objetivos interiores hasta extracción; probar derrota y reinicio.
5. Revisar sonido, HUD, consola, tiempos de frame y VRAM. Sustituir placeholders antes de evaluar realismo final.
