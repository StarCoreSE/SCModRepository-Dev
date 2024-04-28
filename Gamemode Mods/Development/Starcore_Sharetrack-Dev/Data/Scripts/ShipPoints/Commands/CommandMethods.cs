using ShipPoints.Data.Scripts.ShipPoints.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using klime.PointCheck;
using Math0424.Networking;
using Sandbox.ModAPI;
using SCModRepository.Gamemode_Mods.Stable.Starcore_Sharetrack.Data.Scripts.ShipPoints.MatchTimer;
using VRage.Utils;

namespace ShipPoints.Commands
{
    internal static class CommandMethods
    {
        #region Match Commands

        public static void Start(string[] args)
        {
            MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(6), true, true);
            PointCheck._amTheCaptainNow = true;
            PointCheck.I.Team1Tickets.Value = PointCheck.MatchTickets;
            PointCheck.I.Team2Tickets.Value = PointCheck.MatchTickets;
            PointCheck.LocalMatchState = 1;
            MatchTimer.I.Start(PointCheck.Matchtime / 60d / 60d);
            MyAPIGateway.Utilities.ShowMessage("GM", "You are the captain now.");
            MyAPIGateway.Utilities.ShowNotification("HEY DUMBASS, IS DAMAGE ON?", 10000, "Red");
        }

        public static void End(string[] args)
        {
            MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(8), true, true);
            PointCheck._amTheCaptainNow = false;
            PointCheck.I.Team1Tickets.Value = PointCheck.MatchTickets;
            PointCheck.I.Team2Tickets.Value = PointCheck.MatchTickets;
            PointCheck.I.Team3Tickets.Value = PointCheck.MatchTickets;
            PointCheck.LocalMatchState = 0;
            PointCheck.I.CaptainCapTimerZ3T1.Value = 0;
            PointCheck.I.CaptainCapTimerZ3T2.Value = 0;
            PointCheck.I.CaptainCapTimerZ3T3.Value = 0;
            PointCheck.I.CaptainCapTimerZ2T1.Value = 0;
            PointCheck.I.CaptainCapTimerZ2T2.Value = 0;
            PointCheck.I.CaptainCapTimerZ2T3.Value = 0;
            PointCheck.I.CaptainCapTimerZ1T1.Value = 0;
            PointCheck.I.CaptainCapTimerZ1T2.Value = 0;
            PointCheck.I.CaptainCapTimerZ1T3.Value = 0;
            MatchTimer.I.Stop();
            MyAPIGateway.Utilities.ShowMessage("GM", "Match Ended.");
        }

        public static void TakeOver(string[] args)
        {
            PointCheck._amTheCaptainNow = true;
            MyAPIGateway.Utilities.ShowMessage("GM", "You are the captain now.");
        }

        public static void GiveUp(string[] args)
        {
            PointCheck._amTheCaptainNow = false;
            MyAPIGateway.Utilities.ShowMessage("GM", "You are not the captain now.");
        }

        #endregion

        #region Match Config

        public static void SetMatchTime(string[] args)
        {
            try
            {
                MyAPIGateway.Utilities.ShowNotification("Match duration changed to " + args[1] + " minutes.");
                PointCheck.Matchtime = int.Parse(args[1]) * 60 * 60;
                MatchTimer.I.MatchDurationMinutes = PointCheck.Matchtime / 60d / 60d;
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowNotification("Win time not changed, try /setmatchtime xxx (in minutes)");
            }
        }

        public static void SetTeams(string[] args)
        {
            try
            {
                PointCheck.I.Team1.Value = args[1].ToUpper();
                PointCheck.I.Team2.Value = args[2].ToUpper();
                PointCheck.I.Team3.Value = args[3].ToUpper();
                //team1_Local = tempdist[1].ToUpper(); team2_Local = tempdist[2].ToUpper(); team3_Local = tempdist[3].ToUpper();
                MyAPIGateway.Utilities.ShowNotification("Teams changed to " + args[1] + " vs " + args[2] +
                                                        " vs " + args[3]); //sendToOthers = true;
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowNotification("Teams not changed, try /setteams abc xyz");
            }
        }

        public static void SetWinTime(string[] args)
        {
            try
            {
                PointCheck.Wintime = int.Parse(args[1]);
                MatchTimer.I.MatchDurationMinutes = PointCheck.Wintime;
                MyAPIGateway.Utilities.ShowNotification("Win time changed to " + PointCheck.Wintime);
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowNotification("Win time not changed, try /settime xxx (in seconds)");
            }
        }

        public static void SetDelay(string[] args)
        {
            try
            {
                PointCheck.Delaytime = int.Parse(args[1]);
                MyAPIGateway.Utilities.ShowNotification("Delay time changed to " + PointCheck.Delaytime + " minutes.");
                PointCheck.Delaytime *= 60 * 60;
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowNotification("Delay time not changed, try /setdelay x (in minutes)");
            }
        }

        public static void SetDecay(string[] args)
        {
            try
            {
                PointCheck.Decaytime = int.Parse(args[1]);
                MyAPIGateway.Utilities.ShowNotification("Decay time changed to " + PointCheck.Decaytime + " seconds.");
                PointCheck.Decaytime *= 60;
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowNotification("Decay time not changed, try /setdecay xxx (in seconds)");
            }
        }

        public static void SetTeamTickets(string[] args)
        {
            try
            {
                switch (args[1].ToLower())
                {
                    case "1":
                        PointCheck.I.Team1Tickets.Value = int.Parse(args[2]);
                        break;
                    case "2":
                        PointCheck.I.Team2Tickets.Value = int.Parse(args[2]);
                        break;
                    case "3":
                        PointCheck.I.Team3Tickets.Value = int.Parse(args[2]);
                        break;
                    default:
                        MyAPIGateway.Utilities.ShowMessage("ShareTrack", "Invalid team (use 1, 2, or 3).");
                        break;
                }
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowMessage("ShareTrack", "Invalid tickets value.");
            }
        }

        public static void SetTwoTeams(string[] args)
        {
            MyAPIGateway.Utilities.ShowMessage("GM", "Teams set to two.");
            PointCheck.I.ThreeTeams.Value = 0;
        }

        public static void SetThreeTeams(string[] args)
        {
            MyAPIGateway.Utilities.ShowMessage("GM", "Teams set to three.");
            PointCheck.I.ThreeTeams.Value = 1;
            PointCheck.I.Team3Tickets.Value = PointCheck.MatchTickets;
        }

        public static void SetCapMode(string[] args)
        {
            try
            {
                switch (args[1].ToLower())
                {
                    case "0":
                        MyAPIGateway.Utilities.ShowMessage("GM", "Capture zones set to none.");
                        PointCheck.I.GameModeSwitch.Value = 4;
                        PointCheck.LocalGameModeSwitch = 4;
                        MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(15), true, true);
                        break;
                    case "1":
                        MyAPIGateway.Utilities.ShowMessage("GM", "Capture zones set to one.");
                        PointCheck.I.GameModeSwitch.Value = 1;
                        PointCheck.LocalGameModeSwitch = 1;
                        MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(12), true, true);
                        break;
                    case "2":
                        MyAPIGateway.Utilities.ShowMessage("GM", "Capture zones set to two.");
                        PointCheck.I.GameModeSwitch.Value = 2;
                        PointCheck.LocalGameModeSwitch = 2;
                        MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(13), true, true);
                        break;
                    case "3":
                        MyAPIGateway.Utilities.ShowMessage("GM", "Capture zones set to three.");
                        PointCheck.I.GameModeSwitch.Value = 3;
                        PointCheck.LocalGameModeSwitch = 3;
                        MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(14), true, true);
                        break;
                    case "c":
                        MyAPIGateway.Utilities.ShowMessage("GM", "Capture zones set to crazy.");
                        PointCheck.I.GameModeSwitch.Value = 4;
                        PointCheck.LocalGameModeSwitch = 4;
                        MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(16), true, true);
                        break;
                    default:
                        MyAPIGateway.Utilities.ShowMessage("ShareTrack", "Invalid CapMode.");
                        break;
                }
            }
            catch (Exception)
            {
                MyAPIGateway.Utilities.ShowMessage("ShareTrack", "Invalid CapMode.");
            }
        }

        #endregion

        #region Utility Commands

        public static void ToggleSphere(string[] args)
        {
            PointCheck.I.SphereVisual = !PointCheck.I.SphereVisual;
        }

        public static void Shields(string[] args)
        {
            MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(5));
        }

        public static void ReportProblem(string[] args)
        {
            MyAPIGateway.Utilities.ShowNotification("A problem has been reported.", 10000);
            PointCheck.LocalProblemSwitch = 1;
            MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(17), true, true);
        }

        public static void ReportFixed(string[] args)
        {
            MyAPIGateway.Utilities.ShowNotification("Fixed :^)", 10000);
            PointCheck.LocalProblemSwitch = 0;
            MyNetworkHandler.Static.MyNetwork.TransmitToServer(new BasicPacket(18), true, true);
        }

        #endregion
    }
}
