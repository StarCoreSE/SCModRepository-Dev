using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ULTRALogger
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ULTRALogger : MySessionComponentBase
    {
        public static ULTRALogger Instance;
        private TextWriter 
            _gridsWriter,
            _playersWriter,
            _projectilesWriter;
 
        private const string Extension = ".log";
        private bool _isRecording;
        private DateTime _last;
        private Vector3D _badVector = new Vector3D(double.NaN);
        private HashSet<long> _playerIdentities = new HashSet<long>();

        #region common

        private void CheckTime()
        {
            // i just feel like it
            if ((DateTime.Now - _last).TotalSeconds != 0)
            {
                _gridsWriter.WriteLine('\n');
                _last = DateTime.Now;
            }
        }

        private string ShorterPositionString(Vector3D position) => position != _badVector ? $"(X = {position.X:#0.#}, Y = {position.Y:#0.#}, Z {position.Z:#0.#}) " : "";

        private string Timestamp() => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";

        #endregion

        public override void LoadData()
        {
            Instance = this;
            StartLogging();
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            //MyVisualScriptLogicProvider.PlayerConnected
            MyVisualScriptLogicProvider.PlayerRespawnRequest += OnPlayerRespawn;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;

            MyAPIGateway.Players.GetAllIdentites(null, b =>
            {
                if (!_playerIdentities.Contains(b.IdentityId))
                    _playerIdentities.Add(b.IdentityId);
                return true;
            });
        }

        protected override void UnloadData()
        {
            StopLogging();
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
            Instance = null;
        }



        private void StartLogging()
        {
            var fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{Extension}";
            string ascii = " __    __   __      .___________..______          ___       __        ______     _______   _______  _______ .______      \r\n|  |  |  | |  |     |           ||   _  \\        /   \\     |  |      /  __  \\   /  _____| /  _____||   ____||   _  \\     \r\n|  |  |  | |  |     `---|  |----`|  |_)  |      /  ^  \\    |  |     |  |  |  | |  |  __  |  |  __  |  |__   |  |_)  |    \r\n|  |  |  | |  |         |  |     |      /      /  /_\\  \\   |  |     |  |  |  | |  | |_ | |  | |_ | |   __|  |      /     \r\n|  `--'  | |  `----.    |  |     |  |\\  \\----./  _____  \\  |  `----.|  `--'  | |  |__| | |  |__| | |  |____ |  |\\  \\----.\r\n \\______/  |_______|    |__|     | _| `._____/__/     \\__\\ |_______| \\______/   \\______|  \\______| |_______|| _| `._____|\r\n\n";
            try
            {
                _gridsWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage("ULTRALog_GRIDS", typeof(ULTRALogger));
                _gridsWriter.Write(ascii);
                _gridsWriter.WriteLine("GRID LOG");

                _playersWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage("ULTRALog_PLAYERS", typeof (ULTRALogger));
                _playersWriter.Write(ascii);
                _playersWriter.WriteLine("PLAYER LOG");

                // TODO: wc stuff. laterrrr

                _isRecording = true;
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"[ULTRALogger] Error creating log file: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void StopLogging()
        {
                _isRecording = false;
                _gridsWriter?.Close();
                _playersWriter?.Close();
                _projectilesWriter?.Close();
                MyAPIGateway.Utilities.ShowNotification("ULTRALogger stopped.");
        }

        #region grids

        private void OnEntityAdd(IMyEntity entity)
        {
            if (_isRecording)
            {
                CheckTime();
                var grid = entity as IMyCubeGrid;

                if (grid != null)
                {
                    var owner = MyAPIGateway.Players.TryGetIdentityId(grid.BigOwners[0]).DisplayName;
                    grid.OnBlockAdded += OnBlockAdded;
                    grid.OnGridBlockDamaged += OnDamaged;
                    grid.OnBlockRemoved += OnBlockRemoved;
                    _playersWriter.WriteLine($"{Timestamp()} {owner} added new grid: \"{grid.CustomName}\" at {ShorterPositionString(grid.GetPosition())}");
                    _playersWriter.Flush();
                }
            }
        }
        private void OnEntityRemove(IMyEntity entity)
        {
            if (_isRecording)
            {
                CheckTime();
                var grid = entity as IMyCubeGrid;
                if (grid != null)
                {
                    // do i need this?
                    grid.OnBlockAdded -= OnBlockAdded;
                    grid.OnBlockRemoved -= OnBlockRemoved;
                    grid.OnGridBlockDamaged -= OnDamaged;
                    _gridsWriter.WriteLine($"{Timestamp()} Removed grid: \"{grid.CustomName}\" at {ShorterPositionString(grid.GetPosition())}");
                    _gridsWriter.Flush();
                }
            }
        }

        private void OnDamaged(IMySlimBlock block, float whatever, MyHitInfo? hit, long presumablyEntityID)
        {
            if (_isRecording)
            {
                CheckTime();
                string
                   parent = block?.CubeGrid.CustomName,
                   blockType = block.FatBlock?.BlockDefinition.SubtypeId ?? "slim",
                   position = block.FatBlock != null ? "at " + ShorterPositionString((hit.HasValue ? hit.Value.Position : _badVector)) : "";

                _gridsWriter.WriteLine($"{Timestamp()} {blockType} block {position}on parent grid \"{parent}\"");
                _gridsWriter.Flush();
            }
        }

        #endregion

        #region players

        private void OnPlayerRespawn(long identityId)
        {
            var player = MyAPIGateway.Players.TryGetIdentityId(identityId);
            if (player != null)
            {
                if (_playerIdentities.Contains(identityId))
                    _playersWriter.WriteLine($"{Timestamp()} {MyAPIGateway.Players.TryGetIdentityId(identityId).DisplayName} respawned at {ShorterPositionString(player.GetPosition())}");
                else
                {
                    _playersWriter.WriteLine($"{Timestamp()} {MyAPIGateway.Players.TryGetIdentityId(identityId).DisplayName} joined the server and respawned at {ShorterPositionString(player.GetPosition())}");
                    _playerIdentities.Add(identityId);
                }
                _playersWriter.Flush();
            }
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
            if (_isRecording) 
            {
                CheckTime();
                string
                    builder = MyAPIGateway.Players.TryGetIdentityId(block.BuiltBy).DisplayName,
                    parent = block?.CubeGrid.CustomName,
                    blockType = block.FatBlock?.BlockDefinition.SubtypeId ?? "slim",
                    position = block.FatBlock != null ? "at " + ShorterPositionString(block.FatBlock.GetPosition()) : "";
                
                _gridsWriter.WriteLine($"{Timestamp()} {builder} added new {blockType} block {position}on parent grid \"{parent}\"");
                _gridsWriter.Flush();
            }
        }

        private void OnBlockRemoved(IMySlimBlock block)
        {
            if (_isRecording)
            {
                CheckTime();
                string
                    parent = block?.CubeGrid.CustomName,
                    blockType = block.FatBlock?.BlockDefinition.SubtypeId ?? "slim",
                    position = block.FatBlock != null ? "at " + ShorterPositionString(block.FatBlock.GetPosition()) : "";

                _gridsWriter.WriteLine($"{Timestamp()} Removed {blockType} block {position}from parent grid \"{parent}\"");
                _gridsWriter.Flush();
            }
        }

        #endregion
    }
}
