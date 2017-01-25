/**
 * Copyright 2017 Sean Soraghan
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
 * documentation files (the "Software"), to deal in the Software without restriction, including without 
 * limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of 
 * the Software, and to permit persons to whom the Software is furnished to do so, subject to the following 
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using UnityEngine;
using System.Collections;

//===========================================================================================
// Simple class that holds various property names and attributes for the 
// MassSpringSystem.compute shader.
//===========================================================================================

public static class SpringComputeShaderProperties
{
    public const string PositionBufferName       = "posBuffer";
    public const string VelocityBufferName       = "velBuffer";
    public const string ExternalForcesBufferName = "externalForcesBuffer";
    public const string NeighboursBufferName     = "neighboursBuffer";
    public const string PropertiesBufferName     = "propertiesBuffer";
    public const string DeltaTimeBufferName      = "deltaTimeBuffer";
    public const int    NumProperties            = 4;
    public const string PosKernel                = "CSMainPos";
    public const string VelKernel                = "CSMainVel";
}

//===========================================================================================
// Simple class that holds various property names and attributes for the 
// SpringRenderShader.shader.
//===========================================================================================

public static class MassSpringRenderShaderProperties
{
    public const string PositionsBuffer = "buf_Points";
    public const string DebugBuffer     = "buf_Debug";
    public const string VelocityBuffer  = "buf_Vels";
}

//===========================================================================================
// Summary
//===========================================================================================
/**
 * This class is used to periodically run position and velocity buffers through compute
 * shader kernels that update them according to a mass spring model. It also provides access
 * to properties of the model that can be tweaked from the editor. This class is used to 
 * update these property values in the compute shader as they are changed externally.
 * 
 * The MassSpawner Spawner member variable is used to spawn and update game objects according
 * to the positions on the mass spring grid. Alternatively, points can be rendered using
 * the RenderShader Shader member variable. In order to do this, uncomment the OnPostRender
 * function and comment the Update function and the call to SpawnPrimitives in the 
 * Initialise function.
 * 
 * Various ComputeBuffer variables are used to read and write data to and from the compute 
 * shader (MassSpringComputeShader).
 * 
 * This class can also be used to translate touch and mouse input (from the UITouchHandler) 
 * into forces that are applied to the mass spring system (implemented by the 
 * MassSpringComputeShader). 
 */

public class MassSpringSystem : MonoBehaviour
{
    /** The compute shader that implements the mass spring model.
     */
    public ComputeShader MassSpringComputeShader;
    
    /** A Shader that can be used to render the mass points directly
     *  rather than using game objects in the game world.
     */
    public Shader        RenderShader;
    
    /** The mass of individual mass points in the mass spring model.
     *  Increasing this will make the mass points more resistive to
     *  the springs in the model, but will also reduce their velocity.
     */
    [Range(1.0f, 100.0f)] public float Mass            = 1.0f;  
   
    /** The level of damping in the system. Increasing this value
     *  will cause the system to return to a more 'stable' state quicker,
     *  and will reduce the propagation of forces throughout the grid.
     */ 
    [Range(0.1f, 0.999f)] public float Damping         = 0.1f;
    
    /** The stiffness of the spings in the grid. Increasing this will
     *  cause mass points to 'rebound' with higher velocity, and will
     *  also decrease the time taken for the system to return to a
     *  'stable' state.
     */
    [Range(0.1f, 100.0f)] public float SpringStiffness = 10.0f;

    /** The lenght of the springs in the grid. This defines how far
     *  each mass unit is at a resting state.
     */
    [Range(0.1f, 10.0f)]  public float SpringLength    = 1.0f;

    /** The controller of the game object spawner object.
     */
    public MassSpawner Spawner;

    /** The controller of the touch and mouse input handler object.
     */
    public CanvasTouchManager UITouchHandler;

    /** This is the force that will be applied from touch and mouse 
     *  input events on the grid.
     */
    [Range(0.0f, 1000.0f)] public float MaxTouchForce = 100.0f;

    /** Various ComputeBuffer variables are used to read and write data to and from the compute 
     *  shader (MassSpringComputeShader). 
     */
    private ComputeBuffer debugBuffer;
    private ComputeBuffer propertiesBuffer;
    // We fill a buffer of grid neigbour positions and send it to the compute buffer on intialisation, such that 
    // we have access to neughbouring positions in our compute kernels. The neighbours buffer is a buffer of Vector2
    // elements, where the x of each element is the neighbour position and the y is whether that position exists within
    // the bounds of the grid.
    private ComputeBuffer neighboursBuffer; 
    private ComputeBuffer deltaTimeBuffer;
    private ComputeBuffer positionBuffer;
    private ComputeBuffer velocityBuffer;
    private ComputeBuffer externalForcesBuffer;

    /** Our compute shader runs the same kernels in parallel on mutliple blocks of our
     *  mass spring grid. These blocks are of dimensions gridUnitSideX by gridUnitSideY,
     *  and there are numThreadsPerGroupX blocks along the x dimension of our grid and
     *  numThreadsPerGroupY along the Y dimension.
     *  
     *  These values MUST be identical to the gX and gY values in the MassSpringCompute compute shader.
     */
    private const int gridUnitSideX       = 15;
    private const int gridUnitSideY       = 7;
    private const int numThreadsPerGroupX = 4;
    private const int numThreadsPerGroupY = 4;

    /** The resolution of our entire grid, according to the resolution and layout of the individual
     *  blocks processed in parallel by the compute shader.
     */
    private int GridResX;
    private int GridResY;

    /** The total number of mass points (vertices) in the grid.
     */ 
    private int VertCount;

    /** The two kernels in the compute shader for updating the positions and velocities, respectively. 
     */
    private int PosKernel;
    private int VelKernel;

    /** This material can used to render the mass points directly (rather than using game objects).
     *  This material is instantiated using the RenderShader shader.
     */
    private Material RenderMaterial;

    //===========================================================================================
    // Overrides
    //===========================================================================================
    private void Start ()
    {
        Initialise();
    }

    void Update ()
    {
        HandleTouches();
        Dispatch();
        UpdatePrimitivePositions();
    }

    /* This function can be used for graphical debugging puproses,
     * or if you simply want to render the mass points as points rather
     * than maintaining game objects.
    */
    /*
    void OnPostRender ()
    {
        Dispatch ();
        RenderDataPoints ();
    }
   */

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    //===========================================================================================
    // Accessors
    //===========================================================================================
    
    /** Checks if an object is recognised as a spring mass model game object. 
     */
    public static bool IsMassUnit (string objectTag) { return objectTag == "MassUnit"; }

    /** Get the values of the mass positions from the compute buffer.
     */
    public Vector3[] GetPositions()
    {
        Vector3[] positions = new Vector3[VertCount];
        positionBuffer.GetData (positions);
        return positions;
    }

    /** Helper functions to get grid dimension properties in the world space.
     */
    public float GetWorldGridSideLengthX()
    {
        return GridResX * SpringLength;
    }

    public float GetWorldGridSideLengthY()
    {
        return GridResY * SpringLength;
    }

    //===========================================================================================
    // Construction / Destruction
    //===========================================================================================

    /** Initialise all of the compute buffers that will be used to update and read from the compute shader.
     *  Fill all of these buffers with data in order to construct a resting spring mass grid of the correct dimensions.
     *  Initialise the position and velocity kernel values using their name values from the SpringComputeShaderProperties static class.
     */
    public void CreateBuffers()
    {
        positionBuffer       = new ComputeBuffer (VertCount, sizeof (float) * 3);
        velocityBuffer       = new ComputeBuffer (VertCount, sizeof (float) * 3);
        externalForcesBuffer = new ComputeBuffer (VertCount, sizeof (float) * 3);
        debugBuffer          = new ComputeBuffer (VertCount, sizeof (float) * 3);
        neighboursBuffer     = new ComputeBuffer (VertCount, sizeof (float) * 24); //12 float pairs
        propertiesBuffer     = new ComputeBuffer (SpringComputeShaderProperties.NumProperties, sizeof (float));
        deltaTimeBuffer      = new ComputeBuffer (1, sizeof (float));
        
        ResetBuffers();

        PosKernel = MassSpringComputeShader.FindKernel (SpringComputeShaderProperties.PosKernel);
        VelKernel = MassSpringComputeShader.FindKernel (SpringComputeShaderProperties.VelKernel);
    }

    /** Fills all of the compute buffers with starting data to construct a resting spring mass grid of the correct dimensions
     *  according to GridResX and GridResY. For each vertex position we also calculate the positions of each of the neighbouring 
     *  vertices so that we can send this to the compute shader.
     */
    public void ResetBuffers()
    {
        Vector3[] positions  = new Vector3[VertCount];
        Vector3[] velocities = new Vector3[VertCount];
        Vector3[] extForces  = new Vector3[VertCount];
        Vector2[] neighbours = new Vector2[VertCount * 12];

        int neighboursArrayIndex = 0;
        for (int i = 0; i < VertCount; i++)
        {
            float x = ((i % GridResX - GridResX / 2.0f) / GridResX) * GetWorldGridSideLengthX();
            float y = ((i / GridResX - GridResY / 2.0f) / GridResY) * GetWorldGridSideLengthY();

            positions[i]  = new Vector3 (x, y, 0.0f);
            velocities[i] = new Vector3 (0.0f, 0.0f, 0.0f);
            extForces[i]  = new Vector3 (0.0f, 0.0f, 0.0f);
            
            Vector2[] neighbourIndexFlagPairs = GetNeighbourIndexFlagPairs (i);
            for (int n = 0; n < 12; ++n)
            {
                neighbours[neighboursArrayIndex] = neighbourIndexFlagPairs[n];
                neighboursArrayIndex ++;
            }
        }

        positionBuffer.SetData       (positions);
        velocityBuffer.SetData       (velocities);
        debugBuffer.SetData          (positions);
        externalForcesBuffer.SetData (extForces);
        neighboursBuffer.SetData     (neighbours);
    }

    public void ReleaseBuffers()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
        if (debugBuffer != null)
            debugBuffer.Release();
        if (propertiesBuffer != null)
            propertiesBuffer.Release();
        if (deltaTimeBuffer != null)
            deltaTimeBuffer.Release();
        if (externalForcesBuffer != null)
            externalForcesBuffer.Release();
        if (neighboursBuffer != null)
            neighboursBuffer.Release();
    }

    void CreateMaterialFromRenderShader()
    {
        if (RenderShader != null)
            RenderMaterial = new Material (RenderShader);
        else 
            Debug.Log ("Warning! Attempting to initialise MassSpringSystem without setting the Shader variable.");
    }

    /** Calculate our entire grid resolution and vertex count from the structure of the compute shader.
     *  Create our render material, and initialise and fill our compute buffers. Send the vertex neighbour 
     *  positions to the compute shader (we only need to do this once, whereas the position and velocities
     *  need to be sent continuously). Finally, we get the initial positions from the compute buffer and use
     *  them to spawn our game objects using the Spawner.
     */
    public void Initialise()
    { 
        GridResX  = gridUnitSideX * numThreadsPerGroupX;
        GridResY  = gridUnitSideY * numThreadsPerGroupY;
        VertCount = GridResX * GridResY; 
        CreateMaterialFromRenderShader();
        CreateBuffers();
        MassSpringComputeShader.SetBuffer (VelKernel/*PosKernel*/, SpringComputeShaderProperties.NeighboursBufferName, neighboursBuffer);
        Vector3[] positions  = new Vector3[VertCount];
        positionBuffer.GetData (positions);
        Spawner.SetMassUnitSize (SpringLength);
        Spawner.SpawnPrimitives (positions);
    }

    //===========================================================================================
    // Touch Input
    //===========================================================================================
    
    /** Fill and return an array of vertex positions that are the direct neighbouring positions of position
     *  'index' in the mass spring grid.
     *  
     *  Neighbours are listed in 'clockwise' order:
     *  north, north-east, east, south-east, south, south-west, west, north-west
     *  
     *  This function does NOT check the index bounds. 
     */
    public int[] GetNeighbours (int index)
    {
        //n, ne, e, se, s, sw, w, nw
        int[] neighbours = new int[8] {index + GridResX, index + GridResX + 1, index + 1, index - GridResX + 1,
                                       index - GridResX, index - GridResX - 1, index - 1, index + GridResX - 1};

        return neighbours;
    }

    /** The followin functions check whether neighbouring indexes (nIdx) exist within a grid of given
     *  x dimension (gridSideX) and number of vertices (maxIdx).
     */
    bool eastNeighbourExists ( int nIdx,  int gridSideX,  int maxIdx)
    {
        return nIdx % gridSideX > 0 && nIdx < maxIdx;
    }

    bool eastBendNeighbourExists ( int nIdx,  int gridSideX,  int maxIdx)
    {
        return nIdx % gridSideX > 1 && nIdx < maxIdx;
    }

    bool westNeighbourExists (int nIdx,  int gridSideX)
    {
        return (nIdx % gridSideX) < (gridSideX - 1) && nIdx >= 0;
    }

    bool westBendNeighbourExists (int nIdx,  int gridSideX)
    {
        return (nIdx % gridSideX) < (gridSideX - 2) && nIdx >= 0;
    }

    bool verticalNeighbourExists (int nIdx,  int maxIdx)
    {
        return nIdx >= 0 && nIdx < maxIdx;
    }

    /** Fill and return an array of Vector2 where x = neighbour position and y = neighbour exists in grid, 
     *  including both direct neighbour positions and "bend" positions.
     *  Bend positions are 2 grid spaces away on both x and y axes, and implement
     *  resistance to bending in the mass spring grid.
     *  
     *  Neighbours are listed in 'clockwise' order of direct neighbours followed by clockwise bend neighbour positions:
     *  north, north-east, east, south-east, south, south-west, west, north-west, north-bend, east-bend, south-bend, west-bend. 
     */
    public Vector2[] GetNeighbourIndexFlagPairs (int index)
    {
        //n, ne, e, se, s, sw, w, nw, nb, eb, sb, wb
        int[] neighburIndexes = GetNeighbours (index);
        int[] bendIndexes     = {neighburIndexes[0] + GridResX, neighburIndexes[2] + 1, neighburIndexes[4] - GridResX, neighburIndexes[6] - 1 };
        int[] neighbours      = new int[12];
        neighburIndexes.CopyTo (neighbours, 0);
        bendIndexes.CopyTo     (neighbours, 8);

        /** Depending on the specific neighbour position, we need to check varying bounds conditions.
         */
        Vector2[] neighbourFlagPairs = new Vector2[12];
        for (int i = 0; i < 12; ++i)
        {
            int idx = neighbours[i];
            float flag = 0.0f;
            if (i % 4 == 0 || i == 10)
                flag = verticalNeighbourExists (idx, VertCount) ? 1.0f : 0.0f;
            else if (i == 1 || i == 3)
                flag = verticalNeighbourExists (idx, VertCount) && eastNeighbourExists (idx, GridResX, VertCount) ? 1.0f : 0.0f;
            else if (i == 2)
                flag = eastNeighbourExists (idx, GridResX, VertCount) ? 1.0f : 0.0f;
            else if (i == 9)
                flag = eastBendNeighbourExists (idx, GridResX, VertCount) ? 1.0f : 0.0f;
            else if (i == 5 || i == 7)
                flag = verticalNeighbourExists (idx, VertCount) && westNeighbourExists (idx, GridResX) ? 1.0f : 0.0f;
            else if (i == 6)
                flag = westNeighbourExists (idx, GridResX) ? 1.0f : 0.0f;
            else if (i == 11)
                flag = westBendNeighbourExists (idx, GridResX) ? 1.0f : 0.0f;
            neighbourFlagPairs[i] = new Vector2 (idx, flag);    
        }

        return neighbourFlagPairs;
    }

    /** Returns whether a given index position is within the bounds of the grid. Our grid is structured to have rigid, non-moving edges.
     */
    public bool IndexExists (int index)
    {
        return index > GridResX * 2 && index % GridResX > 1 && index % GridResX < GridResX - 2 && index < GridResX * GridResY - 2;
    }

    /** Applies a given pressure value to a given mass index. This pressure is added to the 
     *  external forces acting on the grid of masses.
     */
    public void ApplyPressureToMass (int index, float pressure, ref Vector3[] extForces)
    {
        if (IndexExists (index))
        { 
            Vector3 f = extForces[index];
            extForces[index] = new Vector3 (0.0f, 0.0f, MaxTouchForce * pressure * -1.0f);
        }
    }

    /** Gets the neighbouring positions of a mass index and applies reduced pressure to them.
     */
    public void ApplyPressureToNeighbours (int index, float pressure, ref Vector3[] extForces)
    {
        int[] neighbours = GetNeighbours (index);
        foreach (int i in neighbours)
            ApplyPressureToMass (i, pressure * 0.5f, ref extForces);
    }

    /** Takes in an existing touch or mouse event and transforms it into pressure to be applied at
     *  a specific point on the grid.
     */
    public void UITouchInputUpdated (float x, float y, float pressure,  ref Vector3[] extForces)
    {        
        float WorldGridSideLengthX = GetWorldGridSideLengthX();
        float WorldGridSideLengthY = GetWorldGridSideLengthY();

        int xPosition = (int) (((x + (WorldGridSideLengthX / 2.0f)) / WorldGridSideLengthX) * GridResX);
        int zPosition = (int) (((y + (WorldGridSideLengthY / 2.0f)) / WorldGridSideLengthY) * GridResY);

        int index = xPosition + zPosition * GridResX;
        if (index < 0 || index > VertCount)
            Debug.Log ("Warning: Touch or mouse input generated out of bounds grid index.");

        ApplyPressureToMass (index, pressure, ref extForces);
        ApplyPressureToNeighbours (index, pressure, ref extForces);
    }

    /** Called continuously by the update function to transform input data to grid forces.
     */
    private void HandleTouches()
    {
        Vector3[] extForces = new Vector3[VertCount];
        for (int i = 0; i < VertCount; i++)
            extForces[i] = new Vector3 (0.0f, 0.0f, 0.0f);

        foreach (Vector3 gridTouch in UITouchHandler.GridTouches)
            UITouchInputUpdated (gridTouch.x, gridTouch.y, gridTouch.z, ref extForces);

        externalForcesBuffer.SetData (extForces);

        UITouchHandler.GridTouches.Clear();
    }

    //===========================================================================================
    // Shader Values
    //===========================================================================================

    void SetGridPropertiesAndTime()
    {
        propertiesBuffer.SetData (new float[] {Mass, Damping, SpringStiffness, SpringLength} );
        deltaTimeBuffer.SetData  (new float[] {Time.deltaTime});
    }

    void SetPositionBuffers()
    {
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.DeltaTimeBufferName,      deltaTimeBuffer);
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.PositionBufferName,       positionBuffer);
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.VelocityBufferName,       velocityBuffer);
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.ExternalForcesBufferName, externalForcesBuffer);  
    }

    void SetVelocityBuffers()
    {
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.PropertiesBufferName,     propertiesBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.DeltaTimeBufferName,      deltaTimeBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.ExternalForcesBufferName, externalForcesBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.VelocityBufferName,       velocityBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.PositionBufferName,       positionBuffer);
    }

    void UpdatePrimitivePositions()
    {
        Vector3[] positions = new Vector3[VertCount];
        positionBuffer.GetData (positions);

        if (Spawner != null)
            Spawner.UpdatePositions (positions);
    }

    void Dispatch()
    {
        SetGridPropertiesAndTime();
        
        SetVelocityBuffers();
        MassSpringComputeShader.Dispatch  (VelKernel, gridUnitSideX, gridUnitSideY, 1);

        SetPositionBuffers();
        MassSpringComputeShader.Dispatch  (PosKernel, gridUnitSideX, gridUnitSideY, 1);

    }

    //===========================================================================================
    // Rendering
    //===========================================================================================

    /*
     * If you want to use this to debug the positions, you need to override OnPostRender() instead of Update().
     */
    void RenderDataPoints()
    {
        RenderMaterial.SetPass (0);
        RenderMaterial.SetBuffer (MassSpringRenderShaderProperties.DebugBuffer, debugBuffer);
        RenderMaterial.SetBuffer (MassSpringRenderShaderProperties.PositionsBuffer, positionBuffer);
        RenderMaterial.SetBuffer (MassSpringRenderShaderProperties.VelocityBuffer, velocityBuffer);
        Graphics.DrawProcedural (MeshTopology.Points, VertCount);
    }

    //===========================================================================================
    // Animation
    //===========================================================================================

    /** This function can be used to simulate some movement on the grid.
     *  It should be called continuously (e.g. in the Update or OnPostRender
     *  functions).
     */ 
    void AnimateGrid()
    {
        Vector3[] velocities = new Vector3[VertCount];
        velocityBuffer.GetData (velocities);
        float gestureTime = 1.0f;
        float damp = Time.time < gestureTime? (gestureTime - Time.time) / gestureTime : 0.0f;
        float amp = 20.0f;
        float freq = 0.1f;
        for (int i = 2 * GridResX + 2; i < 3 * GridResX - 2; i++)
            velocities[i] = new Vector3 (0.0f, 0.0f, Mathf.Sin (Time.time * freq) * damp * amp);
        velocityBuffer.SetData (velocities);
    }

    
}
