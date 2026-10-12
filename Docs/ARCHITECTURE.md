# Arquitectura

## Ensamblados
| Ensamblado | Contenido | Depende de |
|---|---|---|
| `EclipseGenesis.Runtime` | Todo el código de juego (`Assets/_Project/Scripts`) | Unity.InputSystem |
| `EclipseGenesis.Editor` | Generadores de escenas y herramientas de QA (solo Editor) | Runtime, Input System, RP Core, HDRP |
| `EclipseGenesis.Tests.EditMode` | Validación estática del proyecto y sus assets | Runtime, Editor |
| `EclipseGenesis.Tests.PlayMode` | Pruebas de comportamiento con entrada simulada | Runtime, Unity.InputSystem.TestFramework |

Las dependencias van en un solo sentido: Editor y Tests dependen de Runtime, nunca al revés.

## Jugador (implementado)

```
Player_FPS (prefab)
├─ CharacterController
├─ PlayerInputReader      (-100) única puerta al InputActionAsset → expone la intención (Move, Look, SprintPressed…)
├─ PlayerLook             (-80)  yaw → cuerpo · pitch → CameraRoot · AddRotation() también lo usará el retroceso
├─ PlayerMovement         (-50)  locomoción; lee MovementSettings (SO) y PlayerInputReader
├─ PlayerCameraEffects    (100, LateUpdate) altura de los ojos, balanceo, caída al aterrizar, inclinación lateral y FOV
├─ MovementDebugOverlay   panel temporal (F1)
└─ CameraRoot             (pitch, altura de los ojos)
   └─ CameraOffset        (balanceo, caída al aterrizar, inclinación lateral)
      └─ MainCamera       (Camera, HDAdditionalCameraData, AudioListener)
         └─ [Fase 3] WeaponHolder / brazos en primera persona
```

**Flujo de datos:** `InputActionAsset → PlayerInputReader → {PlayerLook, PlayerMovement} → eventos (Landed, Jumped, SlideStarted/Ended) → PlayerCameraEffects`.

### PlayerMovement en detalle
- La cápsula crece desde los pies (pivote en el suelo y `center = height/2`), así que agacharse no hace "caer" al jugador.
- **Posturas:** `Standing`, `Crouching` y `Sliding`. Para pasar a una postura más alta se comprueba antes el espacio libre con `OverlapCapsule`, ignorando el collider propio.
- **Estado observable** (`LocomotionState`): Idle, Walk, Run, Sprint, TacticalSprint, Crouch, Slide y Airborne. Lo usarán el Animator, el audio de pasos y la dispersión del arma.
- **Puntos de integración para fases futuras:** la propiedad `IsAiming` (la fija el arma; bloquea el sprint y obliga a caminar), `Teleport()` (respawn en checkpoint), `PlayerLook.AddRotation()` (retroceso), `PlayerCameraEffects.AimFovMultiplier` (ADS) y `PlayerLook.SensitivityMultiplier` (configuración).
- **Colisiones:** después de `Move` se descarta la velocidad que no se ha llegado a aplicar contra una pared, para que no se acumule velocidad.

## Sistemas planificados y dependencias previstas

```
GameManager ──► MissionManager ──► Objective[]          (SO de misión)
     │               └──► CheckpointManager ──► PlayerMovement.Teleport / PlayerHealth.Restore
     ├──► PauseManager ──► PlayerInputReader.SetGameplayEnabled · Time.timeScale · cursor
     └──► SettingsManager (JSON) ──► PlayerLook / PlayerCameraEffects / AudioMixer / QualitySettings

WeaponInventory ──► WeaponController ◄── WeaponData (SO)
                         ├─ WeaponRecoil ──► PlayerLook.AddRotation
                         ├─ HitscanShooter / ProjectileShooter ──► IDamageable
                         └─ eventos (Fired, Reloaded, AmmoChanged) ──► HUDController, AudioManager

IDamageable ◄── PlayerHealth (vida + escudo) · AlienHealth · Hitbox (multiplicador por zona)
AlienBrain (FSM) ──► AlienPerception (vista y oído) · NavMeshAgent · AlienAttack
HUDController ◄── solo escucha eventos; nunca consulta sistemas en Update
```

Reglas: la comunicación entre sistemas va por **eventos C#** o por interfaces (`IDamageable`, `IInteractable`). No hay singletons estáticos con estado de juego (eso facilita el reinicio de la misión y, más adelante, el multijugador). El HUD no tiene lógica de juego.

## Render
HDRP con tres niveles de calidad ya configurados por la plantilla: **High Fidelity**, **Balanced** y **Performant** (`Assets/Settings/HDRP *.asset`). El perfil global de cielo y niebla es `Assets/Settings/SkyandFogSettingsProfile.asset`. Los materiales provisionales usan HDRP/Lit y se crean por script.
