using VRCOSC.Game.Modules;
using VRCOSC.Game.Modules.Avatar;
using VRCOSC.Modules.AXHaptics.Axis;

namespace VRCOSC.Modules.AXHaptics
{
    [ModuleTitle("AXHaptics")]
    [ModuleDescription("Haptics Support for AXIS Trackers")]
    [ModuleAuthor("TahvoVR")]
    [ModulePrefab("VRCOSC-AXHapticsAvatarPrefab", "https://github.com/TahvoDev/AXHaptics/releases/download/latest/AXHaptics.unitypackage")]
    [ModuleGroup(ModuleType.General)]
    public partial class AXHapticsModule : AvatarModule
    {
        public static byte maxNodes = 17;
        public static readonly string bHapticsPrefix = "bHapticsOSC_";
        public static readonly string axHapticsPrefix = "VRCOSC/AXHaptics/";

        Dictionary<string, byte> nodeIndexByParameterName = new Dictionary<string, byte>();
        Dictionary<string, List<byte>> nodeIndicesByBHapticsName = new Dictionary<string, List<byte>>();
        static bool[] isNodeActiveByIndex = new bool[maxNodes];
        AxisUdpSocket? axisUdpSocket;

        #region Module Settings and Parameters
        private enum AXHapticsSetting
        {
            Intensity,
            Duration
        }
        private enum AXHapticsParameter
        {
            TouchedRightThigh,
            TouchedRightCalf,
            TouchedLeftThigh,
            TouchedLeftCalf,
            TouchedRightUpperArm,
            TouchedRightForeArm,
            TouchedLeftUpperArm,
            TouchedLeftForeArm,
            TouchedChest,
            TouchedRightFoot,
            TouchedLeftFoot,
            TouchedRightHand,
            TouchedLeftHand,
            TouchedRightShoulder,
            TouchedLeftShoulder,
            TouchedHead,
            TouchedHips,
            ProximityRightThigh,
            ProximityRightCalf,
            ProximityLeftThigh,
            ProximityLeftCalf,
            ProximityRightUpperArm,
            ProximityRightForeArm,
            ProximityLeftUpperArm,
            ProximityLeftForeArm,
            ProximityChest,
            ProximityRightFoot,
            ProximityLeftFoot,
            ProximityRightHand,
            ProximityLeftHand,
            ProximityRightShoulder,
            ProximityLeftShoulder,
            ProximityHead,
            ProximityHips,
            BHapticsVestFront,
            BHapticsVestBack,
            BHapticsArmRight,
            BHapticsArmLeft,
            BHapticsFootRight,
            BHapticsFootLeft,
            BHapticsHandRight,
            BHapticsHandLeft,
            BHapticsHead,
        }
        #endregion

        static void OnTrackerData(object? sender, AxisOutputData data)
        {
            // Keeps track of what nodes are connected - otherwise, trying to send a vibration to a node not connected will vibrate all trackers. 
            foreach (var nodeData in data.nodesData)
            {
                if (nodeData.NodeId < maxNodes)
                {
                    isNodeActiveByIndex[nodeData.NodeId] = nodeData.IsActive; 
                }
            }
        }

        class NodeInfo
        {
            public AXHapticsParameter ConstantParameter { get; }
            public AXHapticsParameter ProximityParameter { get; }
            public string DisplayName { get; }
            public List<string> BHapticsDevices { get; }

            public NodeInfo(AXHapticsParameter constantParameter, AXHapticsParameter proximityParameter, string displayName, List<string> bHapticsDevices)
            {
                ConstantParameter = constantParameter;
                ProximityParameter = proximityParameter;
                DisplayName = displayName;
                BHapticsDevices = bHapticsDevices;
            }
        }

        NodeInfo[] nodeInfoByIndex = 
        {
            new(AXHapticsParameter.TouchedRightThigh, AXHapticsParameter.ProximityRightThigh, "Right Thigh Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedRightCalf, AXHapticsParameter.ProximityRightCalf, "Right Calf Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedLeftThigh, AXHapticsParameter.ProximityLeftThigh, "Left Thigh Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedLeftCalf, AXHapticsParameter.ProximityLeftCalf, "Left Calf Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedRightUpperArm, AXHapticsParameter.ProximityRightUpperArm, "Right Upper Arm Haptic", new List<string>() { "Arm_Right" }),
            new(AXHapticsParameter.TouchedRightForeArm, AXHapticsParameter.ProximityRightForeArm, "Right Forearm Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedLeftUpperArm, AXHapticsParameter.ProximityLeftUpperArm, "Left Upper Arm Haptic", new List<string>() { "Arm_Left" }),
            new(AXHapticsParameter.TouchedLeftForeArm, AXHapticsParameter.ProximityLeftForeArm, "Left Forearm Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedChest, AXHapticsParameter.ProximityChest, "Chest Haptic", new List<string>() { "Vest_Front", "Vest_Back" }),
            new(AXHapticsParameter.TouchedRightFoot, AXHapticsParameter.ProximityRightFoot, "Right Foot Haptic", new List<string>() { "Foot_Right" }),
            new(AXHapticsParameter.TouchedLeftFoot, AXHapticsParameter.ProximityLeftFoot, "Left Foot Haptic", new List<string>() { "Foot_Left" }),
            new(AXHapticsParameter.TouchedRightHand, AXHapticsParameter.ProximityRightHand, "Right Hand Haptic", new List<string>() { "Hand_Right" }),
            new(AXHapticsParameter.TouchedLeftHand, AXHapticsParameter.ProximityLeftHand, "Left Hand Haptic", new List<string>() { "Hand_Left" }),
            new(AXHapticsParameter.TouchedRightShoulder, AXHapticsParameter.ProximityRightShoulder, "Right Shoulder Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedLeftShoulder, AXHapticsParameter.ProximityLeftShoulder, "Left Shoulder Haptic", new List<string>()),
            new(AXHapticsParameter.TouchedHead, AXHapticsParameter.ProximityHead, "Head Haptic", new List<string>() { "Head" }),
            new(AXHapticsParameter.TouchedHips, AXHapticsParameter.ProximityHips, "Hips Haptic", new List<string>() { "Vest_Front", "Vest_Back" })
        };

        protected override void CreateAttributes()
        {
            CreateSetting(AXHapticsSetting.Intensity, "Haptics Intensity", "Intensity of haptics vibration.", 1f, 0, 1);
            CreateSetting(AXHapticsSetting.Duration, "Haptics Duration", "Duration that haptics activate for each touch in seconds.", 1f, 0, 1);

            for (int i = 0; i < nodeInfoByIndex.Length; i++)
            {
                byte nodeIndex = (byte)i; 
                string constantParameterName =  $"{axHapticsPrefix}{Enum.GetName(nodeInfoByIndex[i].ConstantParameter) ?? string.Empty}"; 
                string proximityParameterName =  $"{axHapticsPrefix}{Enum.GetName(nodeInfoByIndex[i].ProximityParameter) ?? string.Empty}";

                CreateParameter<bool>(nodeInfoByIndex[i].ConstantParameter, ParameterMode.Read, constantParameterName, $"{nodeInfoByIndex[i].DisplayName} Touched", $"Constant Contact Receiver for Node {i}");
                CreateParameter<float>(nodeInfoByIndex[i].ProximityParameter, ParameterMode.Read, proximityParameterName, $"{nodeInfoByIndex[i].DisplayName} Proximity", $"Proximity Contact Receiver for Node {i}");

                // Assign which Axis node activates for corresponding touch and proximity parameters
                nodeIndexByParameterName.Add(constantParameterName, nodeIndex);
                nodeIndexByParameterName.Add(proximityParameterName, nodeIndex);

                // Assign which Axis nodes activate for listed bHaptics parameters 
                foreach (string bHapticsDevice in nodeInfoByIndex[i].BHapticsDevices)
                {
                    if (nodeIndicesByBHapticsName.ContainsKey(bHapticsDevice))
                    {
                        nodeIndicesByBHapticsName[bHapticsDevice].Add(nodeIndex);
                    }
                    else
                    {
                        nodeIndicesByBHapticsName.Add(bHapticsDevice, new List<byte> { nodeIndex });
                    }
                }      
            }

            CreateParameter<float>(AXHapticsParameter.BHapticsArmLeft, ParameterMode.Read, $"{bHapticsPrefix}Arm_Left", "bHaptics Arm Left", "Arm Left for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsArmRight, ParameterMode.Read, $"{bHapticsPrefix}Arm_Right", "bHaptics Arm Right", "Arm Right for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsFootLeft, ParameterMode.Read, $"{bHapticsPrefix}Foot_Left", "bHaptics Foot Left", "bHaptics Foot Left for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsFootRight, ParameterMode.Read, $"{bHapticsPrefix}Foot_Right", "bHaptics Foot Right", "Foot Right for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsHandLeft, ParameterMode.Read, $"{bHapticsPrefix}Hand_Left", "bHaptics Hand Left", "Hand Left for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsHandRight, ParameterMode.Read, $"{bHapticsPrefix}Hand_Right", "bHaptics Hand Right", "Hand Right for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsHead, ParameterMode.Read, $"{bHapticsPrefix}Head", "bHaptics Head", "Head for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsVestFront, ParameterMode.Read, $"{bHapticsPrefix}Vest_Front", "bHaptics Vest Front", "Vest Front for bHaptics Avatars");
            CreateParameter<float>(AXHapticsParameter.BHapticsVestBack, ParameterMode.Read, $"{bHapticsPrefix}Vest_Back", "bHaptics Vest Back", "Vest Back for bHaptics Avatars");
        }
        protected override void OnModuleStart()
        {
            isNodeActiveByIndex = new bool[maxNodes];
            axisUdpSocket = new AxisUdpSocket();
            axisUdpSocket.OnAxisData += OnTrackerData;
            axisUdpSocket.Start();
        }
        [ModuleUpdate(ModuleUpdateMode.Custom)]
        private void moduleUpdate()
        {
        }
        protected override void OnModuleStop()
        {
            axisUdpSocket?.Stop();
        }

        protected override void OnRegisteredParameterReceived(AvatarParameter parameter)
        {
            float hapticIntensity = GetSetting<float>(AXHapticsSetting.Intensity);
            float hapticDuration = GetSetting<float>(AXHapticsSetting.Duration);
            if (parameter.Name.StartsWith($"{axHapticsPrefix}Touched"))
            {
                if (parameter.ValueAs<bool>())
                {
                    nodeIndexByParameterName.TryGetValue(parameter.Name, out byte index);
                    if (isNodeActiveByIndex[index])
                    {
                        axisUdpSocket?.AxisRuntimeCommander.SetNodeVibration(index, hapticIntensity, hapticDuration);
                    }
                };
            }
            else if (parameter.Name.StartsWith($"{axHapticsPrefix}Proximity"))
            {
                float proximity = parameter.ValueAs<float>();

                // Prevents very weak haptics at far distance and adjusts on an exponential curve
                float nodeStrength = (0.25f + ((float)Math.Pow(proximity, 2)) * 0.75f) * hapticIntensity; 

                nodeIndexByParameterName.TryGetValue(parameter.Name, out byte index);
                if (isNodeActiveByIndex[index])
                {
                    axisUdpSocket?.AxisRuntimeCommander.SetNodeVibration(index, nodeStrength, 0.1f);
                }
            }
            else if (parameter.Name.StartsWith(bHapticsPrefix))
            {
                string bHapticsDevice = parameter.Name.Substring(bHapticsPrefix.Length);

                List<byte>? relevantAxisNodeIndices;
                if (nodeIndicesByBHapticsName.TryGetValue(bHapticsDevice, out relevantAxisNodeIndices))
                {
                    foreach (byte index in relevantAxisNodeIndices)
                    {
                        if (isNodeActiveByIndex[index])
                        {
                            axisUdpSocket?.AxisRuntimeCommander.SetNodeVibration(index, hapticIntensity, hapticDuration);
                        }
                    }
                }
            }
        }
    }
}