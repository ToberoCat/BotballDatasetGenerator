# Botball Dataset Generator

The Botball Dataset Generator is a Unity-based simulator that uses the Unity Perception package to create synthetic datasets for Botball game pieces. It provides two configurable scenes for generating annotated image data suitable for training object detection models.

## Overview

This tool simulates scenes with Botball game pieces to generate synthetic images and corresponding annotations. It supports:

- Controlled object placement and labeling in a game table scene.
- Randomized object arrangement with varied lighting and backgrounds in a synthetic scene.

## Scenes

### GameTable Scene

This scene simulates the actual Botball game table with controlled placement of game pieces.

#### Steps to Configure:

1. Import Game Table Asset:

   - Use `.fbx` format (Unity doesnt support .glb).
   - Drag the model into the scene and replace the existing game table.
   - Ensure correct scale.
   - Adjust scale in the Inspector if needed.

2. Convert Game Pieces to Prefabs:

   - Unpack the imported game table object.
   - Identify and drag the highest-level game piece object into the `Prefabs` folder. This has to be done for each game piece individually.
   - Right-click to reconnect similar pieces to the new prefab.

3. Add Required Components to Each Prefab:

   - Labeling:

     - Add a new label for each game piece class.
     - Click "Add to Label Config".
     - Choose `GameTableIdLabelConfig`.

   - GamePieceTag: Required for internal tagging.

   > All child objects of a prefab will inherit the added components.

4. Configure Sampling Bounds:

   * Locate the `PolyRegionSampler` component in the scene.
   * Adjust the sampling region bounds to fit the new table area.
   * Enable Gizmos for control point visibility.
   * Switch to isometric view for easier top-down editing (top-right navigation cube).

### Synthetic Scene

This scene generates images with random background clutter, lighting, and game piece orientations.

#### Steps to Configure:

1. Ensure game piece prefabs are already created from the GameTable Scene.

2. Open the `Simulation Scenario` GameObject.

3. In the Foreground Object Randomizer, add the desired game piece prefabs.

   > Objects will be randomly placed in the scene during simulation.

## Dataset Generation

1. Press the Play button in Unity to start the simulation.

2. To view the output location:

   * Select the Main Camera.
   * Locate the Perception Camera component in the Inspector.
   * Use the button at the bottom to open the output folder.

---

## Dataset Conversion (Unity Solo -> YOLO Format)

A conversion script is included to convert the Unity Solo dataset to YOLO format.

### Usage:

```bash
python3 unity_solo_to_yolo.py --input ~/.config/unity3d/DefaultCompany/BotballDatasetGenerator/solo --output yolo_ds --train 0.8 --val 0.1 --test 0.1
```

- `--input`: Path to the Unity Solo dataset.
- `--output`: Target directory for the YOLO-formatted dataset.
- `--train`, `--val`, `--test`: Split ratios for training, validation, and testing datasets.

## Requirements

* Unity (with Perception package)
* Python 3.x (for dataset conversion)
