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
using UnityEngine.UI;
using System.Collections;

enum MouseEventType
{
    MouseDown = 0,
    MouseDrag,
    MouseUp
};

//================================================================================================
// A controller for any GUI object that wants to respond to mouse or touch input. 
// The CanvasTouchManager class manages a collection of these touch handlers and forwards input
// data to them according to whether the input touch or mouse event lies within their screen bounds.
//================================================================================================

public class CanvasTouchHandler : MonoBehaviour
{
    public int touchID;

    public CanvasTouchHandler()
    {
        touchID = -1;
    }

    public virtual void HandleNewOrExistingTouch (Touch t)
    {}

    public virtual void HandleTouchEnded (Touch t)
    {}

    public virtual void HandleMouseDownEvent (Vector2 mousePosition) { }
    public virtual void HandleMouseDragEvent (Vector2 mousePosition) { }
    public virtual void HandleMouseUpEvent   (Vector2 mousePosition) { }

    public bool HasMouseDown;
}

//================================================================================================
// Summary
//================================================================================================
/**
 * This class manages a collection of these touch handlers and forwards input data to them 
 * according to whether the input touch or mouse event lies within their screen bounds.
 * 
 * The CanvasObjects array can be populated in the Unity editor with other UI objects. 
 */

public class CanvasTouchManager : CanvasTouchHandler
{
    public GameObject[] CanvasObjects;
    public ArrayList    GridTouches = new ArrayList();

    /** A scaling value for the pressure that is simulated from input events in the mass spring grid.
     */
    [Range(0.0f, 1.0f)] public float SimulatedPressure = 1.0f;

    /** Holds the result of raycasts from the camera into the scene that are used to check for collisions
        with mass objects.
     */
    private RaycastHit raycastResult;

	void Update ()
    {
        if (Input.touchCount > 0)
	        HandleTouches();
        if (Input.GetMouseButtonDown (0))
            HandleMouseEvent (MouseEventType.MouseDown);
        if (Input.GetMouseButton (0))
            HandleMouseEvent (MouseEventType.MouseDrag);
        if (Input.GetMouseButtonUp (0))
            HandleMouseEvent (MouseEventType.MouseUp);
	}

    /** Checks whether the mouse event is within any child touch handlers and forwards the mouse event to them if so.
     *  Otherwise, calls the relevant handler function for the given mouse event.
     */
    private void HandleMouseEvent (MouseEventType eventType)
    {
        if (SystemInfo.deviceType != DeviceType.Handheld)
        { 
            bool eventHandled = false;
            for (int c = 0; c < CanvasObjects.Length; c++)
            {
                CanvasTouchHandler handler = CanvasObjects[c].GetComponent<CanvasTouchHandler>();
                if (handler != null && (IsScreenPositionInChildBounds (CanvasObjects[c], Input.mousePosition) || handler.HasMouseDown))
                {
                    switch (eventType)
                    {
                        case MouseEventType.MouseDown: handler.HandleMouseDownEvent (Input.mousePosition); break;
                        case MouseEventType.MouseDrag: handler.HandleMouseDragEvent (Input.mousePosition); break;
                        case MouseEventType.MouseUp:   handler.HandleMouseUpEvent   (Input.mousePosition); break;
                    }
                    eventHandled = true; 
                    break;
                }
            }
            if ( ! eventHandled)
            { 
                switch (eventType)
                {
                    case MouseEventType.MouseDown: HandleMouseDownEvent (Input.mousePosition); break;
                    case MouseEventType.MouseDrag: HandleMouseDragEvent (Input.mousePosition); break;
                    case MouseEventType.MouseUp:   HandleMouseUpEvent   (Input.mousePosition); break;
                }
            }
        }
    }

    public override void HandleMouseDownEvent (Vector2 mousePosition) { ProjectScreenPositionToMassSpringGrid (mousePosition); }
    public override void HandleMouseDragEvent (Vector2 mousePosition) { ProjectScreenPositionToMassSpringGrid (mousePosition); }

    /** Cast a ray from the given screen position and check for collision with mass objects.
     *  If there is a collision with a mass object, add a touch point to the grid touches array
     *  (e.g. to be later be handled by a MassSpringSystem controller).
     */
    public void ProjectScreenPositionToMassSpringGrid (Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay (screenPosition);
        if (Physics.Raycast (ray, out raycastResult))
        {
            GameObject obj = raycastResult.collider.gameObject;
            if (MassSpringSystem.IsMassUnit (obj.tag))
            {
                Vector3 p = obj.transform.position;
                //need to translate back from unity world space so we use z here rather than y
                GridTouches.Add (new Vector3 (p.x, p.z, SimulatedPressure));
            }
        }
    }

    /** Checks whether the touch is within any child touch handlers and forwards the touch event to them if so.
     *  Otherwise, calls the relevant handler function for the given touch event.
     */
    private void HandleTouches()
    {
        for (int t = 0; t < Input.touchCount; t++)
        {
            Touch touch = Input.touches[t];
            bool isNewOrExistingTouch = (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary);
            bool touchHandled = ForwardTouchToChildren (touch, isNewOrExistingTouch);
            if ( ! touchHandled)
                HandleTouch (touch, isNewOrExistingTouch);
        }
    }

    /** Checks whether the touch is within any child touch handlers and forwards the touch event to them if so.
     *  Returns true if the touch has been forwarded or false if the touch remains unhandled.
     */
    private bool ForwardTouchToChildren (Touch touch, bool isNewOrExistingTouch)
    {
        for (int c = 0; c < CanvasObjects.Length; c++)
        {
            CanvasTouchHandler handler = CanvasObjects[c].GetComponent<CanvasTouchHandler>();

            bool touchInChild = IsScreenPositionInChildBounds (CanvasObjects[c], touch.position);
            if (handler != null && (handler.touchID == touch.fingerId || (handler.touchID == -1 && touchInChild)))
            {
                if (isNewOrExistingTouch)
                    handler.HandleNewOrExistingTouch (touch);
                else
                    handler.HandleTouchEnded (touch);
                return true;
            }
        }
        return false;
    }
    
    public void HandleTouch (Touch t, bool isNewOrExistingTouch)
    {
        if(isNewOrExistingTouch)
            HandleNewOrExistingTouch (t);
        else
            HandleTouchEnded (t);
    }

    public override void HandleNewOrExistingTouch (Touch t)
    {
        base.HandleNewOrExistingTouch (t);
        ProjectScreenPositionToMassSpringGrid (t.position);
    }

    private bool IsScreenPositionInChildBounds (GameObject childElement, Vector2 touchScreenPosition)
    {
        if (childElement == null)
            return false;

        RectTransform childRectTrasform = childElement.GetComponent<RectTransform>();
        if (childRectTrasform == null)
            return false;

        Vector2 p = childRectTrasform.anchoredPosition;
        Rect rect = new Rect (p.x, p.y, childRectTrasform.sizeDelta.x, childRectTrasform.sizeDelta.y);
        return rect.Contains (touchScreenPosition);
    }
}
