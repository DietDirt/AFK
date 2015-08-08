﻿﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Threading;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

namespace AFK
{
    [ApiVersion(1, 21)]
    public class AFK : TerrariaPlugin
    {
        public static AFKConfigFile AFKConfig { get; set; }
        public Player[] Players { get; set; }
        internal static string AFKConfigPath { get { return Path.Combine(TShock.SavePath, "AFKconfig.json"); } }

        public override string Name
        {
            get { return "AFK Plugin"; }
        }
        public override string Author
        {
            get { return "DarkunderdoG"; }
        }
        public override string Description
        {
            get { return "AFK Plugin"; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, (args) => { OnInitialize(); });
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, (args) => { OnInitialize(); });
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }

            base.Dispose(disposing);
        }

        public AFK(Main game)
            : base(game)
        {
            Order = 50;
            AFKConfig = new AFKConfigFile();
            Players = new Player[256];
        }

        public void OnInitialize()
        {
            SetupConfig();
            Commands.ChatCommands.Add(new Command("afk.cfg", AFKreload, "afkreload"));

            if (AFKConfig.afkwarp)
            {
                Commands.ChatCommands.Add(new Command("afk.comm", AFKcomm, "afktime"));
                Commands.ChatCommands.Add(new Command("afk.comm", sendafk, "afk"));
                Commands.ChatCommands.Add(new Command("afk.comm", sendback, "return"));
                Commands.ChatCommands.Add(new Command("afk.cfg", setwarptime, "afkwarptime"));
            }
            if (AFKConfig.afkkick)
                Commands.ChatCommands.Add(new Command("afk.cfg", setkicktime, "afkkicktime"));
        }

        public void OnGreetPlayer(GreetPlayerEventArgs e)
        {
            Players[e.Who] = new Player(e.Who);
        }

        public class Player
        {
            public int Index { get; set; }
            public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
            public int afk { get; set; }
            public int afkspam { get; set; }
            public int lasttileX { get; set; }
            public int lasttileY { get; set; }
            public int backtileX { get; set; }
            public int backtileY { get; set; }
            public int afkkick { get; set; }
            public Player(int index)
            {
                Index = index;
                lasttileX = TShock.Players[Index].TileX;
                lasttileY = TShock.Players[Index].TileY;
                backtileX = 0;
                backtileY = 0;
                afk = 0;
                afkspam = 0;
                afkkick = 0;
            }
        }

        public static void SetupConfig()
        {
            try
            {
                if (File.Exists(AFKConfigPath))
                {
                    AFKConfig = AFKConfigFile.Read(AFKConfigPath);
                    // Add all the missing config properties in the json file
                }
                AFKConfig.Write(AFKConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in AFK config file");
                Console.ForegroundColor = ConsoleColor.Gray;

                TShock.Log.ConsoleError("AFK Config Exception");
                TShock.Log.ConsoleError(ex.ToString());
            }
        }

        private DateTime LastCheck = DateTime.UtcNow;

        public void OnUpdate(EventArgs e)
        {
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
            {
                LastCheck = DateTime.UtcNow;
                lock (Players)
                    foreach (Player player in Players)
                    {
                        if (player != null && player.TSPlayer != null)
                        {
                            if (AFKConfig.afkwarp || AFKConfig.afkkick)
                            {
                                string currentregionlist = "";
                                var currentregion = TShock.Regions.InAreaRegionName(player.TSPlayer.TileX, player.TSPlayer.TileY);
                                if (currentregion.Count > 0)
                                    currentregionlist = string.Join(",", currentregion.ToArray());
                                var afkwarp = TShock.Warps.Find("afk");

                                if (AFKConfig.afkkick && !player.TSPlayer.Group.HasPermission("afk.nokick"))
                                {
                                    if ((player.TSPlayer.TileX == player.lasttileX && player.TSPlayer.TileY == player.lasttileY) || (afkwarp != null) && (player.TSPlayer.TileX == (int)afkwarp.Position.X && player.TSPlayer.TileY == (int)afkwarp.Position.Y))
                                        player.afkkick++;
                                    else
                                        player.afkkick = 0;

                                    if (player.afkkick == Math.Round(AFKConfig.afkkicktime * .70) || player.afkkick == Math.Round(AFKConfig.afkkicktime * .80) || player.afkkick == Math.Round(AFKConfig.afkkicktime * .90))
                                        player.TSPlayer.SendErrorMessage("You will be kicked when you're AFK for " + AFKConfig.afkkicktime + "secs. You have been AFK for " + player.afkkick + " secs.");
                                    else if (player.afkkick >= AFKConfig.afkkicktime)
                                    {
                                        TShock.Utils.Kick(player.TSPlayer, "for being AFK for " + player.afkkick + " seconds.", true, false, "Server");
                                        return;
                                    }
                                }

                                if (AFKConfig.afkwarp)
                                {
                                    if (player.afkspam > 0)
                                        player.afkspam--;

                                    if (player.lasttileX != 0 && player.lasttileY != 0 && !currentregionlist.Contains("afk"))
                                    {
                                        if (player.TSPlayer.TileX == player.lasttileX && player.TSPlayer.TileY == player.lasttileY)
                                            player.afk++;
                                        else
                                            player.afk = 0;

                                        if (player.afk > AFKConfig.afkwarptime)
                                        {
                                            if (!player.TSPlayer.IsLoggedIn)
                                            {
                                                TShock.Utils.Kick(player.TSPlayer, "for being AFK.", true, false, "Server");
                                                return;
                                            }
                                            player.backtileX = player.TSPlayer.TileX;
                                            player.backtileY = player.TSPlayer.TileY;
                                            player.afk = 0;

                                            if (afkwarp == null)
                                            {
                                                TShock.Utils.Kick(player.TSPlayer, "for being AFK.", true, false, "Server");
                                                TShock.Log.ConsoleError("AFK Plugin: Warp \"afk\" is not defined.");
                                                return;
                                            }
                                            if (TShock.Regions.GetRegionByName("afk") == null)
                                            {
                                                TShock.Log.ConsoleError("AFK Plugin: Region \"afk\" is not defined");
                                            }
                                            
                                            if (player.TSPlayer.Teleport((int)afkwarp.Position.X * 16, (int)(afkwarp.Position.Y) * 16))
                                            {
                                                player.TSPlayer.SendErrorMessage("You have been warped to the AFK zone. Use the /return command to go back!");
                                                TShock.Utils.Broadcast(player.TSPlayer.Name + " is away from his/her keyboard and has been warped To The AFK Zone!", Color.Yellow);
                                            }
                                        }
                                    }

                                    if (!currentregionlist.Contains("afk") && player.afk > 0 && (player.TSPlayer.TileX == player.lasttileX && player.TSPlayer.TileY == player.lasttileY))
                                    {
                                        if (TShock.Regions.GetRegionByName("afk") != null) //checks if afk region exists
                                        {
                                            if (player.backtileX != 0)
                                            {
                                                if (player.TSPlayer.Teleport(player.backtileX * 16, player.backtileY * 16))
                                                {
                                                    player.TSPlayer.SendSuccessMessage("You have been warped back to where you were");
                                                    TShock.Utils.Broadcast(player.TSPlayer.Name + " is back from AFK! YAY!!!", Color.Yellow);
                                                    player.backtileX = 0;
                                                    player.backtileY = 0;
                                                }
                                            }
                                        }
                                    }
                                }

                                player.lasttileX = player.TSPlayer.TileX;
                                player.lasttileY = player.TSPlayer.TileY;

                            }
                        }
                    }
            }
        }

        private void OnLeave(LeaveEventArgs e)
        {
            if (Players[e.Who] != null)
                Players[e.Who] = null;
        }

        public void OnChat(ServerChatEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Text))
            {
                var msg = e.Buffer;
                var text = e.Text;
                var AFKPly = Players[e.Who];
                string cmd = text.Split(' ')[0];

                if (AFKConfig.afkwarp || AFKConfig.afkkick)
                {
                    if (cmd == "/afktime")
                    {
                        return;
                    }

                    else if ((cmd != "/afk" && cmd != "/return") && AFKPly.backtileX != 0 && AFKConfig.afkwarp)
                    {
                        if (AFKPly.TSPlayer.Teleport(AFKPly.backtileX * 16, AFKPly.backtileY * 16))
                        {
                            AFKPly.TSPlayer.SendSuccessMessage("You have been warped back to where you were!");
                            TShock.Utils.Broadcast(AFKPly.TSPlayer.Name + " is back from AFK! YAY!!!", Color.Yellow);
                            AFKPly.backtileX = 0;
                            AFKPly.backtileY = 0;
                        }
                    }

                    AFKPly.afk = 0;
                    AFKPly.afkkick = 0;
                }
            }
        }

        private void AFKcomm(CommandArgs args)
        {
            var AFKPly = Players[args.Player.Index];
            TShock.Players[args.Player.Index].SendErrorMessage("You have been AFK for: " + AFKPly.afk);
            TShock.Players[args.Player.Index].SendErrorMessage("You have been AFKkick for: " + AFKPly.afkkick);
        }

        private void sendafk(CommandArgs args)
        {
            var AFKPly = Players[args.Player.Index];

            if (AFKPly.afkspam > 0)
            {
                args.Player.SendErrorMessage("You just used AFK a few moments ago. Please wait to use it again");
                return;
            }

            var warp = TShock.Warps.Find("afk");
            if (warp == null)
            {
                args.Player.SendErrorMessage("Unable to send you to AFK. Warp \"afk\" is not defined");
                TShock.Log.ConsoleError("AFK Plugin: Warp \"afk\" is not defined");
                return;
            }

            string currentregionlist = "";
            var currentregion = TShock.Regions.InAreaRegionName(AFKPly.TSPlayer.TileX, AFKPly.TSPlayer.TileY);
            if (currentregion.Count > 0)
                currentregionlist = string.Join(",", currentregion.ToArray());
            if (!currentregionlist.Contains("afk"))
            {
                AFKPly.backtileX = TShock.Players[args.Player.Index].TileX;
                AFKPly.backtileY = TShock.Players[args.Player.Index].TileY;
                AFKPly.afk = 0;
                if (args.Player.Teleport((int)warp.Position.X * 16, (int)warp.Position.Y * 16))
                {
                    args.Player.SendErrorMessage("You have been warped to the AFK zone. Use the /return command to go back!");
                    TShock.Utils.Broadcast(args.Player.Name + " is away from his/her keyboard and has been warped to the AFK zone!", Color.Yellow);
                    AFKPly.afkspam = AFKConfig.afkspam;
                }
            }
            else
                args.Player.SendErrorMessage("You are already in the AFK zone. No need to use /afk");
        }

        private void sendback(CommandArgs args)
        {
            var AFKPly = Players[args.Player.Index];
            if (AFKPly.backtileX != 0)
            {
                if (args.Player.Teleport(AFKPly.backtileX * 16, AFKPly.backtileY * 16))
                {
                    args.Player.SendSuccessMessage("You have been warped back to where you were!");
                    TShock.Utils.Broadcast(args.Player.Name + " is back from AFK! YAY!!!", Color.Yellow);
                    AFKPly.backtileX = 0;
                    AFKPly.backtileY = 0;
                }
            }
            else
                args.Player.SendErrorMessage("Unable to send you back from AFK.");
        }

        private static void setwarptime(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /afkwarptime <time>");
                return;
            }
            AFKConfig.afkwarptime = int.Parse(args.Parameters[0]);
            args.Player.SendWarningMessage("AFK warp time set to: " + AFKConfig.afkwarptime);
        }

        private static void setkicktime(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /afkkicktime <time>");
                return;
            }
            AFKConfig.afkwarptime = int.Parse(args.Parameters[0]);
            args.Player.SendWarningMessage("AFK kick time set to: " + AFKConfig.afkkicktime);
        }

        private void AFKreload(CommandArgs args)
        {
            SetupConfig();
            TShock.Log.ConsoleInfo("AFK Reload Initiated");
            args.Player.SendSuccessMessage("AFk Reload Initiated");
        }

        public static List<Group> FindGroup(string grp)
        {
            var found = new List<Group>();
            grp = grp.ToLower();
            foreach (Group group in TShock.Groups.groups)
            {
                if (group == null)
                    continue;

                string name = group.Name.ToLower();
                if (name.Equals(grp))
                    return new List<Group> { group };
                if (name.Contains(grp))
                    found.Add(group);
            }
            return found;
        }
    }
}