using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputComparisonDeviceInfoProvider
    {
        public InputComparisonDeviceInfo Capture()
        {
            return new InputComparisonDeviceInfo
            {
                deviceModel = SystemInfo.deviceModel,
                deviceName = SystemInfo.deviceName,
                operatingSystem = SystemInfo.operatingSystem,
                processorType = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                processorFrequencyMHz = SystemInfo.processorFrequency,
                systemMemoryMB = SystemInfo.systemMemorySize,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsMemoryMB = SystemInfo.graphicsMemorySize,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version
            };
        }
    }
}
