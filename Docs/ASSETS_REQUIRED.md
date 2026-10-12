# Assets necesarios (modelos, animaciones y sonidos)

Ahora mismo **todo es provisional**: cubos con materiales HDRP/Lit de color plano y ningún audio. Esta lista indica qué hay que sustituir y con qué requisitos. No se descargará ningún asset de pago ni de terceros sin autorización.

| Prioridad | Asset | Sustituye a | Requisitos | Fase |
|---|---|---|---|---|
| Alta | Brazos FPS con rig humanoide | Brazos provisionales de cápsulas y cajas dentro de `EG_Weapon_EG47.prefab` | Rig compatible con Animation Rigging, manos con IK, menos de 15k tris, PBR de 2K | 3 / 9 |
| Alta | EG-47 (modelo del arma) | Modelo provisional de más de 30 primitivas (`EG_Weapon_EG47.prefab`); el reemplazo solo necesita `WeaponModel` con los mismos sockets | Pivote en la empuñadura, sockets: `Muzzle`, `ShellEject`, `Sight`, `Magazine`; LOD0 de 10 a 20k tris | 3 |
| Alta | Animaciones del EG-47 | Animación procedural (`WeaponViewModel`) | Reposo, caminar, correr, sprint, sprint táctico, disparo, recarga (con y sin balas en la recámara), desenfundar/guardar, inspección y deslizamiento | 9 |
| Alta | Stalker (modelo y animaciones) | Criatura provisional de unas 40 primitivas con animación procedural (`EG_Alien_Stalker.prefab`, `AlienBody`) | Rig genérico; animaciones de reposo, caminar, correr, ataque, reacción al impacto, muerte y "olfatear" (búsqueda); hitboxes de cabeza, torso y extremidades | 5 / 9 |
| Alta | Kit modular del laboratorio | Cubos de la escena de pruebas | Rejilla de 1 m, módulos de 4 m (paredes, suelo, techo, puerta corredera, terminal, marcos), materiales de metal desgastado | 6 |
| Media | VFX | — | Fogonazo, trazadoras, impactos (metal, hormigón, carne alienígena), casquillos, sangre alienígena | 3 / 9 |
| Media | Decals de impacto | — | Agujeros de bala para metal y hormigón (HDRP Decal Projector) | 3 |
| Media | Audio: armas | WAV sintetizados (`Wpn_EG47_Shot`, `Wpn_EG47_Reload`, `Wpn_DryFire`, `UI_Hitmarker`) | Disparo cercano y lejano, mecánica de recarga, gatillo sin munición, cola de reverberación en interiores | 9 |
| Media | Audio: jugador | — | Pasos por superficie (metal, hormigón, rejilla), respiración, aterrizaje, deslizamiento | 9 |
| Media | Audio: aliens y ambiente | WAV sintetizados (`Alien_Stalker_*`, ambiente) | Gruñidos y ataques del Stalker, alarma, zumbido del laboratorio, música de exploración y de combate | 9 |
| Baja | VX-9, BR-12, NS-50 y PLASMA-X | — | Mismos sockets que el EG-47 | 3+ |
| Baja | Ravager, Spitter, Phantom y Eclipse Prime | — | — | Campaña |

## Genesis_Research_Facility: qué es provisional hoy

Toda la geometría de la escena es **provisional**: cajas y cilindros con UVs en metros, generados por `FacilitySceneBuilder`. Los materiales son PBR de HDRP, pero sus texturas son **procedurales**: se generan por código en `Art/Generated/Textures` y no son escaneadas ni pintadas. El escenario **no es fotorrealista**.

| Prioridad | Asset | Sustituye a | Requisitos |
|---|---|---|---|
| Alta | Kit modular sci-fi (paredes, suelos, techos, marcos y puertas de seguridad de 2,2 × 2,6 m) | Cajas `Wall_*`, `Floor`, `Ceiling` y puertas de primitivas | Rejilla de 1 m; LODs; colisiones simples; mapas BaseColor/Normal/Mask de HDRP a 2K |
| Alta | Texturas PBR reales: metal pintado gastado, hormigón, chapa antideslizante, rejilla, franjas de peligro | `ProceduralTextures` (MetalPanel, Concrete, DiamondPlate, Grating, Hazard) | 2K, tileables, con Mask Map de HDRP (metal/AO/detalle/suavidad) |
| Alta | Luminarias de techo, balizas giratorias y focos industriales | Cajas emisivas `CeilingLight` y cilindros `Beacon` | Con perfil IES opcional para HDRP |
| Media | Tuberías, conductos, bandejas de cables y rejillas con codos y soportes | Cilindros `Pipe`, cajas `Duct` y `CableTray` | Kit con piezas rectas, codos de 90° y bridas |
| Media | Terminales, pantallas, racks de servidores, bancos de laboratorio, taquillas y soportes de armas | Cajas compuestas (`Terminal`, `ServerRack`, `LabBench`, `Locker`, `WeaponRack_Empty`) | Pantallas con UV propias para la emisión |
| Media | Tanques de especímenes con cristal real y especímenes dentro | Cilindros emisivos opacos `Tank` | Cristal con HDRP Lit transparente o refracción |
| Media | Contenedores, barreras de hormigón y cajas militares | Cajas `ShippingContainer`, `ConcreteBarrier`, `Crate` | Escala real (contenedor de 20 ft: 6,06 × 2,6 × 2,44 m) |
| Media | Biomasa alienígena orgánica | Esferas aplastadas `AlienBiomass` | Shader Graph con translucidez y emisión |
| Media | VFX de humo, vapor y chispas | `LocalVolumetricFog` con ruido 3D y partículas HDRP/Unlit | VFX Graph (paquete no instalado) |
| Media | Audio real: zumbido eléctrico, ventilación, puertas neumáticas, sirena y chispas | WAV sintetizados en `Audio/Generated` | 48 kHz, bucles sin cortes |
| Baja | Decals: suciedad, quemaduras, fluidos alienígenas, señalética y numeración de zonas | 3 decals procedurales | Atlas de decals con normal y máscara |

Las fuentes candidatas (Unity Asset Store gratuito, Mixamo para pruebas de rig, librerías CC0) se propondrán antes de importar nada.
