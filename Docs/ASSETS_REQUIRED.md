# Assets necesarios (modelos, animaciones y sonidos)

Ahora mismo **todo es provisional**: cubos con materiales HDRP/Lit de color plano y ningún audio. Esta lista indica qué hay que sustituir y con qué requisitos. No se descargará ningún asset de pago ni de terceros sin autorización.

| Prioridad | Asset | Sustituye a | Requisitos | Fase |
|---|---|---|---|---|
| Alta | Brazos FPS con rig humanoide | (nada todavía) | Rig compatible con Animation Rigging, manos con IK, menos de 15k tris, PBR de 2K | 3 / 9 |
| Alta | EG-47 (modelo del arma) | Cubo provisional (Fase 3) | Pivote en la empuñadura, sockets: `Muzzle`, `ShellEject`, `Sight`, `Magazine`; LOD0 de 10 a 20k tris | 3 |
| Alta | Animaciones del EG-47 | — | Reposo, caminar, correr, sprint, sprint táctico, disparo, recarga (con y sin balas en la recámara), desenfundar/guardar, inspección y deslizamiento | 9 |
| Alta | Stalker (modelo y animaciones) | Cápsula provisional (Fase 5) | Rig genérico; animaciones de reposo, caminar, correr, ataque, reacción al impacto, muerte y "olfatear" (búsqueda); hitboxes de cabeza, torso y extremidades | 5 / 9 |
| Alta | Kit modular del laboratorio | Cubos de la escena de pruebas | Rejilla de 1 m, módulos de 4 m (paredes, suelo, techo, puerta corredera, terminal, marcos), materiales de metal desgastado | 6 |
| Media | VFX | — | Fogonazo, trazadoras, impactos (metal, hormigón, carne alienígena), casquillos, sangre alienígena | 3 / 9 |
| Media | Decals de impacto | — | Agujeros de bala para metal y hormigón (HDRP Decal Projector) | 3 |
| Media | Audio: armas | — | Disparo cercano y lejano, mecánica de recarga, gatillo sin munición, cola de reverberación en interiores | 9 |
| Media | Audio: jugador | — | Pasos por superficie (metal, hormigón, rejilla), respiración, aterrizaje, deslizamiento | 9 |
| Media | Audio: aliens y ambiente | — | Gruñidos y ataques del Stalker, alarma, zumbido del laboratorio, música de exploración y de combate | 9 |
| Baja | VX-9, BR-12, NS-50 y PLASMA-X | — | Mismos sockets que el EG-47 | 3+ |
| Baja | Ravager, Spitter, Phantom y Eclipse Prime | — | — | Campaña |

Las fuentes candidatas (Unity Asset Store gratuito, Mixamo para pruebas de rig, librerías CC0) se propondrán antes de importar nada.
