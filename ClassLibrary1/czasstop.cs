﻿using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Menus;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using StardewValley.Events;
using System.Linq;
using StardewValley.Minigames;
using StardewValley.Locations;
using StardewValley.Characters;

namespace MultiplayerTime
{
    public class ModConfig
    {
        public SButton ActivationKey { get; set; } = SButton.F3;
        public bool Active { get; set; } = true;
        public bool UiInfoSuite { get; set; } = false;
        public bool InvisibleUI { get; set; } = false;
        public string HourFormat { get; set; } = "Default";
        public string InterfaceTheme { get; set; } = "Default";

    }

    public class PlayerList
    {
        public long PlayerID;
        public int message = -1;

        public PlayerList(long ID)
        {
            PlayerID = ID;
        }
    }

    public class Buffs
    {
        public Buff buff;
        public int duration;

        public Buffs(Buff b, int d)
        {
            buff = b;
            duration = d;
        }
    }

    public class Potwory
    {
        public Monster monster;
        public int info;
        public int Health;

        public Potwory(Monster m, int i, int h)
        {
            monster = m;
            info = i;
            Health = h;
        }
    }
    public interface IGenericModConfigMenuAPI
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);
    }

    class MultiplayerTimeMod : Mod
    {
        /*********
       ** Public methods
       *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        int timeinterval = 0;
        int SKL = 0;
        int SKH = 0;
        bool SKTime = true;
        bool JustPaused = false;
        bool JustUnpaused = false;
        readonly List<Potwory> potwory = new();
        readonly List<PlayerList> Gracze = new();
        readonly PlayerList Me = new(0);
        readonly List<GameLocation> locations = new();
        readonly List<Buffs> buffs = new();
        bool hourFormat;
        Vector2 PasekPosition = new(44, 240);
        Color textColor = Game1.textColor;
        Texture2D Pasek;
        Texture2D PasekWithUIS;
        Texture2D PasekZoom;
        Texture2D Black;
        Texture2D Blue;
        Texture2D Green;
        Texture2D Red;
        private ModConfig Config;
        public override void Entry(IModHelper helper)
        {
            this.Config = (ModConfig)helper.ReadConfig<ModConfig>();

            Black = helper.ModContent.Load<Texture2D>("assets/Black.png");
            Blue = helper.ModContent.Load<Texture2D>("assets/Blue.png");
            Green = helper.ModContent.Load<Texture2D>("assets/Green.png");
            Red = helper.ModContent.Load<Texture2D>("assets/Red.png");

            switch (this.Config.InterfaceTheme)
            {
                case "Default":
                    Pasek = helper.ModContent.Load<Texture2D>("assets/Pasek.png");
                    PasekWithUIS = helper.ModContent.Load<Texture2D>("assets/PasekWithUIS.png");
                    PasekZoom = helper.ModContent.Load<Texture2D>("assets/PasekZoom.png");
                    break;
                case "Vintage V2":
                    Pasek = helper.ModContent.Load<Texture2D>("assets/PasekVintage.png");
                    PasekWithUIS = helper.ModContent.Load<Texture2D>("assets/PasekWithUISVintage.png");
                    PasekZoom = helper.ModContent.Load<Texture2D>("assets/PasekZoomVintage.png");
                    break;
                case "Natural Dark Wood":
                    Pasek = helper.ModContent.Load<Texture2D>("assets/PasekDarkWood.png");
                    PasekWithUIS = helper.ModContent.Load<Texture2D>("assets/PasekWithUISDarkWood.png");
                    PasekZoom = helper.ModContent.Load<Texture2D>("assets/PasekZoomDarkWood.png");
                    break;
            }

            if (this.Config.HourFormat == "Default")
            {
                if (Helper.Translation.Get("Default") == "12")
                {
                    hourFormat = true;
                }
                else
                {
                    hourFormat = false;
                }
            }
            else if (this.Config.HourFormat == "12")
            {
                hourFormat = true;
            }
            else
            {
                hourFormat = false;
            }

            if (this.Config.Active)
            {
                Helper.Events.GameLoop.UpdateTicked += this.GameEvents_UpdateTick;
                Helper.Events.Display.RenderingHud += this.PreRenderHud;
                Helper.Events.Display.RenderedHud += this.PostRenderHud;
                Helper.Events.Display.Rendered += this.RenderClock;
                Helper.Events.GameLoop.DayStarted += this.Statement;
            }
            Helper.Events.GameLoop.GameLaunched += OnLaunched;
            Helper.Events.Input.ButtonPressed += new EventHandler<ButtonPressedEventArgs>(this.ButtonPressed);
            Helper.Events.Multiplayer.PeerConnected += this.PlayerConnected;
            Helper.Events.Multiplayer.PeerDisconnected += this.PlayerDisconnected;
            Helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageRecieved;
            Helper.Events.Content.LocaleChanged += this.OnLocaleChanged;
            Helper.Events.GameLoop.ReturnedToTitle += this.OnTitle;
        }

        private void OnLaunched(object sender, GameLaunchedEventArgs e)
        {
            Config = Helper.ReadConfig<ModConfig>();
            var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
            api.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(Config)
            );

            api.AddKeybind(
                mod: this.ModManifest,
                name: () => "Activation Key",
                tooltip: () => "Clicking this key during game will change state of \"Active\" checkbox below.",
                getValue: () => this.Config.ActivationKey,
                setValue: value => this.Config.ActivationKey = value
            );

            api.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Active",
                tooltip: () => "Mod works only if every player have this checked",
                getValue: () => this.Config.Active,
                setValue: value => this.Config.Active = value
            );

            api.AddBoolOption(
                mod: this.ModManifest,
                name: () => "UI Info Suite",
                tooltip: () => "Check this if you use UI Info Suite mod",
                getValue: () => this.Config.UiInfoSuite,
                setValue: value => this.Config.UiInfoSuite = value
            );

            api.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Invisible UI",
                tooltip: () => "UI of this mod will not appear",
                getValue: () => this.Config.InvisibleUI,
                setValue: value => this.Config.InvisibleUI = value
            );

            api.AddTextOption(
                mod: this.ModManifest,
                name: () => "Hour Format",
                tooltip: () => "Use default hour format for selected language or force 12 or 24 hour format if you use other mods that can change hour format",
                getValue: () => this.Config.HourFormat,
                setValue: value => this.Config.HourFormat = value,
                allowedValues: new string[] { "Default", "12", "24" }
            );

            api.AddTextOption(
                mod: this.ModManifest,
                name: () => "Interface Theme",
                tooltip: () => "Use default interface theme or different if you use other mods that change the theme of interface. Restart the game after changing this option",
                getValue: () => this.Config.InterfaceTheme,
                setValue: value => this.Config.InterfaceTheme = value,
                allowedValues: new string[] { "Default", "Vintage V2", "Natural Dark Wood"}
            );
        }

        private void ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (Context.IsWorldReady)
            {
                if (e.Button == this.Config.ActivationKey)
                {
                    if (this.Config.Active)
                    {
                        this.Config.Active = false;
                        Helper.Events.GameLoop.UpdateTicked -= this.GameEvents_UpdateTick;
                        Helper.Events.Display.RenderingHud -= this.PreRenderHud;
                        Helper.Events.Display.RenderedHud -= this.PostRenderHud;
                        Helper.Events.Display.Rendered -= this.RenderClock;
                        this.Helper.WriteConfig(Config);
                        if (Context.IsWorldReady)
                        {
                            Game1.chatBox.addMessage("Multiplayer time Mod turned off", Color.White);
                            this.Helper.Multiplayer.SendMessage(-1, "pause?", modIDs: new[] { this.ModManifest.UniqueID });
                            Me.message = -1;
                        }
                    }
                    else
                    {
                        this.Config.Active = true;
                        Helper.Events.GameLoop.UpdateTicked += this.GameEvents_UpdateTick;
                        Helper.Events.Display.RenderingHud += this.PreRenderHud;
                        Helper.Events.Display.RenderedHud += this.PostRenderHud;
                        Helper.Events.Display.Rendered += this.RenderClock;
                        this.Helper.WriteConfig(Config);
                        if (Context.IsWorldReady)
                        {
                            Game1.chatBox.addMessage("Multiplayer time Mod turned on", Color.White);
                        }
                    }
                }
                if (e.Button == SButton.MouseLeft && this.Config.Active && Context.HasRemotePlayers)
                {
                    if (new Rectangle((int)((Game1.dayTimeMoneyBox.position.X + PasekPosition.X + 24) * Game1.options.uiScale), (int)((Game1.dayTimeMoneyBox.position.Y + PasekPosition.Y + (Game1.options.zoomButtons ? 52 : 24)) * Game1.options.uiScale), (int)(108 * Game1.options.uiScale), (int)(24 * Game1.options.uiScale)).Contains(Game1.getMouseX(), Game1.getMouseY()))
                    {
                        foreach (PlayerList gracz in Gracze)
                        {
                            foreach (Farmer farmer in Game1.getOnlineFarmers())
                            {
                                if (gracz.message == -1 && gracz.PlayerID == farmer.UniqueMultiplayerID && Context.IsMainPlayer)
                                {
                                    Game1.chatBox.addMessage(farmer.Name + " does not have Multiplayer Time mod", Color.Red);
                                }
                                else if (gracz.message != 1 && gracz.PlayerID == farmer.UniqueMultiplayerID)
                                {
                                    Game1.chatBox.addMessage("Time doesn't freeze because of " + farmer.Name, Color.White);
                                }
                            }
                        }
                        this.Helper.Input.Suppress(SButton.MouseLeft);
                    }
                }
            }
        }

        private void Statement(object sender, EventArgs e)
        {
            if (this.Config.Active)
            {
                Game1.chatBox.addMessage("Multiplayer time Mod turned on", Color.White);
            }
            else
            {
                Game1.chatBox.addMessage("Multiplayer time Mod turned off", Color.White);
            }
            Gracze.Clear();
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                if (Game1.player.UniqueMultiplayerID == farmer.UniqueMultiplayerID)
                {
                    Me.PlayerID = farmer.UniqueMultiplayerID;
                    Gracze.Add(Me);
                }
                else
                {
                    Gracze.Add(new PlayerList(farmer.UniqueMultiplayerID));
                }
            }
        }

        private void PlayerConnected(object sender, PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                if (e.Peer.GetMod("lolmaj.MultiplayerTime") == null)
                {
                    Game1.chatBox.addMessage(Game1.getOnlineFarmers().FirstOrDefault(p => p.UniqueMultiplayerID == e.Peer.PlayerID)?.Name ?? e.Peer.PlayerID.ToString() + " does not have Multiplayer Time mod", Color.Red);
                }
            }
        }

        private void PlayerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            bool paused = !ShouldTimePass();
            for (int i = Gracze.Count - 1; i >= 0; i--)
            {
                if (Gracze[i].PlayerID == e.Peer.PlayerID)
                {
                    Gracze.RemoveAt(i);
                }
            }
            if (paused && ShouldTimePass())
            {
                JustUnpaused = true;
            }
            if (!paused && !ShouldTimePass())
            {
                JustPaused = true;
            }
            if (Context.IsMainPlayer)
            {
                if (!Context.HasRemotePlayers)
                {
                    foreach (GameLocation location in locations)
                    {
                        foreach (Character Monsters in location.characters)
                        {
                            if (Monsters is Monster)
                            {
                                if (Monsters.Speed == 0)
                                {
                                    Monsters.Speed = Convert.ToInt32(MonsterInfo(Monsters.Name)[10]);
                                    if (Monsters is Spiker)
                                    {
                                        Monsters.Speed = 14;
                                    }
                                    if (Monsters is GreenSlime || Monsters is SquidKid)
                                    {
                                        (Monsters as Monster).moveTowardPlayer(Convert.ToInt32(MonsterInfo(Monsters.Name)[9]));
                                    }
                                }
                            }
                        }
                        for (int k = location.TemporarySprites.Count - 1; k >= 0; k--)
                        {
                            if (location.TemporarySprites[k].bombRadius > 0)
                            {
                                location.TemporarySprites[k].paused = false;
                            }
                        }
                    }
                    foreach (Potwory potwor in potwory)
                    {
                        if (potwor.monster is DinoMonster)
                        {
                            (potwor.monster as DinoMonster).totalFireTime = potwor.info + 500;
                        }
                        potwor.monster.Health = potwor.Health;
                    }
                    foreach (Farmer gracz in Game1.getOnlineFarmers())
                    {
                        gracz.temporarilyInvincible = false;
                        gracz.temporaryInvincibilityTimer = 0;
                    }
                    potwory.Clear();
                    locations.Clear();
                }
            }
        }

        public void OnModMessageRecieved(object sender, ModMessageReceivedEventArgs e)
        {
            bool paused = !ShouldTimePass();
            if (e.FromModID == this.ModManifest.UniqueID && e.Type == "pause?")
            {
                foreach (PlayerList gracz in Gracze)
                {
                    if (gracz.PlayerID == e.FromPlayerID)
                    {
                        gracz.message = e.ReadAs<int>();
                    }
                }
            }
            if (paused && ShouldTimePass())
            {
                JustUnpaused = true;
            }
            if (!paused && !ShouldTimePass())
            {
                JustPaused = true;
            }
        }

        public void OnLocaleChanged(object sender, LocaleChangedEventArgs e)
        {
            if (this.Config.HourFormat == "Default")
            {
                if (Helper.Translation.Get("Default") == "12")
                {
                    hourFormat = true;
                }
                else
                {
                    hourFormat = false;
                }
            }
            else if (this.Config.HourFormat == "12")
            {
                hourFormat = true;
            }
            else
            {
                hourFormat = false;
            }
        }

        public void OnTitle(object sender, ReturnedToTitleEventArgs e)
        {
            Gracze.Clear();
        }

        private void PreRenderHud(object sender, EventArgs e)
        {
            if (!ShouldTimePass())
            {
                Game1.textColor *= 0f;
                Game1.dayTimeMoneyBox.timeShakeTimer = 0;
            }
            if (Context.HasRemotePlayers && !Game1.isFestival() && !this.Config.InvisibleUI)
            {
                DrawPasek(Game1.spriteBatch);
            }
        }

        private void PostRenderHud(object sender, EventArgs e)
        {
            if (Context.IsWorldReady && !ShouldTimePass())
            {
                DrawFade(Game1.spriteBatch);
            }
            Game1.textColor = textColor;
        }

        private void RenderClock(object sender, EventArgs e)
        {
            if (Context.IsWorldReady && !this.Config.InvisibleUI)
            {
                if (!Game1.isFestival() && !(Game1.farmEvent != null && (Game1.farmEvent is FairyEvent || Game1.farmEvent is WitchEvent || Game1.farmEvent is SoundInTheNightEvent)) && (Game1.eventUp || Game1.currentMinigame != null || Game1.activeClickableMenu is AnimalQueryMenu || Game1.activeClickableMenu is PurchaseAnimalsMenu || Game1.activeClickableMenu is CarpenterMenu || Game1.freezeControls))
                {
                    Game1.textColor *= 0f;
                    Game1.dayTimeMoneyBox.timeShakeTimer = 0;
                    Game1.spriteBatch.End();
                    Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    Game1.dayTimeMoneyBox.draw(Game1.spriteBatch);
                    Game1.textColor = textColor;
                    DrawFade(Game1.spriteBatch);
                    if (Context.HasRemotePlayers)
                    {
                        DrawPasek(Game1.spriteBatch);
                    }
                }
            }
        }

        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            if (Context.IsWorldReady)
            {
                if (Gracze.Count < Game1.getOnlineFarmers().Count)
                {
                    foreach (Farmer gracz in Game1.getOnlineFarmers().Where(x => !Gracze.Any(z => x.UniqueMultiplayerID == z.PlayerID)).ToList())
                    {
                        Gracze.Add(new PlayerList(gracz.UniqueMultiplayerID));
                    }
                }
                bool paused = !ShouldTimePass();
                if (Me.message != 1 && (!Context.IsPlayerFree || (Game1.currentMinigame != null && (Game1.currentMinigame.minigameId() == "PrairieKing" || Game1.currentMinigame.minigameId() == nameof(CraneGame) || Game1.currentMinigame.minigameId() == nameof(Slots))) || (!Context.CanPlayerMove && !Game1.player.UsingTool) || Game1.freezeControls || Game1.activeClickableMenu is BobberBar))
                {
                    this.Helper.Multiplayer.SendMessage(1, "pause?", modIDs: new[] { this.ModManifest.UniqueID });
                    Me.message = 1;
                }
                if (Me.message != 0 && Context.IsPlayerFree && Game1.currentMinigame == null && !Game1.freezeControls && (Context.CanPlayerMove || Game1.player.UsingTool))
                {
                    this.Helper.Multiplayer.SendMessage(0, "pause?", modIDs: new[] { this.ModManifest.UniqueID });
                    Me.message = 0;
                }
                if (Game1.player.currentLocation is MineShaft && (Game1.player.currentLocation as MineShaft).getMineArea() == 121)
                {
                    if (Game1.player.currentLocation.map.GetLayer("Buildings").Tiles[(int)Game1.currentCursorTile.X, (int)Game1.currentCursorTile.Y] != null && Game1.player.currentLocation.map.GetLayer("Buildings").Tiles[(int)Game1.currentCursorTile.X, (int)Game1.currentCursorTile.Y].TileIndex == 174)
                    {
                        SKL = (Game1.player.currentLocation as MineShaft).mineLevel;
                        SKH = Game1.player.health;
                    }
                    if ((Game1.player.currentLocation as MineShaft).mineLevel - SKL == 1)
                    {
                        SKL = 0;
                    }
                    else if (SKL != 0 && (Game1.player.currentLocation as MineShaft).mineLevel - SKL > 1 && SKH == Game1.player.health)
                    {
                        Game1.player.health = Math.Max(1, SKH - ((Game1.player.currentLocation as MineShaft).mineLevel - SKL) * 3);
                        SKL = 0;
                    }
                }
                else
                {
                    SKL = 0;
                }
                if (paused && ShouldTimePass())
                {
                    JustUnpaused = true;
                }
                if (!paused && !ShouldTimePass())
                {
                    JustPaused = true;
                }
                if (JustPaused)
                {
                    foreach (Buff value in Game1.player.buffs.AppliedBuffs.Values)
                    {
                        buffs.Add(new Buffs(value, value.millisecondsDuration));
                    }
                }
                if (!ShouldTimePass())
                {
                    foreach (Buff value in Game1.player.buffs.AppliedBuffs.Values)
                    {
                        bool handled = false;
                        foreach (Buffs b in buffs)
                        {
                            if (value == b.buff)
                            {
                                value.millisecondsDuration = b.duration;
                                handled = true;
                                break;
                            }
                        }
                        if (handled == false)
                        {
                            switch (value.id)
                            {
                                case "12":
                                case "13":
                                case "14":
                                case "19":
                                case "25":
                                case "26":
                                case "27":
                                    value.millisecondsDuration = 0;
                                    break;
                                case "17":
                                case "20":
                                case "21":
                                case "22":
                                case "23":
                                case "24":
                                case "28":
                                    buffs.Add(new Buffs(value, value.millisecondsDuration));
                                    break;
                            }
                        }
                    }
                    if (!Game1.eventUp && !Game1.paused)
                    {
                        if (Game1.player.swimming.Value)
                        {
                            Game1.player.swimTimer = 100;
                        }
                        if (Game1.player.isInBed.Value)
                        {
                            Game1.player.regenTimer = 100;
                        }
                    }
                }
                if (JustUnpaused)
                {
                    buffs.Clear();
                }
                if (Context.IsMainPlayer)
                {
                    if (Context.HasRemotePlayers)
                    {
                        if (JustPaused)
                        {
                            timeinterval = Game1.gameTimeInterval > 6800 ? 6800 : Game1.gameTimeInterval;
                            foreach (Farmer gracz in Game1.getOnlineFarmers())
                            {
                                if (gracz.currentLocation != null)
                                {
                                    if (!locations.Contains(gracz.currentLocation))
                                    {
                                        locations.Add(gracz.currentLocation);
                                    }
                                }
                            }
                            foreach (GameLocation location in locations)
                            {

                                for (int k = location.TemporarySprites.Count - 1; k >= 0; k--)
                                {
                                    if (location.TemporarySprites[k].bombRadius > 0)
                                    {
                                        location.TemporarySprites[k].paused = true;
                                    }
                                }
                                foreach (Character Monsters in location.characters)
                                {
                                    if (Monsters is Monster)
                                    {
                                        if (Monsters is not Bug)
                                        {
                                            Monsters.Halt();
                                        }
                                        if (Monsters is GreenSlime || Monsters is SquidKid)
                                        {
                                            (Monsters as Monster).moveTowardPlayer(0);
                                        }
                                        if (Monsters is Duggy)
                                        {
                                            potwory.Add(new Potwory(Monsters as Monster, Monsters.Sprite.currentFrame, (Monsters as Monster).Health));
                                        }
                                        if (Monsters is DinoMonster)
                                        {
                                            potwory.Add(new Potwory(Monsters as Monster, (Monsters as DinoMonster).totalFireTime, (Monsters as Monster).Health));
                                        }
                                        if (Monsters is ShadowShaman && potwory.Count == 0)
                                        {
                                            foreach (Character m in location.characters)
                                            {
                                                if (m is Monster)
                                                {
                                                    potwory.Add(new Potwory(m as Monster, 0, (m as Monster).Health));
                                                }
                                            }
                                        }
                                        Monsters.Speed = 0;
                                    }
                                }
                            }
                        }
                        if (!ShouldTimePass())
                        {
                            Game1.gameTimeInterval = timeinterval;
                            foreach (GameLocation location in Game1.locations)
                            {
                                foreach (Character NPCs in location.characters)
                                {
                                    if (NPCs is NPC || NPCs is Pet)
                                    {
                                        NPCs.movementPause = 1;
                                    }
                                }
                                if (location is Farm)
                                {
                                    foreach (FarmAnimal animal in (location as Farm).getAllFarmAnimals())
                                    {
                                        animal.pauseTimer = 100;
                                    }
                                }
                            }
                            foreach (Farmer gracz in Game1.getOnlineFarmers())
                            {
                                gracz.temporarilyInvincible = true;
                                gracz.temporaryInvincibilityTimer = 0;
                                if (gracz.currentLocation != null)
                                {
                                    if (!locations.Contains(gracz.currentLocation))
                                    {
                                        locations.Add(gracz.currentLocation);
                                    }
                                }
                            }
                            foreach (GameLocation location in locations)
                            {
                                foreach (Character Monsters in location.characters)
                                {
                                    if (Monsters is Monster)
                                    {
                                        if (Monsters is Bat || Monsters is Ghost || Monsters is DustSpirit || Monsters is DwarvishSentry || Monsters is DinoMonster)
                                        {
                                            Monsters.xVelocity = 0f;
                                            Monsters.yVelocity = 0f;
                                            if (Monsters is DustSpirit)
                                            {
                                                Monsters.yJumpVelocity = 0f;
                                            }
                                        }
                                        if (Monsters is Fly || Monsters is Serpent)
                                        {
                                            (Monsters as Monster).setInvincibleCountdown(100);
                                            Monsters.stopGlowing();
                                        }
                                        if (Monsters is LavaLurk)
                                        {
                                            (Monsters as LavaLurk).stateTimer = 1f;
                                            if ((Monsters as LavaLurk).currentState.Value == LavaLurk.State.Firing)
                                            {
                                                (Monsters as LavaLurk).currentState.Value = LavaLurk.State.Emerged;
                                            }
                                        }
                                        if (Monsters is BlueSquid)
                                        {
                                            (Monsters as BlueSquid).canMoveTimer = 500f;
                                            if ((Monsters as BlueSquid).projectileIntroTimer.Value > 0)
                                            {
                                                (Monsters as BlueSquid).projectileIntroTimer.Value = 1000f;
                                            }
                                        }
                                        if (Monsters is DinoMonster)
                                        {
                                            if ((Monsters as DinoMonster).totalFireTime > 0)
                                            {
                                                (Monsters as DinoMonster).nextFireTime = 500;
                                                (Monsters as DinoMonster).totalFireTime = 3000;
                                            }
                                        }
                                        if (Monsters is Leaper)
                                        {
                                            if ((Monsters as Leaper).leapProgress.Value < 0.1f)
                                            {
                                                (Monsters as Leaper).leaping.Value = false;
                                            }
                                        }
                                        if (Monsters is Skeleton)
                                        {
                                            Monsters.Sprite.currentFrame = 0;
                                        }
                                        if (Monsters is Shooter)
                                        {
                                            (Monsters as Shooter).shooting.Value = false;
                                        }
                                    }
                                }
                            }
                            foreach (Potwory potwor in potwory)
                            {
                                if (potwor.monster is Duggy)
                                {
                                    potwor.monster.Sprite.currentFrame = potwor.info;
                                }
                            }
                        }
                        if (JustUnpaused)
                        {
                            foreach (GameLocation location in locations)
                            {
                                foreach (Character Monsters in location.characters)
                                {
                                    if (Monsters is Monster)
                                    {
                                        if (Monsters.Speed == 0)
                                        {
                                            Monsters.Speed = Convert.ToInt32(MonsterInfo(Monsters.Name)[10]);
                                            if (Monsters is Spiker)
                                            {
                                                Monsters.Speed = 14;
                                            }
                                            if (Monsters is GreenSlime || Monsters is SquidKid)
                                            {
                                                (Monsters as Monster).moveTowardPlayer(Convert.ToInt32(MonsterInfo(Monsters.Name)[9]));
                                            }
                                        }
                                    }
                                }
                                for (int k = location.TemporarySprites.Count - 1; k >= 0; k--)
                                {
                                    if (location.TemporarySprites[k].bombRadius > 0)
                                    {
                                        location.TemporarySprites[k].paused = false;
                                    }
                                }
                            }
                            foreach (Potwory potwor in potwory)
                            {
                                if (potwor.monster is DinoMonster)
                                {
                                    (potwor.monster as DinoMonster).totalFireTime = potwor.info + 500;
                                }
                                potwor.monster.Health = potwor.Health;
                            }
                            foreach (Farmer gracz in Game1.getOnlineFarmers())
                            {
                                gracz.temporarilyInvincible = false;
                                gracz.temporaryInvincibilityTimer = 0;
                            }
                            potwory.Clear();
                            locations.Clear();
                        }
                        if (SKTime && Game1.gameTimeInterval > 4000)
                        {
                            int SKCount = 0;
                            foreach (Farmer gracz in Game1.getOnlineFarmers())
                            {
                                if (gracz.currentLocation is MineShaft && (gracz.currentLocation as MineShaft).getMineArea() == 121)
                                {
                                    SKCount++;
                                }
                            }
                            Game1.gameTimeInterval -= 2000 * SKCount / Game1.getOnlineFarmers().Count;
                            timeinterval -= 2000 * SKCount / Game1.getOnlineFarmers().Count;
                            SKTime = false;
                        }
                        if (!SKTime && Game1.gameTimeInterval < 1000)
                        {
                            SKTime = true;
                        }
                    }
                }
                JustPaused = false;
                JustUnpaused = false;
            }
        }

        private static string[] MonsterInfo(string name)
        {
            if (name == "Haunted Skull")
            {
                return MonsterInfo("Lava Bat");
            }
            if (name == "Prismatic Slime")
            {
                return MonsterInfo("Green Slime");
            }
            if (name == "Stick Bug")
            {
                return MonsterInfo("Rock Crab");
            }
            return Game1.content.Load<Dictionary<string, string>>("Data\\Monsters")[name].Split(new char[]
            {
                '/'
            });
        }

        private bool ShouldTimePass()
        {
            foreach (PlayerList gracz in Gracze)
            {
                if (gracz.message != 1)
                {
                    return true;
                }
            }
            return false;
        }

        private void DrawFade(SpriteBatch b)
        {
            string text;
            switch (Game1.dayOfMonth % 7)
            {
                case 0:
                    text = Helper.Translation.Get("Sunday", tokens: new { day = Game1.dayOfMonth });
                    break;
                case 1:
                    text = Helper.Translation.Get("Monday", tokens: new { day = Game1.dayOfMonth });
                    break;
                case 2:
                    text = Helper.Translation.Get("Tuesday", tokens: new { day = Game1.dayOfMonth });
                    break;
                case 3:
                    text = Helper.Translation.Get("Wednesday", tokens: new { day = Game1.dayOfMonth });
                    break;
                case 4:
                    text = Helper.Translation.Get("Thursday", tokens: new { day = Game1.dayOfMonth });
                    break;
                case 5:
                    text = Helper.Translation.Get("Friday", tokens: new { day = Game1.dayOfMonth });
                    break;
                case 6:
                    text = Helper.Translation.Get("Saturday", tokens: new { day = Game1.dayOfMonth });
                    break;
                default:
                    text = "";
                    break;
            }
            Vector2 dayPosition = new((float)Math.Floor(187 - ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? Game1.smallFont : Game1.dialogueFont).MeasureString(text).X / 2), (float)Math.Floor(43 - ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? Game1.smallFont : Game1.dialogueFont).MeasureString(text).Y / 2));
            b.DrawString((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? Game1.smallFont : Game1.dialogueFont, text, Game1.dayTimeMoneyBox.position + dayPosition, textColor);
            string zeroPad = (Game1.timeOfDay % 100 == 0) ? "0" : "";
            string hours;
            if (!hourFormat)
            {
                hours = (Game1.timeOfDay / 100 % 24 == 0) ? "00" : string.Concat(Game1.timeOfDay < 1000 ? "0" : "", Game1.timeOfDay / 100 % 24);
            }
            else
            {
                hours = (Game1.timeOfDay / 100 % 12 == 0) ? "12" : string.Concat(Game1.timeOfDay / 100 % 12);
            }
            string Time = string.Concat(new object[]
            {
                hours,
                ":",
                Game1.timeOfDay % 100,
                zeroPad
            });
            if (hourFormat)
            {
                Time = Helper.Translation.Get((Game1.timeOfDay < 1200 || Game1.timeOfDay >= 2400) ? "AM" : "PM", tokens: new { time = Time });
            }
            Vector2 timePosition = new((float)Math.Floor(183 - ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? Game1.smallFont : Game1.dialogueFont).MeasureString(Time).X / 2), (float)Math.Floor(133.5f - ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? Game1.smallFont : Game1.dialogueFont).MeasureString(text).Y / 2));
            bool nofade = ShouldTimePass() || Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 2000.0 > 1000.0;
            b.DrawString((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? Game1.smallFont : Game1.dialogueFont, Time, Game1.dayTimeMoneyBox.position + timePosition, (Game1.timeOfDay >= 2400) ? Color.Red : (textColor * (nofade ? 1f : 0.5f)));
        }

        private void DrawPasek(SpriteBatch b)
        {
            int width = (int)(108 - (Game1.getOnlineFarmers().Count - 1) * 4) / Game1.getOnlineFarmers().Count;
            int i = 0;
            if (this.Config.UiInfoSuite)
            {
                b.Draw(PasekWithUIS, Game1.dayTimeMoneyBox.position + PasekPosition, null, Color.White, 0.0f, Vector2.Zero, 4, SpriteEffects.None, 0.99f);
            }
            b.Draw(Game1.options.zoomButtons ? PasekZoom : Pasek, Game1.dayTimeMoneyBox.position + PasekPosition + new Vector2(0, this.Config.UiInfoSuite ? 44 : 0), null, Color.White, 0.0f, Vector2.Zero, 4, SpriteEffects.None, 0.99f);
            foreach (PlayerList gracz in Gracze)
            {
                if (gracz.message != 1)
                {
                    b.Draw(Blue, Game1.dayTimeMoneyBox.position + PasekPosition + new Vector2(24, Game1.options.zoomButtons ? 52 : 24) + new Vector2(0, this.Config.UiInfoSuite ? 44 : 0) + new Vector2(i * (width + 4), 0), new Rectangle(0, 0, width, 24), Color.White, 0.0f, Vector2.Zero, 1, SpriteEffects.None, 0.99f);
                    if (Context.IsMainPlayer && gracz.message == -1)
                    {
                        b.Draw(Red, Game1.dayTimeMoneyBox.position + PasekPosition + new Vector2(24, Game1.options.zoomButtons ? 52 : 24) + new Vector2(0, this.Config.UiInfoSuite ? 44 : 0) + new Vector2(i * (width + 4), 0), new Rectangle(0, 0, width, 24), Color.White, 0.0f, Vector2.Zero, 1, SpriteEffects.None, 0.99f);
                    }
                }
                else
                {
                    b.Draw(Green, Game1.dayTimeMoneyBox.position + PasekPosition + new Vector2(24, Game1.options.zoomButtons ? 52 : 24) + new Vector2(0, this.Config.UiInfoSuite ? 44 : 0) + new Vector2(i * (width + 4), 0), new Rectangle(0, 0, width, 24), Color.White, 0.0f, Vector2.Zero, 1, SpriteEffects.None, 0.99f);
                }
                if (i != Game1.getOnlineFarmers().Count - 1)
                {
                    b.Draw(Black, Game1.dayTimeMoneyBox.position + PasekPosition + new Vector2(24, Game1.options.zoomButtons ? 52 : 24) + new Vector2(0, this.Config.UiInfoSuite ? 44 : 0) + new Vector2(i * (width + 4) + width, 0), new Rectangle(i * (width + 4) + width, 0, 4, 24), Color.White, 0.0f, Vector2.Zero, 1, SpriteEffects.None, 0.99f);
                }
                i++;
            }
        }
    }
}