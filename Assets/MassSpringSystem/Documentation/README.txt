Mass Spring System
Sean soraghan 
2017
---------------------------------------------
Overview
---------------------------------------------
This package includes a compute shader and accompanying script that implement a mass-spring model. There is also functionality to transfer touch and mouse input events to pressure that is applied to the mass-spring grid. The code is open source and available under the MIT license (see License/license.txt).

---------------------------------------------
Requirements
---------------------------------------------
Compute shaders in Unity closely match DirectX 11 DirectCompute technology. Platforms where compute shaders work:

Windows and Windows Store, with a DirectX 11 graphics API and Shader Model 5.0 GPU.
Modern OpenGL platforms (OpenGL 4.3 on Linux or Windows; OpenGL ES 3.1 on Android). Note that Mac OS X does not support OpenGL 4.3, so no compute shaders there yet.
Modern consoles (Sony PS4 and Microsoft Xbox One).

---------------------------------------------
Setup
---------------------------------------------
The Demo scene shows an instantiation of the mass-spring model in the game world. It can be recreated using the following steps:

1. The MassSpringSystem script (Assets/Scripts/SpringSystem/MassSpringSystem.cs) should be attached to the main camera. 
2. A Canvas object should be created and given the CanvasTouchManager (Assets/Scripts/UI/CanvasTouchManager.cs) script. 
3. An empty game object should be created and given the MassSpawner (Assets/Scripts/SpringSystem/MassSpawner.cs) scipt.
4. In the Main Camera MassSpringSystem component, the Spawner member variable should be set as the MassSpawner Game Object, and the UI Touch Handler member variable should be set as the Canvas object.
5. The Mass Spawner script has a Mass Prefab member variable that needs to be set. A game object should therefore be instantiated somewhere in the scene and set as the Mass Prefab for the Mass Spawner. In the Demo scene, the MassCube prefab is used.

---------------------------------------------
Usage
---------------------------------------------
The main parameters that can be used to tweak the mass spring grid are accessible from the Mass Spring System component in the editor. They are as follows:

Mass
----
The mass of individual mass points in the mass spring model. Increasing this will make the mass points more resistive to the springs in the model, but will also reduce their velocity

Damping
-------
The level of damping in the system. Increasing this value will cause the system to return to a more 'stable' state quicker, and will reduce the propagation of forces throughout the grid.

Stiffness
--------- 
The stiffness of the spings in the grid. Increasing this will cause mass points to 'rebound' with higher velocity, and will also decrease the time taken for the system to return to a 'stable' state.

Spring Length
-------------
The lenght of the springs in the grid. This defines how far each mass unit is at a resting state.