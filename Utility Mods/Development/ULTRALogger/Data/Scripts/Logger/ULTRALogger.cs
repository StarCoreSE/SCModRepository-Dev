using System;
using System.IO;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace ULTRALogger
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Logger : MySessionComponentBase
    {
        public static Logger Instance;
        private TextWriter _writer;
        private const string Extension = ".log";
        private bool _isRecording;

        public override void LoadData()
        {
            Instance = this;
            StartLogging();
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
        }

        protected override void UnloadData()
        {
            StopLogging();
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            Instance = null;
        }

        private void StartLogging()
        {
            var fileName = $"ULTRALog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{Extension}";
            try
            {
                _writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName, typeof(Logger));
                _writer.WriteLine("Timestamp, BlockType, Position");
                _isRecording = true;
                MyAPIGateway.Utilities.ShowNotification("ULTRALogger started.", 10000);
                MyVisualScriptLogicProvider.SendChatMessage($"Logger started at {DateTime.Now}.");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"[ULTRALogger] Error creating log file: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void StopLogging()
        {
            if (_isRecording)
            {
                _isRecording = false;
                _writer?.Close();
                MyAPIGateway.Utilities.ShowNotification("ULTRALogger stopped.");
            }
        }

        private void OnEntityAdd(IMyEntity entity)
        {
            if (_isRecording)
            {
                MyAPIGateway.Utilities.ShowNotification($"Added new entity.{entity.EntityId.ToString("X")}", 1000);
                var block = entity as IMyCubeBlock;

                if (block != null)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var blockType = block.BlockDefinition.SubtypeId;
                    var position = block.GetPosition();

                    _writer.WriteLine($"{timestamp}, {blockType}, {position}");
                    _writer.Flush();
                }
            }
        }
    }
}
