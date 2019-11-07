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
    public bool safe = false;
    public float collidesAtTime;
    public bool isPaused;

    public bool EnableArrows = true;
    public enum SafetyStatus
    {
        NOT_SAFE = 0,
        TO_SAFE_ZONE = 1,
        SAFE = 2,
        TO_NONSAFE_ZONE = 3
    }
    public SafetyStatus safetyStatus; // 0: not safe, 1: flying to safe zone, 2: safe, 3: flying to non-safe zone
    public int safeFlightTime = 2;
    public int safetySpeedMultiplier = 3;
    public float safetyFlightStartTime;

    public int pauseCounter;
    public enum DroneStatus
    {
        PARKED = 0,
        TAKEOFF = 1,
        TO_SHELF = 2,
        DELAY = 3,
    }
    public DroneStatus status;
    public bool isCollided;
    public bool isWarning;

    // public static float SPEED = Utility.DRONE_SPEED;   // private static readonly float SPEED = 0.07f;

    // Pause Variables
    public static int pauseTime = 100;
    public static float clickTime = 0;
    public static float wrongClickTime = 0;
    public float safetyTime = 3.0f;

    // Arrow Variables
    public GameObject arrows1;
    public GameObject arrows2;
    public GameObject ring;
    public GameObject ring_2;
    public GameObject shatter;
    private readonly Vector3 arrowOffset1 = new Vector3(0f, 0f, 0f);
    private readonly Vector3 arrowOffset2 = new Vector3(0f, 0f, 0.1412f);

    public Drone(int droneId, Vector3 initPos)
    {
        this.droneId = droneId;
        this.isPaused = false;
        this.safetyStatus = SafetyStatus.NOT_SAFE;
        this.parkingPos = this.curPos = initPos;
        this.hoverPos = this.parkingPos + hoverShift;
        this.status = DroneStatus.PARKED;
        this.isWarning = false;
        this.isCollided = false;

        // create game object
        // Debug.Log("Created new drone with id: " + droneId);
        GameObject baseObject = TrafficControl.worldobject.GetComponent<TrafficControl>().droneBaseObject;
        gameObjectPointer = UnityEngine.Object.Instantiate(baseObject, initPos, Quaternion.identity);
        gameObjectPointer.GetComponent<DroneProperties>().classPointer = this;

        gameObjectPointer.name = string.Concat("Drone", droneId.ToString());
        gameObjectPointer.layer = 2;
        // gameObjectPointer.gameObject.tag = string.Concat("Drone", droneId.ToString());
        gameObjectPointer.transform.parent = TrafficControl.worldobject.transform;

        GameObject arrow1 = gameObjectPointer.GetComponent<DroneProperties>().Arrows;
        arrows1 = UnityEngine.Object.Instantiate(arrow1, initPos, Quaternion.Euler(0f, -90f, 0f));
        arrows1.transform.parent = TrafficControl.worldobject.transform;
        GameObject arrow2 = gameObjectPointer.GetComponent<DroneProperties>().Arrows;
        arrows2 = UnityEngine.Object.Instantiate(arrow2, initPos, Quaternion.Euler(0f, -270f, 0f));
        arrows2.transform.parent = TrafficControl.worldobject.transform;
        arrows1.SetActive(false);
        arrows2.SetActive(false);
        ring_2 = gameObjectPointer.transform.Find("warningSphere").gameObject;


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
        status = DroneStatus.TAKEOFF;
        dstPos = hoverPos;
        eventPos = e.pos;
        direction = Vector3.Normalize(dstPos - parkingPos);
        eventId = e.shelfId;
    }

    // new code start

    public void RaiseDrone()
    {
        safetyStatus = SafetyStatus.TO_SAFE_ZONE;
        //Debug.Log("Raising Drone");
    }

    public void LowerDrone()
    {
        safetyStatus = SafetyStatus.TO_NONSAFE_ZONE;
        //Debug.Log("Lowering Drone");
    }

    // new code end

    public void SetDronePause()
    {
        isPaused = true;
        pauseCounter = pauseTime;

        GameObject ring = this.gameObjectPointer.transform.Find("protectionSphere").gameObject;

        // Update user click info
        if (!isWarning)
            wrongClickTime++;
        clickTime++;
    }

    public void SetDroneRestart()
    {
        // Update user click info
        if (!isPaused)
            wrongClickTime++;
        clickTime++;

        isPaused = false;
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
        if (isPaused)
        {
            // if (pauseCounter-- == 0)
            // {
            //     isPaused = false;
            // }
            return flag;
        }
        // curPos = status == 0 ? gameObjectPointer.transform.position : gameObjectPointer.transform.position + direction * SPEED;
        // direction = Utility.shelves[eventId] - curPos;
        // direction = GameObject.Find("Event" + eventId.ToString()).transform.position - curPos;
        curPos = status == DroneStatus.PARKED ? curPos : curPos + direction * SPEED;
        // Debug.Log("Move drone " + droneId + " with dir: " + direction + " to pos: " + curPos);

        // New Code
        if (safetyStatus == SafetyStatus.TO_SAFE_ZONE)
        {
            if (curPos.y >= 30.0f)
            {
                safetyStatus = SafetyStatus.SAFE;
                safetyFlightStartTime = Time.time;
            }
            else
            {
                //Directionally Upward:
                curPos = curPos + new Vector3(0, SPEED * safetySpeedMultiplier, 0);

                // Straight Upward:
                //Vector3 orgPos = curPos - direction * SPEED;
                //curPos = orgPos + new Vector3(0, SPEED*safetySpeedMultiplier, 0);
            }
        }
        else if (safetyStatus == SafetyStatus.SAFE)
        {
            //Debug.Log("Safe zone");
            if (Time.time - safetyFlightStartTime > safetyTime)
            {
                safetyStatus = SafetyStatus.TO_NONSAFE_ZONE;
                safetyFlightStartTime = 0;
            }
            else
            {
                Vector3 orgPos = curPos - direction * SPEED;
                curPos = orgPos + new Vector3(direction.x * SPEED, 0, direction.z * SPEED);
            }

        }
        else if (safetyStatus == SafetyStatus.TO_NONSAFE_ZONE)
        {
            //Debug.Log("Non-safe zone");
            if (curPos.y < 30.0f)
            {
                safetyStatus = SafetyStatus.NOT_SAFE;
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
        // gameObjectPointer.transform.rotation = Quaternion.identity;
        // gameObjectPointer.transform.rotation = TrafficControl.worldobject.transform.rotation;

        if (EnableArrows)
        {
            DisplayArrow();
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
            ring.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().collideMaterial;
        }
        else
        {
            ring.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().landingMaterial;
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

    public void WarningRender(bool collided)
    {
        if (collided == true)
        {
            ring_2.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().warningMaterial;
        }
        else
        {
            ring_2.GetComponent<MeshRenderer>().material = this.gameObjectPointer.GetComponent<DroneProperties>().landingMaterial;
        }
    }

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