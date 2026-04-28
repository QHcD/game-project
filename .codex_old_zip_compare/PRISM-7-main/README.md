# PRISM-7: Weapon Trials

Unity game project for `IT8101 - Games Development`.

## Project Overview

`PRISM-7: Weapon Trials` is a 3D combat-focused game prototype built in Unity.  
The player enters a closed arena and fights through weapon-based survival trials against multiple AI-controlled opponents. The current project focuses on melee combat, menu flow, difficulty settings, camera options, and an arena-based progression structure.

The project was developed to meet the core requirements of the IT8101 group game assessment, including:

- a main menu
- settings and options menus
- credits screen
- playable combat gameplay
- an animated player/enemy character
- custom camera support
- scoring and reward feedback
- multiple scenes and organized assets

## Game Concept

The game places the player inside a trial arena where survival depends on movement, positioning, and melee combat timing. The player can fight in either first-person or third-person view and can change gameplay preferences such as difficulty and movement style.

The current version is centered around:

- arena combat
- melee weapon gameplay
- AI enemies in a free-for-all combat space
- score tracking
- timer-based match flow
- win/lose state transitions

## Core Features

- `Main Menu` with `Continue`, `Select Level`, `Options`, `Settings`, `Credits`, and `Quit`
- `Options` screen for:
  - difficulty selection
  - camera view selection
  - movement style selection
- `Settings` screen for:
  - resolution
  - graphics quality
  - fullscreen
  - audio controls
- `Pause Menu` during gameplay
- `Game Over / Mission Failed` flow
- `Level Complete` flow
- first-person melee combat
- third-person gameplay support
- enemy AI combat behavior
- HUD with score, level, weapon, enemies remaining, timer, health, and minimap
- arena/theme selection support

## Gameplay Systems

### Player

- melee attack system
- health system
- first-person and third-person camera modes
- configurable movement scheme:
  - `WASD + Mouse`
  - `Arrow Keys + Mouse`

### Enemies

- AI-controlled melee enemies
- dynamic target selection
- free-for-all combat behavior
- shared character presentation for consistency with the player’s third-person mode

### Match Flow

- combat begins in `GameScene`
- the player can win by eliminating all enemies
- the player loses if health reaches zero
- timed match state is tracked through the HUD and game manager

## Current Scenes

- `MainMenu`
- `GameScene`
- `Options`
- `Settings`
- `Credits`

## Technologies Used

- `Unity 6 / Unity 6.3 LTS`
- `C#`
- `Unity Input System`
- `TextMeshPro`
- `Universal Render Pipeline (URP)`
- runtime-generated UI systems
- imported third-person character assets
- imported first-person melee assets

## Asset and Content Sources

This project currently includes integrated content from local imported Unity packages and tutorial-based starter assets used for prototyping and experimentation, including:

- first-person melee controller/assets
- imported Mixamo-style third-person knight character assets
- UI and menu systems built inside the project

If you submit this project academically, you should list the final external assets/tutorial references used by your team in this section.

## Project Structure

Important folders in this project include:

- `Assets/Scenes`
- `Assets/Scripts`
- `Assets/FirstPersonMelee`
- `Assets/Resources`
- `Assets/Fonts`
- `Assets/Audio`
- `Assets/Animations`
- `Assets/Models`
- `Assets/Materials`

## Main Scripts

- `GameManager.cs`
  - handles overall game state, score, level flow, map selection, difficulty, and gameplay settings
- `RuntimeMenuBuilder.cs`
  - builds the main menu and results screens at runtime
- `OptionsBuilder.cs`
  - creates the options interface
- `SettingsBuilder.cs`
  - creates the settings interface
- `LevelBuilder.cs`
  - builds the arena, spawns enemies, sets managers, and controls runtime gameplay setup
- `HUDManager.cs`
  - handles the gameplay HUD and minimap
- `PlayerHealth.cs`
  - controls player health and damage handling
- `PlayerController.cs`
  - handles player movement, camera behavior, and melee attack logic

## Controls

Default gameplay controls:

- `Move`: `WASD` or `Arrow Keys` depending on selected option
- `Look`: `Mouse`
- `Attack`: `Left Mouse Button`
- `Jump`: configured through the current player input setup
- `Pause`: `Escape`

## How To Run

1. Open the project in Unity.
2. Load the `MainMenu` scene.
3. Press `Play`.
4. Use `Continue` to enter the gameplay scene.

## Team Members

Add your final team details here before submission.

- `Member 1` - Role / contribution
- `Member 2` - Role / contribution
- `Member 3` - Role / contribution
- `Member 4` - Role / contribution
- `Member 5` - Role / contribution

## Development Approach

This project is intended to align with the IT8101 brief by combining:

- game design planning
- iterative development
- SCRUM-based task breakdown
- weekly progress tracking
- gameplay implementation in Unity

Recommended documentation to include alongside this repository:

- product backlog
- game design document
- sprint logs / SCRUM logbook
- final build submission

## Notes For Submission

Before final academic submission, update this README with:

- final team member names
- confirmed tutor-approved custom features
- final list of references/assets used
- gameplay screenshots
- build instructions if they change
- known limitations or future improvements

## Version

- `Product Name`: `PRISM-7`
- `Version`: `0.1.0`
- `Company Name`: `DefaultCompany`

