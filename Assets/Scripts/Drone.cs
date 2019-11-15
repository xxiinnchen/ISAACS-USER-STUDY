using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class Drone
{

    // Drone Position Variables 
    public Vector3 parkingPos;
    public Vector3 hoverPos;
    public Vector3 eventPos;
    public Vector3 spawnPos;
    public Vector3 curPos;
    public Vector3 prevPos;
    public Vector3 dstPos;
    public Vector3 direction;
    public Vector3 epsilon = new Vector3(0.1f, 0.1f, 0.1f);
    public static Vector3 hoverShift = new Vector3(0f, 1.5f, 0f);


    // Drone properties
    public GameObject gameObjectPointer;
    public float SPEED;
    public int droneId;
    public int eventId;
    public int eventNo;
    public int collionDroneId = -2;
    public int nextEvent = -2;
    public float tripTime = 0;
    public float collidesAtTime;

    // Drone Status
    public enum SafetyStatus
    {
        NOT_SAFE = 0,
        TO_SAFE_ZONE = 1,
        SAFE = 2,
    }
    public SafetyStatus safetyStatus = SafetyStatus.NOT_SAFE;

    public enum DroneInteraction
    {
        NO_INTERACTION = 0,
        PAUSED = 1,
        GREEN_BUBBLE = 2,
        FLY_UP = 3,
        FLY_UP_DIAGONAL = 4
    }
    public DroneInteraction droneInteraction = DroneInteraction.NO_INTERACTION;

    public enum DroneStatus
    {
        PARKED = 0,
        TAKEOFF = 1,
        TO_SHELF = 2,
        DELAY = 3,
    }
    public DroneStatus status;



    // UI Features
    public bool EnableArrows = true;
    public bool EnableBubble = false;

    // Arrow Variables
    public GameObject arrows1;
    public GameObject arrows2;
    public GameObject ring;
    private readonly Vector3 arrowOffset1 = new Vector3(0f, 0f, 0f);
    private readonly Vector3 arrowOffset2 = new Vector3(0f, 0f, 0.1412f);

    // Bubble Variables
    public GameObject interactionBubble;
    public GameObject warningBubble;



    // TODO
    public int safeFlightTime = 2;
    public int safetySpeedMultiplier = 3;
    public float safetyFlightStartTime;
    public bool isCollided;


    // Pause Variables
    public static int pauseTime = 100;
    public static float clickTime = 0;
    public static float wrongClickTime = 0;
    public float safetyTime = TrafficControl.worldobject.GetComponent<TrafficControl>().Interact_SafeTime;

    /// <summary>
    /// Initialize drone
    /// </summary>
    /// <param name="droneId"></param>
    /// <param name="initPos"></param>
    public Drone(int droneId, Vector3 initPos)
    {
        // Setup initial drone properties
        this.droneId = droneId;
        this.safetyStatus = SafetyStatus.NOT_SAFE;
        this.parkingPos = this.curPos = initPos;
        this.hoverPos = this.parkingPos + hoverShift;
        this.status = DroneStatus.PARKED;
        this.isCollided = false;


        // Create and assign prefab
        GameObject baseObject = TrafficControl.worldobject.GetComponent<TrafficControl>().droneBaseObject;
        gameObjectPointer = UnityEngine.Object.Instantiate(baseObject, initPos, Quaternion.identity);
        gameObjectPointer.GetComponent<DroneProperties>().classPointer = this;

        gameObjectPointer.name = string.Concat("Drone", droneId.ToString());
        gameObjectPointer.layer = 2;
        // gameObjectPointer.gameObject.tag = string.Concat("Drone", droneId.ToString());
        gameObjectPointer.transform.parent = TrafficControl.worldobject.transform;

        // Find and assign Arrow 1 Prefab
        GameObject arrow1 = gameObjectPointer.GetComponent<DroneProperties>().Arrows;
        arrows1 = UnityEngine.Object.Instantiate(arrow1, initPos, Quaternion.Euler(0f, -90f, 0f));
        arrows1.transform.parent = TrafficControl.worldobject.transform;

        // Find and assign Arrow 2 Prefab
        GameObject arrow2 = gameObjectPointer.GetComponent<DroneProperties>().Arrows;
        arrows2 = UnityEngine.Object.Instantiate(arrow2, initPos, Quaternion.Euler(0f, -270f, 0f));
        arrows2.transform.parent = TrafficControl.worldobject.transform;

        // Find and assign Bubble Prefabs
        this.interactionBubble = gameObjectPointer.GetComponent<DroneProperties>().InteractionBubble;
        this.warningBubble = gameObjectPointer.GetComponent<DroneProperties>().WarningBubble;

        try
        {
            GameObject textHelperChild = this.gameObjectPointer.transform.Find("Text Helper").gameObject;
            TextMeshPro textHelper = textHelperChild.GetComponent<TextMeshPro>();
            textHelper.SetText(this.droneId.ToString());
        }
        catch (NullReferenceException e)
        {
            //Debug.Log(e);
            Debug.Log("No TextMeshPro child.");
        }

    }

    public void AddEvent(Event e)
    {
        this.gameObjectPointer.SetActive(true);
        this.EnableArrows = true;
        this.interactionBubble.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().NoBubble;

        status = DroneStatus.TAKEOFF;
        dstPos = hoverPos;
        eventPos = e.pos;
        direction = Vector3.Normalize(dstPos - parkingPos);
        eventId = e.shelfId;
    }

    public void Interact_Pause()
    {
        safetyStatus = SafetyStatus.SAFE;
        safetyFlightStartTime = Time.time;
        droneInteraction = DroneInteraction.PAUSED;
    }
    public void Interact_GreenBubble()
    {
        safetyStatus = SafetyStatus.SAFE;
        safetyFlightStartTime = Time.time;
        this.interactionBubble.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().GreenBubble;
        droneInteraction = DroneInteraction.GREEN_BUBBLE;

    }
    public void Interact_FlyUp()
    {
        safetyStatus = SafetyStatus.TO_SAFE_ZONE;
        droneInteraction = DroneInteraction.FLY_UP;
    }
    public void Interact_FlyUpDiagonal()
    {
        safetyStatus = SafetyStatus.TO_SAFE_ZONE;
        droneInteraction = DroneInteraction.FLY_UP_DIAGONAL;
    }
    
    public void UI_Warning(bool warning)
    {
        if (warning)
        {
            warningBubble.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().PurpleBubble;
        }
        else
        {
            warningBubble.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().NoBubble;
        }
    }

    public enum MoveStatus
    {
        PAUSED = 0,
        END_TO_SHELF = 1,
        OTHER = -1
    }
    /// <summary>
    /// Update the status of drone and Move
    /// </summary>
    /// <returns> 
    /// drone paused: 0; 
    /// end of to_shelf trip: 1;
    /// end of whole trip: 2;
    /// otherwise: -1 
    /// </returns>
    public MoveStatus Move()
    {
        MoveStatus flag = MoveStatus.OTHER;


        prevPos = curPos;
        curPos = status == DroneStatus.PARKED ? curPos : curPos + direction * SPEED;


        if (safetyStatus == SafetyStatus.TO_SAFE_ZONE)
        {
            if (curPos.y >= 30.0f)
            {
                safetyStatus = SafetyStatus.SAFE;
                safetyFlightStartTime = Time.time;
            }
            else
            {
                if (droneInteraction == DroneInteraction.FLY_UP_DIAGONAL)
                {
                    curPos = curPos + new Vector3(0, SPEED * safetySpeedMultiplier, 0);
                }
                if (droneInteraction == DroneInteraction.FLY_UP)
                {
                    Vector3 orgPos = curPos - direction * SPEED;
                    curPos = orgPos + new Vector3(0, SPEED*safetySpeedMultiplier, 0);
                }
            }
        }
        else if (safetyStatus == SafetyStatus.SAFE)
        {
            if (Time.time - safetyFlightStartTime > safetyTime)
            {
                safetyStatus = SafetyStatus.NOT_SAFE;
                this.interactionBubble.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().NoBubble;
                safetyFlightStartTime = 0;
            }
            else
            {
                if (droneInteraction == DroneInteraction.PAUSED)
                {
                    curPos = prevPos;
                }
                if (droneInteraction == DroneInteraction.FLY_UP)
                {
                    Vector3 orgPos = curPos - direction * SPEED;
                    curPos = orgPos + new Vector3(direction.x * SPEED, 0, direction.z * SPEED);
                }
                if (droneInteraction == DroneInteraction.FLY_UP_DIAGONAL)
                {
                    Vector3 orgPos = curPos - direction * SPEED;
                    curPos = orgPos + new Vector3(direction.x * SPEED, 0, direction.z * SPEED);
                }
            }

        }

        // End of New code


        flag = MoveStatus.OTHER;


        if (status != DroneStatus.PARKED && Utility.IsLessThan(curPos - dstPos, epsilon))
        {
            // Debug.Log(status + " " + curPos + " " + dstPos + " " + hoverPos + " " + parkingPos + " " + eventPos);

            if (Utility.IsLessThan(dstPos - hoverPos, epsilon))
            {
                if (status == DroneStatus.TAKEOFF)  // end of takeoff
                {
                    Utility.DeleteChild(this.gameObjectPointer, "Line");
                    status = DroneStatus.TO_SHELF;
                    dstPos = eventPos;
                    // Debug.Log(droneId + "end of takeoff, now to event: " + eventPos + "cur: " + curPos);
                }
            }
            else if (Utility.IsLessThan(dstPos - eventPos, epsilon)) // Drone Reached the event
            {
                // cur_s = 2 --> 3
                // end of to_shelf trip
                status = DroneStatus.PARKED;
                curPos = parkingPos;
                flag = MoveStatus.END_TO_SHELF;
            }
        }

        gameObjectPointer.transform.position = curPos;
       
        if (EnableArrows)
        {
            DisplayArrow();
        }
        else
        {
            arrows1.SetActive(false);
            arrows2.SetActive(false);
        }
        
        return flag;
    }

    public void RotateArrow()
    {
        Vector3 baseVector = Vector3.up;
        Vector3 basePoint = gameObjectPointer.transform.Find("pCube3").gameObject.transform.position;
        //Quaternion q = Quaternion.LookRotation(-direction, baseVector);
        Quaternion q = Quaternion.FromToRotation(new Vector3(0f, 0f, -1f), direction);
        // Debug.Log(q);
        arrows1.transform.rotation = q * Quaternion.Euler(0f, -90f, 0f);
        arrows2.transform.rotation = q * Quaternion.Euler(0f, -90f, 0f);
    }

    public void DisplayArrow()
    {
        RotateArrow();
        Vector3 basePoint = gameObjectPointer.transform.Find("pCube3").gameObject.transform.position;
        arrows1.transform.position = basePoint + arrowOffset1;
        arrows2.transform.position = curPos + arrowOffset2;


        if (status == DroneStatus.TAKEOFF)
        {
            arrows1.SetActive(true);
            arrows2.SetActive(false);
        }
        if (status == DroneStatus.TO_SHELF)
        {
            arrows1.SetActive(false);
            arrows2.SetActive(true);
        }
        if (gameObjectPointer.transform.position == parkingPos)
        {
            arrows1.SetActive(false);
            arrows2.SetActive(false);
        }
    }

    public void DroneCollideRender(bool collided)
    {
        ring = this.gameObjectPointer.transform.Find("protectionSphere").gameObject;
        if (collided == true)
        {
            ring.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().BlueBubble;
        }
        else
        {
            ring.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().NoBubble;
        }
    }

    /*
    public void CollideEffect()
    {
        shatter = this.gameObjectPointer.GetComponent<DroneProperties>().shatteredDrone;
        // Debug.Log("id" + this.droneId);
        // Debug.Log("status" + this.status);
        // Debug.Log("Collide Effect!");
        Utility.DeleteChild(this.gameObjectPointer, "Line");
        GameObject shatterObject = Object.Instantiate(shatter, this.gameObjectPointer.transform.position, this.gameObjectPointer.transform.rotation);
        shatterObject.tag = "shatter";
    }*/


    public float CalAveTime(Vector3[] shelf, float deltaTime)
    {
        // calculate average round trip time for the current drone
        float numFrame = 0;

        foreach (Vector3 shelfGrid in shelf)
        {
            float curDist = 2 * Utility.CalDistance(this.parkingPos, shelfGrid);
            numFrame += curDist / SPEED;
        }

        return numFrame * deltaTime / shelf.Length;
    }

    public void TriggerCollision(Collider other)
    {
        //Debug.Log("Trigger for drone " + droneId);

        try
        {
            GameObject droneB_gameObject = GameObject.Find(other.gameObject.name);
            DroneProperties droneB_droneProperties = droneB_gameObject.GetComponent<DroneProperties>();
            Drone droneB = droneB_droneProperties.classPointer;

            if (!isCollided &&  droneB.droneId == collionDroneId)
            {
                /*
                GameObject droneA_textHelperChild = this.gameObjectPointer.transform.Find("Text Helper").gameObject;
                TextMeshPro droneA_textHelper = droneA_textHelperChild.GetComponent<TextMeshPro>();
                droneA_textHelper.color = Color.red;
                //Debug.LogFormat("Collision for {0} and {1}", droneId, droneB.droneId);
                */
                TrafficControl.worldobject.GetComponent<TrafficControl>().userErrorColliders += 0.5f;
                isCollided = true;
                //droneB.isCollided = true;

                Debug.LogFormat("===== Drone {0}, Drone {1} | COLLISION ", this.droneId, droneB.droneId);
   
            }
        }
        catch (NullReferenceException)
        {
            Debug.LogFormat("Unable to find {0}", other.name);
        }

    }

}