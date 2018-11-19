/**
 * Copyright (c) 2018 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 * Component created to create UI input field to change the frame rate for all cameras on a vehicle
 */


﻿using UnityEngine;
using UnityEngine.PostProcessing;
using System.Collections.Generic;

public class CameraSettingsManager : MonoBehaviour
{
    public GameObject Robot;
    private List<Camera> Cameras = new List<Camera>();

    void Awake()
    {
        AddUIElement();    
    }

    private void AddUIElement()
    {
        var frameRateTextbox = Robot.GetComponent<UserInterfaceTweakables>().AddTextbox("CamerasFPS", "Camera Publish Rate: ", "15");
        frameRateTextbox.onValueChanged.AddListener(value =>
        {
            try 
            {
                foreach(Camera cam in Cameras)
                {
                    cam.GetComponent<VideoToROS>().FPSChangeCallback(int.Parse(value));
                }
            }
            catch(System.Exception)
            {
                Debug.Log("Camera frame rate: Please input valid number!");
            }
        });
    }

    public void AddCamera(Camera cam)
    {
        Cameras.Add(cam);
    }


    
}
