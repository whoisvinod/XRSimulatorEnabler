using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

[InitializeOnLoad]
static class XRSimulatorDevicePrompt
{
    const string k_SettingsAssetPath = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";
    const string k_SimulatorEnabledProperty = "m_AutomaticallyInstantiateSimulatorPrefab";
    const string k_SimulatorPrefabProperty = "m_SimulatorPrefab";
    const string k_XriPackageName = "com.unity.xr.interaction.toolkit";
    const string k_SimulatorSampleName = "XR Interaction Simulator";
    const string k_SimulatorPrefabName = "XR Interaction Simulator";

    static XRSimulatorDevicePrompt()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode ||
            IsHeadMountedDisplayConnected())
            return;

        if (!TryGetSimulatorEnabled(out var simulatorEnabled) || simulatorEnabled)
            return;

        if (!EditorUtility.DisplayDialog(
                "Alert",
                "No VR device is detected. Simulator is OFF. Turn it ON?",
                "Yes",
                "No"))
            return;

        EnableSimulator();
    }

    static bool IsHeadMountedDisplayConnected()
    {
        if (XRSettings.isDeviceActive)
            return true;

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, devices);
        return devices.Exists(device => device.isValid);
    }

    static bool TryGetSimulatorEnabled(out bool enabled)
    {
        enabled = false;
        var settingsAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(k_SettingsAssetPath);
        if (settingsAsset == null)
        {
            Debug.LogError($"Could not load XR Interaction Toolkit settings at '{k_SettingsAssetPath}'.");
            return false;
        }

        var settings = new SerializedObject(settingsAsset);
        var property = settings.FindProperty(k_SimulatorEnabledProperty);
        if (property == null)
        {
            Debug.LogError($"Could not find '{k_SimulatorEnabledProperty}' in the XR Interaction Toolkit settings.");
            return false;
        }

        enabled = property.boolValue;
        return true;
    }

    static void EnableSimulator()
    {
        var settingsAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(k_SettingsAssetPath);
        if (settingsAsset == null)
        {
            Debug.LogError($"Could not load XR Interaction Toolkit settings at '{k_SettingsAssetPath}'.");
            return;
        }

        var simulatorPrefab = FindOrImportSimulatorPrefab();
        if (simulatorPrefab == null)
            return;

        var settings = new SerializedObject(settingsAsset);
        var enabledProperty = settings.FindProperty(k_SimulatorEnabledProperty);
        var prefabProperty = settings.FindProperty(k_SimulatorPrefabProperty);
        if (enabledProperty == null || prefabProperty == null)
        {
            Debug.LogError("Could not find the simulator settings properties in the XR Interaction Toolkit settings.");
            return;
        }

        Undo.RecordObject(settingsAsset, "Enable XR Interaction Simulator");
        enabledProperty.boolValue = true;
        prefabProperty.objectReferenceValue = simulatorPrefab;
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(settingsAsset);
        AssetDatabase.SaveAssets();
    }

    static GameObject FindOrImportSimulatorPrefab()
    {
        var packageSamples = Sample.FindByPackage(k_XriPackageName, string.Empty);
        if (packageSamples == null)
        {
            Debug.LogError($"Could not find samples for the {k_XriPackageName} package.");
            return null;
        }

        var simulatorSample = default(Sample);
        var foundSimulatorSample = false;
        foreach (var sample in packageSamples)
        {
            if (sample.displayName == k_SimulatorSampleName)
            {
                simulatorSample = sample;
                foundSimulatorSample = true;
                break;
            }
        }

        if (!foundSimulatorSample)
        {
            Debug.LogError($"Could not find the {k_SimulatorSampleName} sample in the XR Interaction Toolkit package.");
            return null;
        }

        if (!simulatorSample.isImported)
        {
            if (!simulatorSample.Import(Sample.ImportOptions.HideImportWindow))
            {
                Debug.LogError($"Failed to import the {k_SimulatorSampleName} sample.");
                return null;
            }
        }

        foreach (var guid in AssetDatabase.FindAssets($"\"{k_SimulatorPrefabName}\""))
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null && prefab.TryGetComponent<XRInteractionSimulator>(out _))
                return prefab;
        }

        Debug.LogError($"Could not find the {k_SimulatorPrefabName} prefab after importing the XR Interaction Toolkit sample.");
        return null;
    }
}
