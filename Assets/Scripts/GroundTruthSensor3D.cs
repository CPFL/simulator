/**
 * Copyright (c) 2018 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GroundTruthSensor3D : MonoBehaviour, Comm.BridgeClient
{
    public string objects3DTopicName = "/simulator/ground_truth/3d_detections";
    public string autowareLidarDetectionTopicName = "/detection/lidar_objects";
    public float frequency = 10.0f;

    public ROSTargetEnvironment targetEnv;
    public GameObject lidarSensor;
    public RadarRangeTrigger lidarRangeTrigger;
    public GameObject boundingBox;
    private List<GameObject> boundingBoxes = new List<GameObject>();
    private List<GameObject> lidarBoundingBoxes = new List<GameObject>();

    private uint seqId;
    private uint objId;
    private float nextSend;
    private Comm.Bridge Bridge;
    Comm.Writer<Ros.Detection3DArray> DetectedObjectArrayWriter;
    private List<Ros.Detection3D> detectedObjects;
    private Dictionary<Collider, Ros.Detection3D> lidarDetectedColliders;
    private bool isEnabled = false;
    private bool isFirstEnabled = true;

    private bool isLidarPredictionEnabled = false;
    private List<Ros.Detection3D> lidarPredictedObjects;
    private List<Ros.Detection3D> lidarPredictedVisuals;
    private bool isVisualize = true;

    private void Awake()
    {
        AddUIElement();
        detectedObjects = new List<Ros.Detection3D>();
        lidarDetectedColliders = new Dictionary<Collider, Ros.Detection3D>();
        lidarPredictedObjects = new List<Ros.Detection3D>();
        lidarPredictedVisuals = new List<Ros.Detection3D>();
        lidarRangeTrigger.SetCallback(onLidarRangeTriggered);

        if (lidarSensor && lidarSensor.GetComponent<LidarSensor>())
        {
            lidarRangeTrigger.transform.localScale = new Vector3(
                lidarSensor.GetComponent<LidarSensor>().MaxDistance * 2,
                lidarSensor.GetComponent<LidarSensor>().MaxDistance * 2,
                lidarSensor.GetComponent<LidarSensor>().MaxDistance * 2
            );
        }
    }

    private void Start()
    {
        nextSend = Time.time + 1.0f / frequency;
    }

    private void Update()
    {
        if (isEnabled && lidarDetectedColliders != null)
        {
            detectedObjects = lidarDetectedColliders.Values.ToList();
            if (isVisualize)
            {
                Visualize(detectedObjects, boundingBoxes);
            }
            lidarDetectedColliders.Clear();
            objId = 0;

            PublishGroundTruth(detectedObjects);
        }

        if (isLidarPredictionEnabled && lidarSensor != null && lidarSensor.GetComponent<LidarSensor>().enabled && isVisualize)
        {
            Visualize(lidarPredictedVisuals, lidarBoundingBoxes);
        }
    }

    public void Enable(bool enabled)
    {
        isEnabled = enabled;
        objId = 0;

        if (isEnabled && isFirstEnabled)
        {
            isFirstEnabled = false;
            AgentSetup agentSetup = GetComponentInParent<AgentSetup>();
            if (agentSetup != null && agentSetup.NeedsBridge != null)
            {
                agentSetup.AddToNeedsBridge(this);
            }
        }

        if (detectedObjects != null)
        {
            detectedObjects.Clear();
        }

        if (lidarDetectedColliders != null)
        {
            lidarDetectedColliders.Clear();
        }
    }

    public void EnableLidarPrediction(bool enabled)
    {
        isLidarPredictionEnabled = enabled;

        if (isLidarPredictionEnabled && isFirstEnabled)
        {
            isFirstEnabled = false;
            AgentSetup agentSetup = GetComponentInParent<AgentSetup>();
            if (agentSetup != null && agentSetup.NeedsBridge != null)
            {
                agentSetup.AddToNeedsBridge(this);
            }
        }

        if (lidarPredictedVisuals != null)
        {
            lidarPredictedVisuals.Clear();
        }

        if (lidarPredictedObjects != null)
        {
            lidarPredictedObjects.Clear();
        }
    }

    public void OnBridgeAvailable(Comm.Bridge bridge)
    {
        Bridge = bridge;
        Bridge.OnConnected += () =>
        {
            if (targetEnv == ROSTargetEnvironment.AUTOWARE || targetEnv == ROSTargetEnvironment.APOLLO || targetEnv == ROSTargetEnvironment.LGSVL)
            {
                DetectedObjectArrayWriter = Bridge.AddWriter<Ros.Detection3DArray>(objects3DTopicName);
            }

            if (targetEnv == ROSTargetEnvironment.AUTOWARE)
            {
                Bridge.AddReader<Ros.DetectedObjectArray>(autowareLidarDetectionTopicName, msg =>
                {
                    if (!isLidarPredictionEnabled || lidarPredictedObjects == null)
                    {
                        return;
                    }

                    foreach (Ros.DetectedObject obj in msg.objects)
                    {
                        Ros.Detection3D obj_converted = new Ros.Detection3D()
                        {
                            header = new Ros.Header()
                            {
                                stamp = new Ros.Time()
                                {
                                    secs = obj.header.stamp.secs,
                                    nsecs = obj.header.stamp.nsecs,
                                },
                                seq = obj.header.seq,
                                frame_id = obj.header.frame_id,
                            },
                            id = obj.id,
                            label = obj.label,
                            score = obj.score,
                            bbox = new Ros.BoundingBox3D()
                            {
                                position = new Ros.Pose()
                                {
                                    position = new Ros.Point()
                                    {
                                        x = obj.pose.position.x,
                                        y = obj.pose.position.y,
                                        z = obj.pose.position.z,
                                    },
                                    orientation = new Ros.Quaternion()
                                    {
                                        x = obj.pose.orientation.x,
                                        y = obj.pose.orientation.y,
                                        z = obj.pose.orientation.z,
                                        w = obj.pose.orientation.w,
                                    },
                                },
                                size = new Ros.Vector3()
                                {
                                    x = obj.dimensions.x,
                                    y = obj.dimensions.y,
                                    z = obj.dimensions.z,
                                },
                            },
                            velocity = new Ros.Twist()
                            {
                                linear = new Ros.Vector3()
                                {
                                    x = obj.velocity.linear.x,
                                    y = 0,
                                    z = 0,
                                },
                                angular = new Ros.Vector3()
                                {
                                    x = 0,
                                    y = 0,
                                    z = obj.velocity.angular.z,
                                },
                            },
                        };
                        lidarPredictedObjects.Add(obj_converted);
                    }
                    lidarPredictedVisuals = lidarPredictedObjects.ToList();
                    lidarPredictedObjects.Clear();
                });
            }
        };
    }

    // Get linear velocity from collider
    System.Func<Collider, Vector3> GetLinVel = ((col) =>
    {
        var trafAiMtr = col.GetComponentInParent<TrafAIMotor>();
        if (trafAiMtr != null)
        {
            return trafAiMtr.currentVelocity;
        }
        else
        {
            return col.attachedRigidbody == null ? Vector3.zero : col.attachedRigidbody.velocity;
        }
    });

    // Get angular velocity from collider
    System.Func<Collider, Vector3> GetAngVel = ((col) =>
    {
        return col.attachedRigidbody == null ? Vector3.zero : col.attachedRigidbody.angularVelocity;
    });

    private void onLidarRangeTriggered(Collider detect)
    {
        if (!isEnabled || lidarDetectedColliders == null)
        {
            return;
        }

        if (detect.isTrigger)
        {
            return;
        }

        if (!lidarDetectedColliders.ContainsKey(detect))
        {
            string label = "";
            Vector3 size = Vector3.zero;
            float y_offset = 0.0f;
            if (detect.gameObject.layer == 28 || detect.gameObject.layer == 14 || detect.gameObject.layer == 19)
            {
                // if GroundTruth (Player), NPC, or NPC Static layer
                label = "car";
                if (detect.GetType() == typeof(BoxCollider))
                {
                    size.x = ((BoxCollider)detect).size.z;
                    size.y = ((BoxCollider)detect).size.x;
                    size.z = ((BoxCollider)detect).size.y;
                    y_offset = ((BoxCollider)detect).center.y;
                }
            }
            else if (detect.gameObject.layer == 18)
            {
                // if Pedestrian layer
                label = "pedestrian";
                if (detect.GetType() == typeof(CapsuleCollider))
                {
                    size.x = ((CapsuleCollider)detect).radius;
                    size.y = ((CapsuleCollider)detect).radius;
                    size.z = ((CapsuleCollider)detect).height;
                    y_offset = ((CapsuleCollider)detect).center.y;
                }
            }

            if (label == "" || size.magnitude == 0)
            {
                return;
            }

            // Local position of object in Lidar local space
            Vector3 relPos = lidarSensor.transform.InverseTransformPoint(detect.transform.position);
            // Lift up position to the ground
            relPos.y += y_offset;
            // Convert from (Right/Up/Forward) to (Forward/Left/Up)
            relPos.Set(relPos.z, -relPos.x, relPos.y);

            // Relative rotation of objects wrt Lidar frame
            Quaternion relRot = Quaternion.Inverse(lidarSensor.transform.rotation) * detect.transform.rotation;
            // Convert from (Right/Up/Forward) to (Forward/Left/Up)
            relRot.Set(relRot.z, -relRot.x, relRot.y, relRot.w);

            // Linear velocity in forward direction of objects, in meters/sec
            float linear_vel = Vector3.Dot(GetLinVel(detect), detect.transform.forward);
            // Angular velocity around up axis of objects, in radians/sec
            float angular_vel = -(GetAngVel(detect)).y;

            lidarDetectedColliders.Add(detect, new Ros.Detection3D()
            {
                header = new Ros.Header()
                {
                    stamp = Ros.Time.Now(),
                    seq = seqId++,
                    frame_id = "velodyne",
                },
                id = objId++,
                label = label,
                score = 1.0f,
                bbox = new Ros.BoundingBox3D()
                {
                    position = new Ros.Pose()
                    {
                        position = new Ros.Point()
                        {
                            x = relPos.x,
                            y = relPos.y,
                            z = relPos.z,
                        },
                        orientation = new Ros.Quaternion()
                        {
                            x = relRot.x,
                            y = relRot.y,
                            z = relRot.z,
                            w = relRot.w,
                        },
                    },
                    size = new Ros.Vector3()
                    {
                        x = size.x,
                        y = size.y,
                        z = size.z,
                    },
                },
                velocity = new Ros.Twist()
                {
                    linear = new Ros.Vector3()
                    {
                        x = linear_vel,
                        y = 0,
                        z = 0,
                    },
                    angular = new Ros.Vector3()
                    {
                        x = 0,
                        y = 0,
                        z = angular_vel,
                    },
                },
            });
        }
    }

    private void PublishGroundTruth(List<Ros.Detection3D> detectedObjects)
    {
        if (Bridge == null || Bridge.Status != Comm.BridgeStatus.Connected)
        {
            return;
        }

        if (Time.time < nextSend)
        {
            return;
        }

        if (detectedObjects == null)
        {
            return;
        }

        if (targetEnv == ROSTargetEnvironment.AUTOWARE || targetEnv == ROSTargetEnvironment.APOLLO || targetEnv == ROSTargetEnvironment.LGSVL)
        {
            var detectedObjectArrayMsg = new Ros.Detection3DArray()
            {
                header = new Ros.Header()
                {
                    stamp = Ros.Time.Now(),
                },
                detections = detectedObjects,
            };
            DetectedObjectArrayWriter.Publish(detectedObjectArrayMsg);

            nextSend = Time.time + 1.0f / frequency;
        }
    }

    private void Visualize(List<Ros.Detection3D> objects, List<GameObject> boundingBoxes)
    {
        if (boundingBox == null || objects == null)
        {
            return;
        }

        foreach (GameObject bbox in boundingBoxes)
        {
            Destroy(bbox);
        }
        boundingBoxes.Clear();

        foreach (Ros.Detection3D obj in objects)
        {
            GameObject bbox = Instantiate(boundingBox, transform);

            Vector3 relPos = new Vector3
            (
                (float)obj.bbox.position.position.x,
                (float)obj.bbox.position.position.y,
                (float)obj.bbox.position.position.z
            );

            relPos.Set(-relPos.y, relPos.z, relPos.x);
            Vector3 worldPos = lidarSensor.transform.TransformPoint(relPos);
            bbox.transform.position = worldPos;

            Quaternion relRot = new Quaternion
            (
                (float)obj.bbox.position.orientation.x,
                (float)obj.bbox.position.orientation.y,
                (float)obj.bbox.position.orientation.z,
                (float)obj.bbox.position.orientation.w
            );

            relRot.Set(-relRot.y, relRot.z, relRot.x, relRot.w);
            Quaternion worldRot = lidarSensor.transform.rotation * relRot;
            bbox.transform.rotation = worldRot;

            bbox.transform.localScale = new Vector3
            (
                (float)obj.bbox.size.y * 1.1f,
                (float)obj.bbox.size.z * 1.1f,
                (float)obj.bbox.size.x * 1.1f
            );

            Renderer rend = bbox.GetComponent<Renderer>();
            switch (obj.label)
            {
                case "car":
                    rend.material.SetColor("_Color", new Color(0, 1, 0, 0.3f));  // Color.green
                    break;
                case "pedestrian":
                    rend.material.SetColor("_Color", new Color(1, 0.92f, 0.016f, 0.3f));  // Color.yellow
                    break;
                case "bicycle":
                    rend.material.SetColor("_Color", new Color(0, 1, 1, 0.3f));  // Color.cyan
                    break;
                default:
                    rend.material.SetColor("_Color", new Color(1, 0, 1, 0.3f));  // Color.magenta
                    break;
            }

            bbox.SetActive(true);
            Destroy(bbox, Time.deltaTime);
            boundingBoxes.Add(bbox);
        }
    }

    public void EnableVisualize(bool enable)
    {
        isVisualize = enable;
    }

    private void AddUIElement()
    {
        if (targetEnv == ROSTargetEnvironment.AUTOWARE || targetEnv == ROSTargetEnvironment.APOLLO || targetEnv == ROSTargetEnvironment.LGSVL)
        {
            var groundTruth3DCheckbox = transform.parent.gameObject.GetComponent<UserInterfaceTweakables>().AddCheckbox("ToggleGroundTruth3D", "Enable Ground Truth 3D:", isEnabled);
            groundTruth3DCheckbox.onValueChanged.AddListener(x => Enable(x));
        }

        if (targetEnv == ROSTargetEnvironment.AUTOWARE)
        {
            var lidarPredictionCheckbox = transform.parent.gameObject.GetComponent<UserInterfaceTweakables>().AddCheckbox("ToggleLidarPrediction", "Enable Lidar Prediction:", isLidarPredictionEnabled);
            lidarPredictionCheckbox.onValueChanged.AddListener(x => EnableLidarPrediction(x));
        }
    }
}
