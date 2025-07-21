# Botball Dataset Generator

This "simulator" uses the Unity engine with the Perception package to create synthetic botball datasets.

## Creating own table assets

- Import gametable model (Has to be .fbx to import materials, etc. .glb isnt supported by unity right now)
- Drag GameTable into scene (replace old one)
    - Make sure table scale is alright. The scale if alright if it approximately is one meter. Default cube can be used as reference (1x1x1 meter). If not, needs to be scaled in the inspector when clicking on the .fbx model
- In scene view, click on the game pieces you want to convert into detectable objects
- First, unpack the gametable object to allow modifying it in unity
- Then drag the correct object (Not a Part, but the actual highest element named like the wanted game piece) into the prefab folder to create a template
- Once you have a template from a game piece, you can use the right click menu to reconnect any game piece that is the same to this prefab.
- To the template, you have to add two components:
    - Labeling
        - You need to add a label to the game piece to make it recognized as a class
        - Click "Add new label"
        - Enter the class name
        - Click Add to Label config
        - Choose the GameTableIdLabelConfig and click add label
        - Done
    - GamePieceTag
- The components will be applied to all child objects that inherit this template
- Repeat this until all game pieces are marked

## Adapting the region sampler

To make game piece randomization work properly, the bounds have to be adapted.
Search the "PolyRegionSampler". There should be a component named poly region sampler.

In an infobox, it explains the controls. Adjust the region to fit the new gametable area where samples should be allowed. Make sure gizmos are enabled, else the control points are invisible (One can move them in the scene view).

It helps switching the camera to isometric to get a good top-down view of the game table (Top right, by clicking on the navigation cube)

