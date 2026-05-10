# 🚗 Car Assembly & Racing — Unity Project

> An interactive 3D educational game built with Unity 6 (6000.3.5f1) by a team of 6.
> Players assemble a real car step-by-step in a garage, then take it to the race track.

---

## 📖 Project Overview

This project is a **Unity course team project** combining automotive education with gaming.
The experience is split into two connected scenes:

| Scene | Description |
|-------|-------------|
| **MainMenu** | Animated intro with a rotating race track model; two entry buttons |
| **SampleScene** (Garage) | Interactive car assembly with guided narration and snap mechanics |
| **race** | Full driving experience on a cartoon race track with lap timer and HUD |

The player's journey:
1. Open the **main menu** → watch the track zoom-in intro
2. Enter the **Garage** → assemble the engine, steering system, and differential step-by-step
3. Open the garage door and drive the assembled car on the **Race Track**

---

## 🎮 Gameplay — Garage Assembly Mode

The garage scene (`SampleScene.unity`) is the educational core of the project.
Assembly is divided into **four sequential modules**, each narrated with ElevenLabs AI voice-overs:

### 1. Engine Assembly (`MoteurAssemblyManager`)
Players drag-and-drop engine parts (base, crankshaft, axles, pistons, connecting rods, driven gear)
onto snap zones. Each correctly placed part triggers a narration step explaining how that component
works. A visual encouragement panel rewards progress.

### 2. Steering System Assembly (`SteeringAssemblyController` / `SteeringSnapSetup`)
After the engine is complete, the steering module activates. Players assemble:
- Steering wheel → Column → Input/Intermediate/Upper/Lower gears → Output gear → Rack → Tie-rod links → Knuckles
Gear rotation animations (`SteeringSystemGearRotation`) show how the steering rack actually works.

### 3. Differential Assembly (`DifferentialAssemblyManager` / `DifferentialSnapSetup`)
Inserted after steering. Players assemble the ring gear, spider gears, and differential carrier.
A demo animation plays to illustrate how differential gears split torque between wheels.

### 4. Car Body Assembly (`CarAssemblyManager`)
The final phase: attaching wheels, doors, hood, bumpers, headlights, spoiler, and roof panels.
A drag-and-drop inventory system (`InventoryManager`, `DraggableInventoryItem`) lets players
pick parts from an icon tray and drop them onto the correct positions on the car.

Once all parts are assembled:
- The **garage door button pulses red** as a hint
- The player clicks the physical 3D button → `GarageDoorController` opens the door
- The car drives out to the race scene

---

## 🏎️ Gameplay — Race Mode

The race scene (`race.unity`) features:

| Feature | Script |
|---------|--------|
| Realistic rear-wheel-drive physics | `CarController.cs` |
| Animated speedometer HUD | `SpeedometerUI.cs` |
| Countdown timer (3-2-1-GO!) | `CountdownTimer.cs` |
| Lap detection and timing | `LapTimer.cs` + `StartLineTrigger.cs` |
| Race results UI | `RaceResultsUI.cs` |
| Camera follow with switcher | `CameraFollow.cs` + `CameraSwitcher.cs` |
| Exhaust smoke particle effect | `ExhaustSmoke.cs` + `ExhaustSmokeController.cs` |
| Brake lights | `BrakeLights.cs` |
| Background music (persistent) | `MusicManager.cs` |
| Alarm light on garage | `AlarmLightController.cs` |

---

## 🏗️ Project Structure

```
unity_project/
├── Assets/
│   ├── Scenes/
│   │   ├── MainMenu.unity          # Main menu scene
│   │   ├── SampleScene.unity       # Garage / assembly scene
│   │   └── race.unity              # Race track scene
│   ├── Scripts/                    # All C# MonoBehaviours (see below)
│   ├── script/                     # Inventory & drag-drop scripts
│   ├── NarrationSteps/             # ElevenLabs MP3 narration audio + ScriptableObject steps
│   │   └── Steering/               # Steering-specific narration
│   ├── Animations/                 # Animator controllers
│   ├── prefabs/                    # Car part prefabs
│   ├── data/inventoryItems/        # ScriptableObject inventory item definitions
│   ├── materials/                  # URP materials for car, garage, effects
│   │   └── Car/                    # Car-specific PBR materials
│   ├── icons/                      # Inventory slot icons (PNG)
│   ├── sounds/                     # Music and SFX (MP3)
│   ├── myRessources/               # Garage FBX, button FBX, textures, GarageDoor
│   ├── CartoonTracksPack1/         # Race track asset pack (meshes, materials, textures)
│   ├── TrackRoad/                  # CartoonRaceTrackOval FBX
│   ├── TextMesh Pro/               # TMP package assets
│   ├── Settings/                   # URP render pipeline assets (PC + Mobile)
│   ├── car.fbx / car co.fbx        # Main car 3D model
│   ├── cranckshaft.fbx             # Crankshaft sub-model
│   ├── pist.fbx                    # Piston sub-model
│   └── fix00.fbx                   # Engine base sub-model
├── Packages/
│   ├── manifest.json               # Unity package dependencies
│   └── com.bezi.sidekick/          # Bezi editor plugin
├── ProjectSettings/                # Unity project configuration
└── .gitattributes                  # Git LFS tracking rules
```

---

## 📜 Scripts Reference

### Assembly & Progression
| Script | Purpose |
|--------|---------|
| `MoteurAssemblyManager.cs` | Engine assembly phase controller |
| `MoteurSnapSetup.cs` | Snap zones and completion detection for engine |
| `CarAssemblyManager.cs` | Car body assembly + garage door transition |
| `AssemblyOrderManager.cs` | Enforces correct part placement order |
| `AssemblyOrderValidator.cs` | Validates snap order rules |
| `SteeringAssemblyController.cs` | Steering module gating + IsSteeringComplete flag |
| `SteeringSnapSetup.cs` | Snap zones for all steering parts |
| `DifferentialAssemblyManager.cs` | Differential phase controller |
| `DifferentialSnapSetup.cs` | Snap zones for differential parts |

### Narration & Feedback
| Script | Purpose |
|--------|---------|
| `EngineNarrationController.cs` | Sequences engine narration audio clips |
| `EngineNarrationStep.cs` | ScriptableObject: one narration step |
| `SteeringNarrationController.cs` | Sequences steering narration |
| `DifferentialNarrationController.cs` | Sequences differential narration |
| `EngineEncouragementPanel.cs` | Shows praise UI when parts are placed correctly |
| `EncouragementChime.cs` | Audio feedback for correct placement |
| `EncouragementFlash.cs` | Visual flash feedback for correct placement |
| `InstructionCardController.cs` | Shows/hides step instruction cards |
| `InstructionCardContent.cs` | Data for one instruction card |

### Inventory & Drag-Drop
| Script | Purpose |
|--------|---------|
| `InventoryManager.cs` | Manages inventory slots and item state |
| `InventorySystem.cs` | Core inventory data system |
| `InventoryItemData.cs` | ScriptableObject: inventory item definition |
| `InventorySlotVisuals.cs` | Updates slot icon/highlight visuals |
| `DraggableInventoryItem.cs` | Drag-and-drop logic for inventory items |
| `DragPreview.cs` | Floating ghost preview while dragging |
| `PartGrabber.cs` | Raycasts and picks up 3D parts from the scene |
| `SnapToPlace.cs` | Snaps a dragged part to the nearest valid zone |
| `SnapZoneAssignment.cs` | Assigns which part belongs to which snap zone |
| `SnapSuccessEffect.cs` | Particle/audio effect on successful snap |
| `AssemblyProgressUI.cs` | Progress bar showing how many parts are placed |
| `InventoryTooltip.cs` | Shows part name tooltip on hover |
| `InventoryAudioManager.cs` | Plays audio on inventory events |
| `WheelInventorySlot.cs` | Specialized slot for wheel placement |
| `WheelPlacementTracker.cs` | Tracks wheel snap completion |
| `WheelDragExtension.cs` | Extends drag behaviour for wheel parts |
| `WheelGhostPreview.cs` | Ghost preview for wheel placement |

### Race & Car Physics
| Script | Purpose |
|--------|---------|
| `CarController.cs` | Rear-wheel-drive WheelCollider physics |
| `CameraFollow.cs` | Smooth camera that follows the car |
| `CameraSwitcher.cs` | Switches between camera views |
| `CountdownTimer.cs` | 3-2-1-GO race start countdown |
| `LapTimer.cs` | Records lap times |
| `StartLineTrigger.cs` | Detects when car crosses start/finish |
| `RaceResultsUI.cs` | End-of-race summary panel |
| `SpeedometerUI.cs` | Animated needle speedometer HUD |
| `BrakeLights.cs` | Activates tail-light emission on braking |
| `ExhaustSmoke.cs` | Particle system exhaust effect |
| `ExhaustSmokeController.cs` | Controls smoke intensity by speed |
| `ExhaustFume.cs` / `ExhaustFumeDriver.cs` | Low-speed exhaust fumes |

### Environment & UI
| Script | Purpose |
|--------|---------|
| `MainMenuController.cs` | Intro animation + scene loading buttons |
| `MusicManager.cs` | Persistent cross-scene background music |
| `GarageDoorController.cs` | Opens/closes the animated garage door |
| `GarageDoorInstructionUI.cs` | UI hint for interacting with garage door button |
| `DoorButtonInteraction.cs` | Raycasts onto the 3D button to trigger door |
| `AlarmLightController.cs` | Flashing alarm light on the garage wall |
| `CockpitInteriorSetup.cs` | Configures cockpit camera interior view |
| `ConfettiConfigurator.cs` | Confetti particle system when assembly completes |
| `ReadyForTheRoadButton.cs` | Final CTA button after full assembly |
| `PrecedentButton.cs` | "Back" navigation button in assembly flow |
| `DifferentialPrecedentButton.cs` | "Back" button for differential phase |
| `SteeringWheelRotation.cs` | Animated steering wheel in cockpit |
| `SteeringWheelPivot.cs` | Pivot helper for steering wheel animation |
| `SteeringSystemGearRotation.cs` | Animates gears in the steering demo |
| `SteeringAnimationController.cs` | Drives the steering assembly animations |
| `MoteurAnimationController.cs` | Drives the engine assembly animations |
| `DifferentialDemoController.cs` | Demo spin animation for differential |

### Editor Tools
| Script | Purpose |
|--------|---------|
| `Scripts/Editor/PivotRecenterTool.cs` | Editor tool to re-center object pivots |

---

## ⚙️ Technical Details

| Property | Value |
|----------|-------|
| **Unity Version** | 6000.3.5f1 (Unity 6) |
| **Render Pipeline** | Universal Render Pipeline (URP) |
| **Target Platforms** | PC (primary), Mobile (URP profile included) |
| **Input System** | Legacy Input Manager + new Input System (InputSystem_Actions) |
| **Physics** | Unity PhysX — WheelColliders for car physics |
| **Audio** | Unity AudioSource — ElevenLabs AI voice narration (MP3) |
| **Text** | TextMesh Pro |
| **Git LFS** | All binary assets tracked (FBX, PNG, PSD, MP3, MP4, TGA, etc.) |

---

## 🔧 Setup & Getting Started

### Prerequisites
- Unity **6000.3.5f1** (download from Unity Hub)
- Git with **Git LFS** installed (`git lfs install`)

### Clone & Open

```bash
# Make sure Git LFS is initialized
git lfs install

# Clone the repository
git clone git@github.com:GHF-git/unity_project.git

# Open in Unity Hub → Add project from disk → select the cloned folder
```

> ⚠️ **Important:** Always pull with LFS enabled. Large binary assets (FBX models, textures,
> audio, video) are stored in Git LFS. Without it, you'll get LFS pointer files instead of
> real assets.

### First Time in the Editor
1. Open `Assets/Scenes/MainMenu.unity` as the starting scene
2. Unity will reimport assets — this may take a few minutes on first open
3. Press **Play** to test the main menu; use the buttons to load other scenes

### Running the Full Experience
- Set scene build order in **File → Build Settings**:
  1. `Assets/Scenes/MainMenu.unity`
  2. `Assets/Scenes/SampleScene.unity`
  3. `Assets/Scenes/race.unity`

---

## 🎵 Assets & Attributions

| Asset | Source |
|-------|--------|
| Narration voice-overs | ElevenLabs AI (Michael voice) — April–May 2026 |
| Race track music | *SURV1V3* — Daiki Kasho (Gran Turismo 7 Soundtrack) |
| Cartoon Tracks Pack 1 | Unity Asset Store |
| Garage FBX | Third-party (see `myRessources/`) |
| Car FBX | Custom-built (`car.fbx`, `car co.fbx`, `car_final.fbx`) |
| Speedometer UI | Unity-Speedometer-master (open source) |
| Alarm with Lamp FBX | Third-party (`myRessources/AlarmWithLamp`) |

---

## 👥 Team

This project was developed by a team of **6 members** as part of a Unity development course.

| Role | Contribution |
|------|-------------|
| Engine Assembly module | Snap system, narration integration, encouragement UI |
| Steering Assembly module | Gear animation, steering snap, narration |
| Differential Assembly module | Demo animation, snap zones, narration |
| Car Body Assembly / Inventory | Drag-drop system, slot visuals, part prefabs |
| Race Scene | Car physics, lap timer, speedometer, camera |
| Scene Management / Main Menu | Menu animation, scene flow, music manager, garage door |

---

## 🐛 Known Issues / Notes

- The `_Recovery/` folder contains Unity auto-generated crash-recovery scenes — it is excluded from git
- Multiple `.sln` files exist from different Visual Studio workspace setups — these are also excluded
- The `UserSettings/` folder is local editor state and excluded from version control
- `Assets/0319.mp4` — a green-screen smoke video used for the exhaust chroma-key shader

---

## 📄 License

This project is for educational purposes. Third-party assets retain their original licenses.
