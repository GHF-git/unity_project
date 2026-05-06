# Unity Racing Game - v02

A realistic car racing game built with Unity featuring physics-based driving mechanics, lap timing, and interactive car assembly systems.

## 📋 Project Overview

This is an intermediate version of a multiplayer racing game currently in development. The project includes realistic car physics, garage mechanics, and a complete race UI system. A more feature-rich version with additional content is being developed by the project team.

## 🎮 Game Features

### Core Gameplay
- **Realistic Car Physics**: WheelCollider-based rear-wheel drive car controller with progressive torque build-up (accelerator feel)
- **Lap Timing System**: Automatic lap timing with start/finish line triggers
- **Race UI**: Real-time speedometer and race results display
- **Countdown Timer**: Pre-race countdown before GO! signal

### Car Systems
- **Engine Assembly & Animation**: Interactive engine assembly mechanics with smooth animations
- **Steering Assembly**: Steerable wheel mechanics with visual feedback
- **Braking System**: Dynamic brake lights and exhaust effects
- **Suspension & Handling**: Speed-dependent steering angle reduction for stability
- **Transmission**: Automatic forward/reverse with realistic directional control

### Visual Effects
- **Exhaust Smoke Effects**: Dynamic exhaust fume simulation
- **Brake Lights**: Responsive brake indicator lights
- **Alarm Lights**: Vehicle status indicators
- **Video Integration**: Race finish video playback

### Environmental Features
- **Garage System**: Interactive garage door and environment
- **Track Roads**: Pre-built racing tracks
- **Camera Follow**: Third-person dynamic camera system
- **UI Overlays**: HUD timer bar and race results screen

## 🛠️ Technical Stack

- **Engine**: Unity (with New Input System)
- **Physics**: PhysX with WheelColliders
- **UI Framework**: TextMesh Pro
- **Scripting Language**: C#
- **Asset Format**: FBX models, MP4 videos, PNG textures

## 📁 Project Structure

```
Assets/
├── Scenes/                    # Race and sample scenes
│   ├── race.unity            # Main racing scene
│   └── SampleScene.unity     # Demo scene
├── Scripts/                   # C# Game Scripts
│   ├── CarController.cs      # Core car physics and input handling
│   ├── LapTimer.cs           # Race timing system
│   ├── RaceResultsUI.cs      # End-of-race display
│   ├── SpeedometerUI.cs      # Speed display
│   ├── CarAssemblyManager.cs # Car assembly mechanics
│   ├── CameraFollow.cs       # Third-person camera
│   ├── CountdownTimer.cs     # Pre-race countdown
│   ├── ExhaustFume.cs        # Exhaust effects
│   ├── BrakeLights.cs        # Brake light indicators
│   └── [Other specialized controllers]
├── Materials/                 # Car and track materials
├── prefabs/                   # Reusable game objects
├── CartoonTracksPack1/        # Track assets
├── TrackRoad/                 # Road meshes and textures
├── data/                      # Game data and configurations
├── icons/                     # UI icons
└── Scenes/ & myRessources/   # Additional scene resources
```

## 🎮 Controls

### Vehicle Control
- **W** - Accelerate (Forward)
- **S** - Reverse / Brake
- **A/D** - Steer Left/Right
- **Space** - Handbrake (Full Stop)

### Camera
- Third-person follow camera with dynamic positioning

## 🚀 Getting Started

### Prerequisites
- Unity 2022.3+ (recommended)
- Git LFS (for large asset files)
- 10GB+ free disk space

### Installation

1. **Clone the repository:**
   ```bash
   git clone git@github.com:GHF-git/unity_project.git
   cd unity_project
   ```

2. **Install Git LFS:**
   ```bash
   git lfs install
   git lfs pull
   ```

3. **Open in Unity:**
   - Launch Unity Hub
   - Click "Add project from disk"
   - Select the `unity_project` folder
   - Wait for the project to initialize and assets to import

4. **Open the Main Scene:**
   - Navigate to `Assets/Scenes/race.unity`
   - Press Play to test

## 🎯 Game Mechanics Explained

### Car Physics
The car controller implements:
- **Rear-Wheel Drive**: Motor torque applied only to rear wheels
- **Progressive Acceleration**: Torque ramps up over configurable time for realistic feel
- **Dynamic Steering**: Steering angle reduces at high speed for stability
- **Engine Braking**: Light friction when coasting
- **Smart Braking**: Automatic direction-change braking

### Race Flow
1. **Pre-Race**: Countdown timer locks all input and applies full brakes
2. **Race Start**: "GO!" signal released when countdown reaches zero
3. **Lap Tracking**: First line cross counts, second cross finishes lap
4. **Finish**: Video plays, results screen shows with race time
5. **Results**: UI displays final lap time and status

## 📊 Performance Notes

- Optimized for PC and console platforms
- WheelCollider-based physics pipeline
- Real-time UI updates with minimal overhead
- Video playback integrated without separate rendering

## 🔧 Configuration

### Key Tunable Parameters (in CarController.cs)
- `maxMotorTorque`: Maximum engine torque (Nm)
- `maxSpeedKmh`: Speed limiter
- `torqueRampTime`: Acceleration feel
- `brakeTorque`: Braking force
- `maxSteeringAngle`: Steering sensitivity
- `steeringSpeedReduction`: High-speed steering reduction

### Audio/Video Assets
- `Smoke Green Screen.mp4`: Finish video effect
- Various FBX models for car parts and environment

## 🐛 Known Issues & Limitations

- This is an intermediate version (v02)
- More features and content being added in newer versions
- Some visual effects may require optimization for lower-end hardware

## 📈 Future Development

- Enhanced multiplayer networking
- Additional tracks and environments
- Advanced car customization
- More detailed vehicle dynamics
- Environmental interactions

## 🤝 License & Credits

**Current Status**: Private Development Version

Project developed by a collaborative team. This version is maintained separately while a more feature-complete version is in active development.

## 📞 Support

For issues, questions, or contributions, please contact the development team.

---

**Last Updated**: May 2026
**Engine Version**: Unity 2022.3+
**Platform Target**: PC/Console

