#define IS_USER_STUDY

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using TMPro;


using UnityEngine.SceneManagement;


public class TrafficControl : MonoBehaviour
{
    
    // Flightpath & Collision options
    [Header("Flight Plan Provided")]
    public bool FlightPathProvided = true;

    [Header("Collision Logic")]
    public bool CollisionLogic_CustomBound = true;
    public float CollisionLogic_CustomBound_Dist = 0.5f;
    public bool CollisionLogic_Time = false;
    public bool CollisionLogic_Colliders = false;
    public bool CollisionLogic_Original = false;
    public bool Collision_DuringTakeoff = false;

    [Header("UI Features")]
    public bool UI_EnableArrows = true;
    public bool UI_EnableLineRender = true;
    public bool UI_EnableWarning = true;
    public float UI_Warning_Bound = 6.0f;

    [Header("Interaction Features")]
    public float Interact_SafeTime = 100.0f;
    public bool Interact_Pause = false;
    public bool Interact_GreenBubble = false;
    public bool Interact_FlyUp = false;
    public bool Interact_FlyUpDiagonal = true;


    [Header("Collision Logic")]
    public bool OnCollision_Disappear = true;
    public bool OnCollision_RedBubble = false;


    [Header("Debug Helpers")]
    public bool FlightDebugTrip = false;
    public bool FlightDebugCol = false;
    public bool FlightPlanDebug = false;
    public bool TextDebug = false;

    [Header("Prefab Variables")]
    public GameObject droneBaseObject;
    public GameObject eventBaseObject;
    public static GameObject worldobject;

    [Header("Materials")]
    public Material RedBubble;
    public Material GreenBubble;
    public Material NoBubble;
    public Material NoEvent;
    public Material BlueEvent;

    [Header("User Study Variables")]
    public int EXIT_TIME = 180;
    public float timeCounter = 0;
    public int MAX_SEED;
    public static int numDrones = 10;
    float EVENT_INTERVAL = Utility.EVENT_INTERVALS[numDrones];


    // Drone and Event Dictionaries 
    public static Dictionary<int, Drone> dronesDict = new Dictionary<int, Drone>();
    public static Dictionary<int, Event> eventsDict = new Dictionary<int, Event>();
    public static Dictionary<int, float> eventColTimeDict = new Dictionary<int, float>();

    public OrderedSet<int> waitingEventsId = new OrderedSet<int>();
    public Queue<CsvRow> waitingEventsID_Flightplan = new Queue<CsvRow>();
    public HashSet<int> ongoingEventsId = new HashSet<int>();

    public OrderedSet<int> availableDronesId = new OrderedSet<int>();
    public HashSet<int> workingDronesId = new HashSet<int>();

    public static Vector3[] shelves = Utility.shelves;
    public static Vector3[] parkinglot = Utility.parking;


    // User Data variables
    public int systemError = 0;
    public int userError = 0;
    public float userErrorColliders = 0;
    private float eventTimer = 0;
    private int successEventCounter = 0;
    private int totalEventCounter = 0;
    private int flyingDroneCount = 0;
    private int each_trip_counter = 0;
    private float minuteCounter = 0; // time elapsed in that minute
    private int currMinCollisionCounter = 0; // number of collisions in the current minute

    // CSV flightpath variables
    public static string csv_filename = "Assets/Log/flightplan.csv";
    public static StreamReader csv_reader = new StreamReader(csv_filename);
    public static List<CsvRow> flightPlan = new List<CsvRow>();
    public static int flightPlanIndex = 0;

    // Functional Variables
    private float AVE_TIME;
    private System.Random rnd;

    /// <summary>
    /// Read attached CSV into flightPlan
    /// </summary>
    public void ReadCSV()
    {
        string header = csv_reader.ReadLine();

        while (!csv_reader.EndOfStream)
        {
            string line = csv_reader.ReadLine();
            string[] values = line.Split(',');

            //Debug.Log(line);

            if (!String.IsNullOrEmpty(line))
            {
                //Debug.Log(line);
                CsvRow csvRow = new CsvRow(values);
                flightPlan.Add(csvRow);
            }

        }
    }

    /// <summary>
    ///  Helper function to Debug Event Assignment
    /// </summary>
    /// <param name="availableDrone"></param>
    public void printEvents(Drone availableDrone)
    {
        int tempStartingIndex = Array.IndexOf(Utility.parking, availableDrone.parkingPos);
        int tempTeleportIndex = Array.IndexOf(Utility.parking, availableDrone.spawnPos);
        int tempCurrEventNo = availableDrone.eventNo;
        int tempNextEvent = availableDrone.nextEvent;
        int tempCurrEventID = availableDrone.eventId;

        Debug.LogFormat("{0} : Drone {1} starting from {2} fly to {3} Spawning at {4} Next event {5}", tempCurrEventNo, availableDrone.droneId, tempStartingIndex, tempCurrEventID, tempTeleportIndex, tempNextEvent);
    }

    /// <summary>
    /// Generate random event
    /// </summary>
    /// <returns></returns>
    public int GenRandEvent()
    {
        int idx = -1;
        while (idx == -1)
        {
            idx = rnd.Next(shelves.Length);
            idx = waitingEventsId.Contains(idx) ? -1 : idx;
        }

        return idx;
    }

    /// <summary>
    /// Initilize all drones with position = id
    /// </summary>
    /// <param name="num"></param>
    public void initDrones(int num)
    {
        for (int i = 0; i < num; i++)
        {
            Drone newDrone = new Drone(i, parkinglot[i]);
            newDrone.EnableArrows = this.UI_EnableArrows;
            newDrone.gameObjectPointer.GetComponent<DroneProperties>().EnableLineRender = this.UI_EnableLineRender;
            dronesDict.Add(i, newDrone);
            availableDronesId.Add(i);
        }
    }

    /// <summary>
    /// Initilize all drones with their positions given in flightplan.csv
    /// </summary>
    /// <param name="num"></param>
    public void initDronesWithPath(int num)
    {
        for (int i = 0; i < num; i++)
        {
            int droneInitIndex = flightPlan[i].startingLaunchpadIndexX + flightPlan[i].startingLaunchpadIndexY * 10;
            Drone newDrone = new Drone(i, parkinglot[droneInitIndex]);
            newDrone.EnableArrows = this.UI_EnableArrows;
            newDrone.gameObjectPointer.GetComponent<DroneProperties>().EnableLineRender = this.UI_EnableLineRender;
            dronesDict.Add(i, newDrone);
            availableDronesId.Add(i);
        }
    }

    /// <summary>
    /// Initilize all events
    /// </summary>
    /// <param name="num"></param>
    public void initEvent(int num)
    {
        for (int i = 0; i < num; i++)
        {
            Event newEvent = new Event(i, shelves[i]);
            eventsDict.Add(i, newEvent);
        }
    }

    /// <summary>
    /// Check if the distance between line 1 given by two points and line 2 given by another set of points is less than some bound
    /// </summary>
    /// <param name="p1"></param> Point of line 1
    /// <param name="p2"></param> Another point of line 1
    /// <param name="p3"></param> Point of line 2
    /// <param name="p4"></param> Another point of line 2
    /// <returns></returns>
    public bool IsWithinCollisionBound(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, double bound)
    {
        Vector3 v1, v2, w;
        v1 = p2 - p1;
        v2 = p4 - p3;
        w = p4 - p1;

        Vector4 v1p, v2p, wp, identity;
        v1p = new Vector4(v1.x, v1.y, v1.z, 0);
        v2p = new Vector4(v2.x, v2.y, v2.z, 0);
        wp = new Vector4(w.x, w.y, w.z, 0);
        identity = new Vector4(0, 0, 0, 1);

        Matrix4x4 matrix_denominator = new Matrix4x4(v1p, v2p, wp, identity);
        double det = matrix_denominator.determinant;

        double nominator = Vector3.Cross(v1, v2).magnitude;

        double dist = det / nominator;

        if (dist < bound)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Update collision and UI logic upon collision of drone A and drone B
    /// </summary>
    /// <param name="droneA"></param>
    /// <param name="droneB"></param>
    public void UpdatedCollisionHelper(Drone droneA, Drone droneB)
    {
        // Increase collision count
        userError++;


        // Updates On Collision UI elements
        if (OnCollision_Disappear)
        {
            droneA.gameObjectPointer.SetActive(false);
            droneB.gameObjectPointer.SetActive(false);
            droneA.EnableArrows = false;
            droneB.EnableArrows = false;
        }
        if (OnCollision_RedBubble)
        {
            droneA.interactionBubble.GetComponent<MeshRenderer>().material = RedBubble;
            droneB.interactionBubble.GetComponent<MeshRenderer>().material = RedBubble;
        }

        // Update drones collision status
        droneA.isCollided = true;
        droneB.isCollided = true;

        // Debug Text Helper
        if (TextDebug)
        {
            GameObject droneA_textHelperChild = droneA.gameObjectPointer.transform.Find("Text Helper").gameObject;
            TextMeshPro droneA_textHelper = droneA_textHelperChild.GetComponent<TextMeshPro>();
            droneA_textHelper.color = Color.red;

            GameObject droneB_textHelperChild = droneB.gameObjectPointer.transform.Find("Text Helper").gameObject;
            TextMeshPro droneB_textHelper = droneB_textHelperChild.GetComponent<TextMeshPro>();
            droneB_textHelper.color = Color.red;
        }

        // Debug Flight helper
        if (FlightDebugCol)
        {
            Debug.LogFormat("===== Drone {0}, Drone {1} | COLLISION  at POS {2}, {3}  | Status {4}, {5} =====", droneB.droneId, droneB.droneId, droneA.curPos.ToString("F2"), droneB.curPos, ToString(), droneA.status, droneB.status);
        }


    }

    /// <summary>
    /// Gamelogic to react to user clicking on a drone. Function is called from the interaction scripts.
    /// </summary>
    /// <param name="drone"></param>
    public void OnClick(GameObject droneGameObject)
    {
        Drone drone = droneGameObject.GetComponent<DroneProperties>().classPointer;

        if (Interact_Pause)
        {
            drone.Interact_Pause();
        }
        if (Interact_GreenBubble)
        {
            drone.Interact_GreenBubble();
        }
        if (Interact_FlyUp)
        {
            drone.Interact_FlyUp();
        }
        if (Interact_FlyUpDiagonal)
        {
            drone.Interact_FlyUpDiagonal();
        }
    }

    // Use this for initialization
    void Start()
    {

        // Read flightpath CSV
        ReadCSV();


        // Populate waitingEventsID_flightplan
        for(int i = 0; i < flightPlan.Count; i++)
        {
            CsvRow tempEvent = flightPlan[i];
            waitingEventsID_Flightplan.Enqueue(tempEvent);
        }

        // Not sure what this done
        AVE_TIME = Utility.AVGTIME;

        // Assign global variables
        worldobject = this.gameObject;
        
        // Initialize Drones based on CSV or ID's
        if (FlightPathProvided)
        {
            initDronesWithPath(numDrones);
        }
        else
        {

            initDrones(numDrones);
        }

        // Initialize Events
        initEvent(shelves.Length);

    }

    // FixedUpdate is called once per frame
    void FixedUpdate()
    {

        /// <summary>
        /// Initilize new event randomly every EVENT_INTERVAL.
        /// We pre-populate all events if we have a CSV so this function is not needed then. 
        /// </summary>
        if (eventTimer > EVENT_INTERVAL & !FlightPathProvided)
        {
            // Reset eventTimer
            eventTimer = 0;

            /// <summary>
            /// Check that there are more drones than events.
            /// </summary>
            if ((waitingEventsId.Count + ongoingEventsId.Count < shelves.Length - 1) || (waitingEventsID_Flightplan.Count + ongoingEventsId.Count < shelves.Length - 1))
            {
                    int newIdx = GenRandEvent();
                    waitingEventsId.Add(newIdx);
            }
        }

        /// <summary>
        /// Assign avaliable event to avaliable drones
        /// If we have a flightplan these pairs are pre-planned
        /// If we don't have a flightplan these pairs are random or in order
        /// </summary>
        if (availableDronesId.Count > 0 && (waitingEventsId.Count > 0 || waitingEventsID_Flightplan.Count > 0))
        {
            /// <summary>
            /// Assign drone-event pairs based on pre-planned flightplan
            /// </summary>
            if (FlightPathProvided)
            {
                try
                {
                    /// <summary>
                    /// Obtain all information about next event
                    /// </summary>
                    CsvRow e = waitingEventsID_Flightplan.Peek();
                    int eventId = e.eventID;
                    int droneId = e.droneID;
                    int startingLaunchpadId = e.startingLaunchpadPos;
                    int shelfId = e.sheldIndexPos;
                    int nextEventId = e.followedBy;
                    int teleportId = e.teleportToLaunchpadIndexPos;
                    int collisionDroneId = e.collidesWithNum;
                    float collidesAtTime = e.collidesAtTime;
                    bool droneFound = false;

                    /// <summary>
                    /// Find drone corresponding to event.
                    /// This logic is implemented for all pairs after the initial numDrones events
                    /// </summary>
                    foreach (Drone availableDrone in dronesDict.Values)
                    {
                        // Get current drone id.
                        int d = availableDrone.droneId;

                        /// <summary>
                        /// Assign correct Drone to current event
                        /// Check 1: Ensure drone - event pair is correct
                        /// Check 2: Ensure drone is parked and available
                        /// </summary>
                        if (availableDrone.nextEvent == totalEventCounter && availableDronesId.Contains(d))
                        {
                            // Update Drone target event
                            availableDrone.eventId = shelfId;
                            availableDrone.AddEvent(eventsDict[shelfId]);
                            availableDrone.eventNo = totalEventCounter;

                            // Update Drone next event
                            availableDrone.nextEvent = nextEventId;

                            // Update Drone collision information
                            availableDrone.collionDroneId = collisionDroneId;
                            availableDrone.collidesAtTime = collidesAtTime;

                            // Update Drone next spawn positions
                            if (teleportId > 0)
                                availableDrone.spawnPos = Utility.parking[teleportId];

                            // Update global events and drone dictionaries
                            availableDronesId.Remove(d);
                            workingDronesId.Add(d);
                            waitingEventsID_Flightplan.Dequeue();
                            ongoingEventsId.Add(eventId);

                            // Colour Event
                            eventsDict[shelfId].markEvent(BlueEvent);

                            // Update global event counter
                            totalEventCounter++;

                            // End search for correct drone-event pair
                            droneFound = true;

                            // Debug Helper
                            if (FlightPlanDebug)
                            {
                                Debug.LogFormat("Assigning event {0} to drone {1}", e.eventID, availableDrone.droneId);
                            }
                        }

                        /// <summary>
                        /// End for loop if drone is found
                        /// </summary>
                        if (droneFound)
                        {
                            break;
                        }
                    }

                    /// <summary>
                    /// Find drone corresponding to event.
                    /// This logic is implemented for first numDrones events
                    /// </summary>
                    if (!droneFound && totalEventCounter < numDrones)
                    {
                        // Get correct drone
                        int d = availableDronesId.Next();
                        Drone availableDrone = dronesDict[d];

                        // Update Drone target event
                        // ?availableDrone.eventId = shelfId;
                        availableDrone.AddEvent(eventsDict[shelfId]);
                        availableDrone.eventNo = totalEventCounter;

                        // Update Drone next event
                        availableDrone.nextEvent = nextEventId;

                        // Update Drone collision information
                        availableDrone.collionDroneId = collisionDroneId;
                        availableDrone.collidesAtTime = collidesAtTime;

                        // Update Drone next spawn positions
                        availableDrone.spawnPos = Utility.parking[teleportId];

                        // Colour Event
                        eventsDict[shelfId].markEvent(BlueEvent);

                        // Update global events and drone dictionaries
                        availableDronesId.Remove(d);
                        workingDronesId.Add(d);
                        waitingEventsID_Flightplan.Dequeue();
                        ongoingEventsId.Add(eventId);

                        // Update global event counter
                        totalEventCounter++;

                        // Debug Helper
                        if (FlightPlanDebug)
                        {
                            printEvents(availableDrone);
                        }

                    }

                }
                catch (ArgumentOutOfRangeException err)
                {
                    Debug.Log("### 2.INVALID EVENT." + err);
                }

            }

            /// <summary>
            /// 
            /// </summary>
            else
            {
                int e = waitingEventsId.Next();
                int d = Utility.IS_RND_TAKEOFF ? availableDronesId.NextRnd() : availableDronesId.Next();

                Drone availableDrone = dronesDict[d];

                availableDrone.AddEvent(eventsDict[e]);
                availableDrone.eventNo = totalEventCounter;
                availableDronesId.Remove(d);
                workingDronesId.Add(d);
                waitingEventsId.Remove(e);
                ongoingEventsId.Add(e);
                totalEventCounter++;
            }

        }

        /// <summary>
        /// 1. Update direction of each drone
        /// 2. Check for collision based on decided logic:
        ///     a. CollisionLogic_CustomBound   : Trigger pre-planned collision when distance is less than CollisionLogic_CustomBound_Dist
        ///     b. CollisionLogic_Time          : Trigger pre-planned collision based on time.
        ///     c. CollisionLogic_Colliders     : Trigger collision based on Drone Colliders (Default)
        ///     d. CollisionLogic_Original      : Trigger collision based on original master students logic (We don't understand this completely)
        /// </summary>
        foreach (int i in workingDronesId)
        {
            // Get Drone
            Drone droneA = dronesDict[i];

            // Update direction
            droneA.direction = Vector3.Normalize(droneA.dstPos - droneA.curPos);


            /// <summary>
            /// Trigger collision based on OnTriggerEnter via Drone.cs and Colliders
            /// Default
            /// </summary>
            if (CollisionLogic_Colliders)
            {
                continue;
            }

            /// <summary>
            /// For droneA - droneB collision pair trigger collision when distance(droneA, droneB) < CollisionLogic_CustomBound_Dist
            /// </summary>
            if (CollisionLogic_CustomBound)
            {
                // If drone does not collide continue
                if (droneA.collionDroneId == -2)
                {
                    continue;
                }

                // Obtain pre-planned drone to collide with
                Drone droneB = dronesDict[droneA.collionDroneId];

                // Obtain distance(droneA, droneB)
                Vector3 delta = droneA.gameObjectPointer.transform.Find("pCube2").gameObject.transform.position - droneB.gameObjectPointer.transform.Find("pCube2").gameObject.transform.position;
                float dis = delta.magnitude;

                // Trigger Collision based on following logic
                // Check 1 : distance < CollisionLogic_CustomBound_Dist
                // Check 2 : Ensure drones have not collided
                // Check 3 : Ensure user has not interacted
                if ( (dis < CollisionLogic_CustomBound_Dist) & (!droneA.isCollided && !droneB.isCollided) & (droneA.safetyStatus == Drone.SafetyStatus.NOT_SAFE && droneB.safetyStatus == Drone.SafetyStatus.NOT_SAFE))
                {
                    UpdatedCollisionHelper(droneA, droneB);
                }
                else
                {
                    systemError++;
                }

            }

            /// <summary>
            /// For droneA - droneB collision pair trigger collision when Unity_Time > pre-planned collision time from the CSV
            /// </summary>
            if (CollisionLogic_Time)
            {
                // If drone does not collide continue
                if (droneA.collionDroneId == -2)
                {
                    continue;
                }

                // Obtain pre-planned drone to collide with
                Drone droneB = dronesDict[droneA.collionDroneId];

                // Obtain pre-planned collision time
                float collisionTime = droneA.collidesAtTime;
                
                // Trigger Collision based on following logic
                // Check 1 : Unity time has exceeded pre-planned collision time
                // Check 2 : Ensure drones have not collided
                // Check 3 : Ensure user has not interacted
                if ((timeCounter > collisionTime) & (!droneA.isCollided && !droneB.isCollided) & (droneA.safetyStatus == Drone.SafetyStatus.NOT_SAFE && droneB.safetyStatus == Drone.SafetyStatus.NOT_SAFE) )
                {
                    UpdatedCollisionHelper(droneA, droneB);

                }
            }

            /// <summary>
            /// Trigger collision based on original master students logic (We don't understand this completely)
            /// </summary>
            if (CollisionLogic_Original)
            {
                foreach (int j in workingDronesId)
                {
                    Drone droneB = dronesDict[j];

                    if (i == j)
                    {
                        continue;
                    }
                    //Debug.Log("Drone Collision Loop 2");

                    // ??
                    Vector3 delta = droneA.gameObjectPointer.transform.Find("pCube2").gameObject.transform.position - droneB.gameObjectPointer.transform.Find("pCube2").gameObject.transform.position;
                    float dis = delta.magnitude;

                    // ??
                    if (dis < Utility.INTERACT_DIM)
                    {
                        // ??
                        if (dis < Utility.BOUND_DIM)
                        {
                            //Check 2 : Ensure drones have not collided
                            if (!droneA.isCollided && !droneB.isCollided)
                            {
                                // Increase collision count
                                userError++;

                                // Debug Text Helper
                                if (TextDebug)
                                {
                                    GameObject droneA_textHelperChild = droneA.gameObjectPointer.transform.Find("Text Helper").gameObject;
                                    TextMeshPro droneA_textHelper = droneA_textHelperChild.GetComponent<TextMeshPro>();
                                    droneA_textHelper.color = Color.red;

                                    GameObject droneB_textHelperChild = droneB.gameObjectPointer.transform.Find("Text Helper").gameObject;
                                    TextMeshPro droneB_textHelper = droneB_textHelperChild.GetComponent<TextMeshPro>();
                                    droneB_textHelper.color = Color.red;
                                }

                                // Debug Flight helper
                                if (FlightDebugCol)
                                {
                                    Debug.LogFormat("===== Drone {0}, Drone {1} | COLLISION  at POS {2}, {3}  | Status {4}, {5} =====", i, droneB.droneId, droneA.curPos.ToString("F2"), droneB.curPos, ToString(), droneA.status, droneB.status);
                                }
                            }

                            droneA.isCollided = true;
                            droneB.isCollided = true;
                        }
                        else
                        {
                            systemError++;
                        }
                    }
                }
            }

        }


        /// <summary>
        /// If UI_EnableWarning check and display warning
        /// </summary>
        if (UI_EnableWarning)
        {
            foreach (Drone droneA in dronesDict.Values)
            {
                foreach (Drone droneB in dronesDict.Values)
                {
                    if (droneA.droneId == droneB.droneId)
                    {
                        continue;
                    }

                    // Obtain distance(droneA, droneB)
                    Vector3 delta = droneA.gameObjectPointer.transform.position - droneB.gameObjectPointer.transform.position;
                    float dis = delta.magnitude;
                    bool warning = (dis < UI_Warning_Bound & !droneA.isCollided & !droneB.isCollided);

                    droneA.UI_Warning(warning);
                    droneB.UI_Warning(warning);
                }
            }
        }

        /// <summary>
        /// Move every drone based on current status
        /// </summary>
        for (int i = 0; i < numDrones; i++)
        {
            // Obtain current drone and status
            Drone currDrone = dronesDict[i];
            Drone.DroneStatus status = currDrone.status;


            // If parked continue to next drone
            if (status == Drone.DroneStatus.PARKED)
            {
                continue;
            }

            // Move drone and obtain new status
            Drone.MoveStatus moveStatus = currDrone.Move();

            // If trip completed
            if (moveStatus == Drone.MoveStatus.END_TO_SHELF)
            {
                // Free event
                ongoingEventsId.Remove(currDrone.eventId);
                eventsDict[currDrone.eventId].markEvent(NoEvent);

                // Reset drone trip time
                currDrone.tripTime = 0;
                
                // Count as successfull trip if no collision
                if (!currDrone.isCollided)
                {
                    successEventCounter++;
                }
                
                // Debug helper
                if (FlightDebugTrip)
                {
                    Debug.LogFormat("Drone {0} | event {1} | COMPLETE, trip time: {2}", i, currDrone.eventId, currDrone.tripTime);
                }
            }

            // If drone respawned
            if (currDrone.status == Drone.DroneStatus.PARKED)
            {
                //Reset collision status
                currDrone.isCollided = false;
                workingDronesId.Remove(i);

                // Make drone avaliable
                if (currDrone.nextEvent != -1)
                {
                    availableDronesId.Add(i);
                }

                // Double check to remove correspinding event
                ongoingEventsId.Remove(currDrone.eventId);
            }

            // Increment drone trip time
            currDrone.tripTime += Time.fixedDeltaTime;
        }
      
        // Increment global time counters
        timeCounter += Time.fixedDeltaTime;
        minuteCounter += Time.fixedDeltaTime;
        eventTimer += Time.fixedDeltaTime;


#if IS_USER_STUDY
        if (true) // (SEED <= MAX_SEED)
        {
            if (timeCounter >= EXIT_TIME)
            {
                //Debug.Log(timeCounter);
                Debug.Log("====================End of a 3 minute user study=============================");
                Debug.LogFormat("User Error: {0}", userError);
                //Debug.Log(SEED);
                //ResetSim();
                //ReloadScene();

                QuitGame();
            }
        }
        else
        {
            Debug.Log("???????????????????????????");
            QuitGame();
        }
#endif
    }

    /*
    void UpdateSeed()
    {
        StreamWriter writer = new StreamWriter(seed_filename, false);
        int newSeed = SEED + 1;
        writer.WriteLine(newSeed.ToString());

        writer.Close();
    }


    void ReloadScene()
    {
        UpdateSeed();



        //seed_filename = "Assets/Scripts/SEED.txt";
        //reader = new StreamReader(seed_filename);
        //seed_string = reader.ReadLine();
        //SEED = strToInt(seed_string);


        // Drone and Event Dictionaries 
        dronesDict = new Dictionary<int, Drone>();
        eventsDict = new Dictionary<int, Event>();

        waitingEventsId = new OrderedSet<int>();
        ongoingEventsId = new HashSet<int>();

        availableDronesId = new OrderedSet<int>();
        workingDronesId = new HashSet<int>();

        shelves = Utility.shelves;
        parkinglot = Utility.parking;


        // User Data variables

        systemError = 0;
        userError = 0;
        timeCounter = 0;
        eventTimer = 0;
        successEventCounter = 0;
        totalEventCounter = 0;

        //rnd = new System.Random(SEED);

        //UnityEditor.PrefabUtility.ResetToPrefabState(this.gameObject);
        UnityEditor.PrefabUtility.RevertObjectOverride(gameObject, UnityEditor.InteractionMode.AutomatedAction);

        SceneManager.LoadScene("Assets/Scenes/SampleScene.unity");
    }


    void ResetSim()
    {
        float successRate = successEventCounter / numDrones;

        string filename = "Assets/Log/ONE-WAY/" + numDrones.ToString() + "/" + numDrones.ToString() + "_40Events.txt";
        string filename_success = "Assets/Log/ONE-WAY/" + numDrones.ToString() + "/" + numDrones.ToString() + "_40Events_success.txt";

        // write to log file
        StreamWriter fileWriter = new StreamWriter(filename, true);
        StreamWriter fileWriter_success = new StreamWriter(filename_success, true);

        fileWriter.WriteLine("CURRENT TIME: " + System.DateTime.Now);
        fileWriter.WriteLine("==========Basic Parameters==========");
        //fileWriter.WriteLine("Interface " + SceneManager.GetActiveScene().name);
        //fileWriter.WriteLine("FPS: " + 1 / Time.deltaTime);
        fileWriter.WriteLine("Drone speed: " + Utility.DRONE_SPEED);
        //fileWriter.WriteLine("Seed: " + SEED);
        fileWriter.WriteLine("Number of drones: " + numDrones);
        //fileWriter.WriteLine("Average time: " + AVE_TIME);
        fileWriter.WriteLine("Event interval: " + EVENT_INTERVAL);
        fileWriter.WriteLine("Number of events: " + totalEventCounter);

        fileWriter.WriteLine("==========User Study Data==========");
        //fileWriter.WriteLine("Seed: " + SEED);
        fileWriter.WriteLine("System error: " + systemError);
        fileWriter.WriteLine("User error: " + userError);

        if ((userError) == 18)
        {
            fileWriter_success.WriteLine("==========User Study Data==========");
            fileWriter_success.WriteLine("Number of drones: " + numDrones);
            //fileWriter_success.WriteLine("Seed: " + SEED);
            fileWriter_success.WriteLine("System error: " + systemError);
            fileWriter_success.WriteLine("User error: " + userError);
        }
        fileWriter.WriteLine("Number success events: " + successEventCounter);
        fileWriter.WriteLine(" ");

        fileWriter.Close();
        fileWriter_success.Close();

        //CSV file

        string[][] output = new string[rowData.Count][];

        for (int i = 0; i < output.Length; i++)
        {
            output[i] = rowData[i];
        }

        int length = output.GetLength(0);
        string delimiter = ",";

        StringBuilder sb = new StringBuilder();

        for (int index = 0; index < length; index++)
        {
            sb.AppendLine(string.Join(delimiter, output[index]));
        }

        string csv_filepath = "Assets/Log/ONE-WAY/" + numDrones.ToString() + "/" + numDrones.ToString() + "_40Events_" + seed_string + "seed_CSV.csv";

        StreamWriter outStream = System.IO.File.CreateText(csv_filepath);
        outStream.WriteLine(sb);
        outStream.Close();

    }
    */

    void OnApplicationQuit()
    {
        float successRate = successEventCounter / numDrones;

        string filename = "Assets/Log/" + SceneManager.GetActiveScene().name + "_" + numDrones + "test4.txt";
        // write to log file
        StreamWriter fileWriter = new StreamWriter(filename, true);

        fileWriter.WriteLine("CURRENT TIME: " + System.DateTime.Now);
        fileWriter.WriteLine("==========Basic Parameters==========");
        fileWriter.WriteLine("Interface " + SceneManager.GetActiveScene().name);
        fileWriter.WriteLine("FPS: " + 1 / Time.deltaTime);
        fileWriter.WriteLine("Drone speed: " + Utility.DRONE_SPEED);
        //fileWriter.WriteLine("Seed: " + SEED);
        fileWriter.WriteLine("Number of drones: " + numDrones);
        fileWriter.WriteLine("Average time: " + AVE_TIME);
        fileWriter.WriteLine("Event interval: " + EVENT_INTERVAL);
        fileWriter.WriteLine("Number of events: " + totalEventCounter);

        fileWriter.WriteLine("==========User Study Data==========");
        //fileWriter.WriteLine("Seed: " + SEED);
        fileWriter.WriteLine("System error: " + systemError);
        fileWriter.WriteLine("User error: " + userError / 2);
        fileWriter.WriteLine("Number success events: " + successEventCounter);
        fileWriter.WriteLine(" ");

        fileWriter.Close();


    }

    public void QuitGame()
    {
        // save any game data here
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
    }
}
