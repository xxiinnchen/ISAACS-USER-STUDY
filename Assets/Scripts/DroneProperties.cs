﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneProperties : MonoBehaviour
{

    public Drone classPointer;

    // Prefab pointers
    public GameObject Arrows;
    public GameObject shatteredDrone;
    public GameObject InteractionBubble;
    public GameObject WarningBubble;

    // Materials
    public Material PurpleBubble;
    public Material BlueBubble;
    public Material RedBubble;
    public Material GreenBubble;
    public Material NoBubble;

    public MeshRenderer meshRend;

    public bool EnableLineRender = true;
    private int LineCount = 0;
    private const int LINERENDER = 10;

    // Start is called before the first frame update

    void Start()
    {
        classPointer.SPEED = Utility.DRONE_SPEED;
    }

    // Update is called once per frame
    void Update()
    {
        if (LineCount % LINERENDER == 0 && EnableLineRender)
        {
            Vector3 curPos = gameObject.transform.position;
            DrawLine(curPos + 95 * Utility.DRONE_SPEED * classPointer.direction, curPos + 100 * Utility.DRONE_SPEED * classPointer.direction);
        }

        LineCount++;
    }

    public void DrawLine(Vector3 start, Vector3 end, float duration = 1.5f)
    {
        GameObject myLine = new GameObject();
        myLine.name = "Line";
        myLine.transform.parent = this.gameObject.transform;
        myLine.transform.position = start;
        myLine.AddComponent<LineRenderer>();
        LineRenderer lr = myLine.GetComponent<LineRenderer>();
        lr.material = Resources.Load("Traj", typeof(Material)) as Material;
        lr.startColor = Utility.Traj;
        lr.endColor = Utility.Traj;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        Object.Destroy(myLine, duration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "drone")
        {
            classPointer.TriggerCollision(other);
        }
    }

}
