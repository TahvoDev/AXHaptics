using System.Drawing;
using System.Linq;

namespace VRCOSC.Modules.AXHaptics.Axis
{
    public enum CommandType
    {
        MainHeader,
        ImuZero,
        Buzz,
        LedColor,
        ResetIMU,
        SetMode,
        Calibration,
        StartStreaming,
        StopStreaming,
        SetStreamingMode,
        SinglePoseCalibration
    }

    public class AxisCommander
    {
        public AxisCommander(AxisUdpSocket axisUdpSocket) => _axisUdpSocket = axisUdpSocket;

        private readonly AxisUdpSocket _axisUdpSocket;

        private static readonly Dictionary<CommandType, byte[]> _cmdBytes = new Dictionary<CommandType, byte[]>()
        {
            { CommandType.MainHeader, new byte[] { 0x00, 0xEF, 0xAC, 0xEF, 0xAC } },
            { CommandType.ImuZero, new byte[] { 0x16, 0x02, 0x01 } },
            { CommandType.Buzz, new byte[] { 0x80 } },
            { CommandType.LedColor, new byte[] { 0x81 } },
            { CommandType.SetMode, new byte[] { 0x21 } },
            { CommandType.Calibration, new byte[] { 0x16, 0x01, 0x00 } },
            { CommandType.StartStreaming, new byte[] { 0x01, 0xE0 } },
            { CommandType.StopStreaming, new byte[] { 0x01, 0xE1 } },
            { CommandType.SetStreamingMode, new byte[] { 0x01, 0xE2, 0x01, 0x00 } },
            { CommandType.SinglePoseCalibration, new byte[] { 0x16, 0x00, 0x01 } }
        };


        // Optimize as an array, keeping the dictionary for readability
        private static readonly byte[][] _cmdBytesArr = Enum.GetValues<CommandType>()
            .OrderBy(c => (int)c)
            .Select(c => _cmdBytes.TryGetValue(c, out var b) ? b : Array.Empty<byte>())
            .ToArray();

        private static byte GetByteFromNormalizedFloat(float normalizedFloat)
        {
            return (byte)MathF.Round(Math.Clamp(normalizedFloat * 255f, 0f, 255f));
        }

        private static byte[] ToBytes(CommandType command)
        {
            return _cmdBytesArr[(int)command];
        }

        private void SendCommand(CommandType command)
        {
            _axisUdpSocket.SendData(ToBytes(command));
        }

        private void SendCommandWithHeader(CommandType command)
        {
            _axisUdpSocket.SendData(
                Enumerable.Concat(
                    ToBytes(CommandType.MainHeader),
                    ToBytes(command)
                ).ToArray()
            );
        }

        public void StartStreaming()
        {
            SendCommand(CommandType.StartStreaming);
            SendCommand(CommandType.SetStreamingMode);
        }

        public void StopStreaming()
        {
            SendCommand(CommandType.StopStreaming);
        }

        public void Reboot()
        {
            SendCommandWithHeader(CommandType.Calibration);
        }

        public void ZeroAll()
        {
            SendCommandWithHeader(CommandType.ImuZero);
        }

        public void SinglePoseCalibration()
        {
            SendCommandWithHeader(CommandType.SinglePoseCalibration);
        }

        public void SetNodeVibration(byte nodeIndex, float intensity, float durationSeconds)
        {
            _axisUdpSocket.SendData(
                Enumerable.Concat(
                    Enumerable.Concat(
                        ToBytes(CommandType.MainHeader),
                        ToBytes(CommandType.Buzz)
                    ),
                    new byte[] {
            nodeIndex,
            GetByteFromNormalizedFloat(intensity),
            GetByteFromNormalizedFloat(durationSeconds / 25.5f)
                    }
                ).ToArray()
            );
        }

        public void SetNodeLedColor(byte nodeIndex, Color color, float brightness)
        {
            brightness = brightness > 1f ? 1f / 3f : brightness / 3f;

            _axisUdpSocket.SendData(
                Enumerable.Concat(
                    Enumerable.Concat(
                        ToBytes(CommandType.MainHeader),
                        ToBytes(CommandType.LedColor)
                    ),
                    new byte[] {
                        nodeIndex,
                        color.R,
                        color.B,
                        color.G,
                        GetByteFromNormalizedFloat(brightness)
                    }
                ).ToArray()
            );
        }
    }
}
