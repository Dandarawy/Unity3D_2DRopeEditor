using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
[CustomEditor(typeof(Rope))]
public class RopeEditor : Editor
{
    Texture nodeTexture;
    Texture dotHandleTexture;
    static GUIStyle handleStyle = new GUIStyle();
    static GUIStyle dothandleStyle = new GUIStyle();
    List<int> alignedPoints=new List<int>();

    Vector3 ropePosition;
    void OnEnable()
    {
        nodeTexture = Resources.Load<Texture>("Handle");
        if (nodeTexture == null) nodeTexture = EditorGUIUtility.whiteTexture;
        dotHandleTexture = Resources.Load<Texture>("DotHandle");
        if (dotHandleTexture == null) nodeTexture = EditorGUIUtility.whiteTexture;
        handleStyle.alignment = TextAnchor.MiddleCenter;
        handleStyle.fixedWidth = 15;
        handleStyle.fixedHeight = 15;
        dothandleStyle.alignment = TextAnchor.MiddleCenter;
        dothandleStyle.fixedWidth = 8;
        dothandleStyle.fixedHeight = 8;
        ropePosition = (target as Rope).transform.position;
    }
    void OnSceneGUI()
    {
        Rope rope = (target as Rope);
        Vector3[] localPoints = rope.nodes.ToArray();
        Vector3[] worldPoints = new Vector3[rope.nodes.Count];
        for (int i = 0; i < worldPoints.Length; i++)
            worldPoints[i] = rope.transform.TransformPoint(localPoints[i]);
        DrawPolyLine(worldPoints);
        DrawNodes(rope, worldPoints);
        DrawHinges(rope);
        
        if (Event.current.shift)
        {
            //Adding Points
            Vector3 mousePos = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;
            Vector3 polyLocalMousePos = rope.transform.InverseTransformPoint(mousePos);
            Vector3 nodeOnPoly = HandleUtility.ClosestPointToPolyLine(worldPoints);
            float handleSize = HandleUtility.GetHandleSize(nodeOnPoly);
            int nodeIndex = FindNodeIndex(worldPoints, nodeOnPoly);
            Handles.DrawLine(worldPoints[nodeIndex - 1], mousePos);
            Handles.DrawLine(worldPoints[nodeIndex], mousePos);
            if (Handles.Button(mousePos, Quaternion.identity, handleSize * 0.1f, handleSize, HandleFunc))
            {
                polyLocalMousePos.z = 0;
                Undo.RecordObject(rope, "Insert Node");
                rope.nodes.Insert(nodeIndex, polyLocalMousePos);
                UpdateRope(rope);
                Event.current.Use();
            } 
        }
        if (Event.current.control && rope.nodes.Count > 2)
        {
            //Deleting Points
            int indexToDelete = FindNearestNodeToMouse(worldPoints);
            Handles.color = Color.red;
            float handleSize = HandleUtility.GetHandleSize(worldPoints[0]);
            if (Handles.Button(worldPoints[indexToDelete], Quaternion.identity, handleSize * 0.09f, handleSize, DeleteHandleFunc))
            {
                Undo.RecordObject(rope, "Remove Node");
                rope.nodes.RemoveAt(indexToDelete);
                indexToDelete = -1;
                UpdateRope(rope);
                Event.current.Use();
            }
            Handles.color = Color.white;
        }
        if(ropePosition!=rope.transform.position)
        {
            ropePosition = rope.transform.position;
            UpdateEndsJoints(rope);
        }
    }

    private void DrawHinges(Rope rope)
    {
        if (rope.WithPhysics &&
            rope.HangFirstSegment &&
            rope.transform.childCount > 0)
        {
            //Draw Hinge 
            Transform firstSegment = rope.transform.GetChild(0);
            float handleSize = HandleUtility.GetHandleSize(firstSegment.position);
            Vector2 hingePositionInWorldSpace = rope.transform.TransformPoint(rope.FirstSegmentConnectionAnchor);
            if (GetConnectedObject(hingePositionInWorldSpace, firstSegment.GetComponent<Rigidbody2D>()))
                GUI.color = Color.magenta;
            else
                GUI.color = Color.blue;
            Vector2 newFirstSegmentAnchor = Handles.FreeMoveHandle(hingePositionInWorldSpace,
                Quaternion.identity, handleSize * 0.05f, Vector3.one, SolidHandleFunc);
            if (newFirstSegmentAnchor != hingePositionInWorldSpace)
            {
                rope.FirstSegmentConnectionAnchor = rope.transform.InverseTransformPoint(newFirstSegmentAnchor);
                UpdateEndsJoints(rope);
            }
            GUI.color = Color.white;
        }

        if (rope.WithPhysics && rope.HangLastSegment)
        {
            //Draw Hinge 
            Transform lastSegment = rope.transform.GetChild(rope.transform.childCount - 1);
            float handleSize = HandleUtility.GetHandleSize(lastSegment.position);
            Vector2 hingePositionInWorldSpace = rope.transform.TransformPoint(rope.LastSegmentConnectionAnchor);
            if (GetConnectedObject(hingePositionInWorldSpace, lastSegment.GetComponent<Rigidbody2D>()))
                GUI.color = Color.magenta;
            else
                GUI.color = Color.blue;
            Vector2 newLastSegmentAnchor = Handles.FreeMoveHandle(hingePositionInWorldSpace,
            Quaternion.identity, handleSize * 0.05f, Vector3.one, SolidHandleFunc);
            GUI.color = Color.white;
            if (newLastSegmentAnchor != hingePositionInWorldSpace)
            {
                rope.LastSegmentConnectionAnchor = rope.transform.InverseTransformPoint(newLastSegmentAnchor);
                UpdateEndsJoints(rope);
            }
        }
    }
    static Rigidbody2D GetConnectedObject(Vector2 position, Rigidbody2D originalObj)
    {
        Rigidbody2D[] sceneRigidbodies = GameObject.FindObjectsOfType<Rigidbody2D>();
        for(int i=0;i<sceneRigidbodies.Length;i++)
        {
            if(originalObj!=sceneRigidbodies[i]&& sceneRigidbodies[i].GetComponent<SpriteRenderer>().bounds.Contains(position))
            {
                return sceneRigidbodies[i];
            }
        }
        return null;
    }
    private int FindNearestNodeToMouse(Vector3[] worldNodesPositions)
    {
        Vector3 mousePos = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;
        mousePos.z = 0;
        int index = -1;
        float minDistnce = float.MaxValue;
        for (int i = 0; i < worldNodesPositions.Length; i++)
        {
            float distance = Vector3.Distance(worldNodesPositions[i], mousePos);
            if (distance < minDistnce)
            {
                index = i;
                minDistnce = distance;
            }
        }
        return index;
    }
    private int FindNodeIndex(Vector3[] worldNodesPositions, Vector3 newNode)
    {
        float smallestdis = float.MaxValue;
        int prevIndex = 0;
        for (int i = 1; i < worldNodesPositions.Length; i++)
        {
            float distance = HandleUtility.DistanceToPolyLine(worldNodesPositions[i - 1], worldNodesPositions[i]);
            if (distance < smallestdis)
            {
                prevIndex = i - 1;
                smallestdis = distance;
            }
        }
        return prevIndex + 1;
    }
    private void DrawPolyLine(Vector3[] nodes)
    {
        if (Event.current.shift) Handles.color = Color.green;
        else if (Event.current.control ) Handles.color = Color.red;
        else Handles.color = Color.white;
        for(int i=0;i<nodes.Length-1;i++)
        {
            if(alignedPoints.Contains(i)&&alignedPoints.Contains(i+1))
            {
                Color currentColor = Handles.color;
                Handles.color = Color.green;
                Handles.DrawLine(nodes[i], nodes[i + 1]);
                Handles.color = currentColor;
            }
            else
                Handles.DrawLine(nodes[i], nodes[i + 1]);

        }
        Handles.color = Color.white;
    }
    private void DrawNodes(Rope rope, Vector3[] worldPoints)
    {
        for (int i = 0; i < rope.nodes.Count; i++)
        {
            Vector3 pos = rope.transform.TransformPoint(rope.nodes[i]);
            float handleSize = HandleUtility.GetHandleSize(pos);
            Vector3 newPos = Handles.FreeMoveHandle(pos, Quaternion.identity, handleSize * 0.09f, Vector3.one, HandleFunc);
            if (newPos != pos)
            {
                CheckAlignment(worldPoints, handleSize * 0.1f, i, ref newPos);
                Undo.RecordObject(rope, "Move Node");
                rope.nodes[i] = rope.transform.InverseTransformPoint(newPos);
                UpdateRope(rope);
            }
        }
    }
    bool CheckAlignment(Vector3[] worldNodes, float offset, int index, ref Vector3 position)
    {
        alignedPoints.Clear();
        bool aligned = false;
        //check straight lines
        //check previous line
        if (index >= 2)
        {
            //represent the line with the equation y=mx+b
            float dy = worldNodes[index - 1].y - worldNodes[index - 2].y;
            float dx = worldNodes[index - 1].x - worldNodes[index - 2].x;
            float m = dy / dx;
            float b = worldNodes[index - 1].y - m * worldNodes[index - 1].x;

            float newX = (position.x + m * (position.y - b)) / (m * m + 1);
            float newY = (m * (position.x + m * position.y) + b) / (m * m + 1);
            Vector3 newPos = new Vector3(newX, newY);
            float distance = Vector3.Distance(newPos, position);
            if (distance * distance < offset * offset)
            {
                position.x = newX;
                position.y = newY;
                aligned = true;
                alignedPoints.Add(index - 1);
                alignedPoints.Add(index - 2);
            }
        }
        //check next line
        if (index < worldNodes.Length - 2)
        {
            //represent the line with the equation y=mx+b
            float dy = worldNodes[index + 1].y - worldNodes[index + 2].y;
            float dx = worldNodes[index + 1].x - worldNodes[index + 2].x;
            float m = dy / dx;
            float b = worldNodes[index + 1].y - m * worldNodes[index + 1].x;

            float newX = (position.x + m * (position.y - b)) / (m * m + 1);
            float newY = (m * (position.x + m * position.y) + b) / (m * m + 1);
            Vector3 newPos = new Vector3(newX, newY);
            float distance = Vector3.Distance(newPos, position);
            if (distance * distance < offset * offset)
            {
                position.x = newX;
                position.y = newY;
                aligned = true;
                alignedPoints.Add(index + 1);
                alignedPoints.Add(index + 2);
            }
        }
        //check vertical
        //check with the prev node
        //the node can be aligned to the prev and next node at once, we need to return more than one alginedTo Node
        if (index > 0)
        {
            float dx = Mathf.Abs(worldNodes[index - 1].x - position.x);
            if (dx < offset)
            {
                position.x = worldNodes[index - 1].x;
                alignedPoints.Add(index - 1);
                aligned = true;
            }
        }
        //check with the next node
        if (index < worldNodes.Length - 1)
        {
            float dx = Mathf.Abs(worldNodes[index + 1].x - position.x);
            if (dx < offset)
            {
                position.x = worldNodes[index + 1].x;
                alignedPoints.Add(index + 1);
                aligned = true;
            }
        }
        //check horizontal
        if (index > 0)
        {
            float dy = Mathf.Abs(worldNodes[index - 1].y - position.y);
            if (dy < offset)
            {
                position.y = worldNodes[index - 1].y;
                alignedPoints.Add(index - 1);
                aligned = true;
            }
        }
        //check with the next node
        if (index < worldNodes.Length - 1)
        {
            float dy = Mathf.Abs(worldNodes[index + 1].y - position.y);
            if (dy < offset)
            {
                position.y = worldNodes[index + 1].y;
                alignedPoints.Add(index + 1);
                aligned = true;
            }
        }
        

        if(aligned)
            alignedPoints.Add(index);

        return aligned;
    }
    void HandleFunc(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        if (controlID == GUIUtility.hotControl)
            GUI.color = Color.red;
        else
            GUI.color = Color.green;
        Handles.Label(position, new GUIContent(nodeTexture), handleStyle);
        GUI.color = Color.white;
    }
    void SolidHandleFunc(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        Handles.Label(position, new GUIContent(dotHandleTexture), dothandleStyle);
    }
    void DeleteHandleFunc(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        GUI.color = Color.red;
        Handles.Label(position, new GUIContent(nodeTexture), handleStyle);
        GUI.color = Color.white;
    }
    static void UpdateRope(Rope rope)
    {
        DestroyChildren(rope);
        if (rope.SegmentsPrefabs==null||rope.SegmentsPrefabs.Length == 0)
        {
            Debug.LogWarning("Rope Segments Prefabs is Empty");
            return;
        }
        float segmentHeight = rope.SegmentsPrefabs[0].bounds.size.y * (1 + rope.overlapFactor);
        List<Vector3> nodes = rope.nodes;
        int currentSegPrefIndex = 0;
        Rigidbody2D previousSegment = null;
        float previousTheta = 0;
        int currentSegment = 0;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            //construct line between nodes[i] and nodes[i+1]
            float theta = Mathf.Atan2(nodes[i + 1].y - nodes[i].y, nodes[i + 1].x - nodes[i].x);
            float dx = segmentHeight * Mathf.Cos(theta);
            float dy = segmentHeight * Mathf.Sin(theta);
            float startX = nodes[i].x + dx / 2;
            float startY = nodes[i].y + dy / 2;
            float lineLength = Vector2.Distance(nodes[i + 1], nodes[i]);
            int segmentCount = 0;
            switch(rope.OverflowMode)
            {
                case LineOverflowMode.Round:
                    segmentCount = Mathf.RoundToInt(lineLength / segmentHeight);
                    break;
                case LineOverflowMode.Shrink:
                    segmentCount = (int)(lineLength / segmentHeight);
                    break;
                case LineOverflowMode.Extend:
                    segmentCount = Mathf.CeilToInt(lineLength / segmentHeight);
                    break;
            }
            for (int j = 0; j < segmentCount; j++)
            {
                if (rope.SegmentsMode == SegmentSelectionMode.RoundRobin)
                {
                    currentSegPrefIndex++;
                    currentSegPrefIndex %= rope.SegmentsPrefabs.Length;
                }
                else if (rope.SegmentsMode == SegmentSelectionMode.Random)
                {
                    currentSegPrefIndex = Random.Range(0, rope.SegmentsPrefabs.Length);
                }
                GameObject segment = (Instantiate(rope.SegmentsPrefabs[currentSegPrefIndex]) as SpriteRenderer).gameObject;
                segment.name = "Segment_" + currentSegment;
                segment.transform.parent = rope.transform;
                segment.transform.localPosition = new Vector3(startX + dx * j, startY + dy * j);
                segment.transform.localRotation = Quaternion.Euler(0, 0, theta * Mathf.Rad2Deg - 90);
                if (rope.WithPhysics)
                {
                    Rigidbody2D segRigidbody = segment.GetComponent<Rigidbody2D>();
                    if (segRigidbody == null)
                        segRigidbody = segment.AddComponent<Rigidbody2D>();
                    //if not the first segment, make a joint
                    if (currentSegment != 0)
                    {
                        float dtheta = 0;
                        if (j == 0)
                        {
                            //first segment in the line
                            dtheta = (theta - previousTheta) * Mathf.Rad2Deg;
                            if (dtheta > 180) dtheta -= 360;
                            else if (dtheta < -180) dtheta += 360;
                        }
                        //add Hinge
                        AddJoint(rope, dtheta, segmentHeight, previousSegment, segment);
                    }
                    previousSegment = segRigidbody;
                }
                currentSegment++;
            }
            previousTheta = theta;
        }
        UpdateEndsJoints(rope);
    }
    private static void UpdateEndsJoints(Rope rope)
    {
        Transform firstSegment = rope.transform.GetChild(0);
        if (rope.WithPhysics&& 
            rope.HangFirstSegment&&
            rope.transform.childCount>0)
        {

            HingeJoint2D joint = firstSegment.gameObject.GetComponent<HingeJoint2D>();
            if(!joint)
                joint = firstSegment.gameObject.AddComponent<HingeJoint2D>();
            Vector2 hingePositionInWorldSpace = rope.transform.TransformPoint(rope.FirstSegmentConnectionAnchor);
            joint.connectedAnchor = hingePositionInWorldSpace;
            joint.anchor = firstSegment.transform.InverseTransformPoint(hingePositionInWorldSpace);
            joint.connectedBody = GetConnectedObject(hingePositionInWorldSpace, firstSegment.GetComponent<Rigidbody2D>());
            if(joint.connectedBody)
            {
                joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(hingePositionInWorldSpace);
            }
        }
        else
        {
             HingeJoint2D joint = firstSegment.gameObject.GetComponent<HingeJoint2D>();
             if (joint) DestroyImmediate(joint);
        }
        Transform lastSegment = rope.transform.GetChild(rope.transform.childCount - 1);
        if (rope.WithPhysics && rope.HangLastSegment)
        {
            HingeJoint2D[] joints = lastSegment.gameObject.GetComponents<HingeJoint2D>();
            HingeJoint2D joint = null;
            if (joints.Length > 1)
                joint = joints[1];
            else
                joint = lastSegment.gameObject.AddComponent<HingeJoint2D>();
            Vector2 hingePositionInWorldSpace = rope.transform.TransformPoint(rope.LastSegmentConnectionAnchor);
            joint.connectedAnchor = hingePositionInWorldSpace;
            joint.anchor = lastSegment.transform.InverseTransformPoint(hingePositionInWorldSpace) ;
            joint.connectedBody = GetConnectedObject(hingePositionInWorldSpace, lastSegment.GetComponent<Rigidbody2D>());
            if (joint.connectedBody)
            {
                joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(hingePositionInWorldSpace);
            }
        }
        else
        {
            HingeJoint2D[] joints = lastSegment.gameObject.GetComponents<HingeJoint2D>();
            if (joints.Length > 1)
                for (int i = 1; i < joints.Length; i++)
                    DestroyImmediate(joints[i]);
        }
    }
    private static void AddJoint(Rope rope, float dtheta, float segmentHeight, Rigidbody2D previousSegment, GameObject segment)
    {
        HingeJoint2D joint = segment.AddComponent<HingeJoint2D>();
        joint.connectedBody = previousSegment;
        joint.anchor = new Vector2(0, -segmentHeight / 2);
        joint.connectedAnchor = new Vector2(0, segmentHeight / 2);
        if (rope.useBendLimit)
        {
            joint.useLimits = true;
            joint.limits = new JointAngleLimits2D()
            {
                min = dtheta - rope.bendLimit,
                max = dtheta + rope.bendLimit
            };
        }
       
#if UNITY_5
         if (rope.BreakableJoints)
            joint.breakForce = rope.BreakForce;
#endif
    }
    private static void DestroyChildren(Rope rope)
    {
        while (rope.transform.childCount > 0)
            DestroyImmediate(rope.transform.GetChild(0).gameObject);
    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        Rope rope = target as Rope;
        if(rope.WithPhysics)
        {
            bool  hangFirstSegment = EditorGUILayout.Toggle("Hang First Segment", rope.HangFirstSegment);
            if (hangFirstSegment != rope.HangFirstSegment)
            {
                rope.HangFirstSegment = hangFirstSegment;
                if(hangFirstSegment)
                {
                    rope.FirstSegmentConnectionAnchor = rope.nodes[0];
                }
                UpdateEndsJoints(rope);
                SceneView.RepaintAll();
            }
            bool hangLastSegment = EditorGUILayout.Toggle("Hang Last Segment", rope.HangLastSegment);
            if (hangLastSegment != rope.HangLastSegment)
            {
                rope.HangLastSegment = hangLastSegment;
                if(hangLastSegment)
                {
                    rope.LastSegmentConnectionAnchor = rope.nodes[rope.nodes.Count - 1];
                }
                UpdateEndsJoints(rope);
                SceneView.RepaintAll();
            }
            bool useBendLimit = EditorGUILayout.Toggle("Use Bend Limits", rope.useBendLimit);
            if (useBendLimit != rope.useBendLimit)
            {
                rope.useBendLimit = useBendLimit;
                UpdateRope(rope);
            }
            if(rope.useBendLimit)
            {
                int bendLimit = EditorGUILayout.IntSlider("Bend Limits",rope.bendLimit, 0, 180);
                if(bendLimit!=rope.bendLimit)
                {
                    rope.bendLimit = bendLimit;
                    UpdateRope(rope);
                }
            }
#if UNITY_5
            rope.BreakableJoints = EditorGUILayout.Toggle("Breakable Joints", rope.BreakableJoints);
            if(rope.BreakableJoints)
            {
                rope.BreakForce = EditorGUILayout.FloatField("Break Force", rope.BreakForce);

            }
#endif
        }
        if(GUILayout.Button("Update"))
        {
            UpdateRope(rope);
        }
    }
    
    [MenuItem("GameObject/Rope",false,1)]
    public static void CreateRope()
    {
        GameObject g = new GameObject();
        g.name = "Rope";
        g.AddComponent<Rope>();
        Vector3 position= SceneView.lastActiveSceneView.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1.0f)).origin;
        g.transform.position = new Vector3(position.x, position.y, 0);
        Selection.activeGameObject = g;
    }
}
