using SDL2;
using System.Reflection;

namespace TeamDefense2
{
    enum MouseClickStages
    {
        noTouch,
        yesTouch,
        clickAndTouch
    }
    class Button : RenderObject
    {
        public Action? action1;
        public Action<string>? action2;
        public string parameter = string.Empty;
        public Button(int x, int y, Action<string> action, string parameter)
        {
            this.parameter = parameter;
            action2 = action;
            this.x = x;
            this.y = y;
            textureName = "button";
            TextureSet();
        }
        public Button(int x, int y, Action action)
        {
            action1 = action;
            this.x = x;
            this.y = y;
            textureName = "button";
            TextureSet();
        }
        public Button(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        public override void Logic()
        {
            base.Logic();
            if (mouseClick != MouseClickStages.noTouch) //If touching mouse
                sizeModifier = 1.1f;
            else
                sizeModifier = 1;
        }
        public override void Click()
        {
            if (action1 == null)
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                action2.Invoke(parameter);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            else
                action1.Invoke();
        }
    }
    class RoundButton : Button
    {
        bool clickable;
        float windup = 0; //This allows the clock to do a funny animation when clicked when a round is already in progress
        public RoundButton(int x, int y, Action action) : base(x, y)
        {
            action1 = action;
            this.x = x;
            this.y = y;
            textureName = "clock";
            TextureSet();
        }
        public override void Logic()
        {
            if (!Program.roundInProgress || (Program.waveHandlers.Count > 0 && Program.waveHandlers[0].TimeLeft < Program.waveHandlers[0].RoundLength / 4 && windup == 0))
            {
                clickable = true;
                if (Program.waveHandlers.Count > 0)
                    Program.roundBonus = (int)((Program.waveHandlers[0].TimeLeft / Program.waveHandlers[0].RoundLength) * 400);
            }
            else
                clickable = false;

            CheckForClick();
            if (mouseClick != MouseClickStages.noTouch && clickable) //If touching mouse and clickable
                sizeModifier = 1.1f;
            else
                sizeModifier = 1;

            if (windup != 0)
            {
                windup += Program.deltaTime / 8;
            }

        }
        public override void Click()
        {
            if (Program.roundInProgress)
            {
                if (clickable)
                    windup = 1;
            }
            else
            {
                base.Click();
            }
        }
        public override void Render(nint renderer)
        {
            if (!clickable) //Button can't be clicked so it's grey
                SDL.SDL_SetTextureColorMod(Program.entitiesTexture, 200, 200, 200);
            base.Render(renderer);
            if (!clickable) //No need to run this function so this probably saves like a very very very very very very very very very small amount of resources
                SDL.SDL_SetTextureColorMod(Program.entitiesTexture, 255, 255, 255);

            //Draw clockhand
            SDL.SDL_Rect srcrect = Program.entitiesTextureMap["clockHand"];
            SDL.SDL_Rect dstrect = new()
            {
                x = (int)x - 2,
                y = (int)y - srcrect.h,
                w = srcrect.w,
                h = srcrect.h
            };
            SDL.SDL_Point center = new()
            {
                x = srcrect.w / 2,
                y = srcrect.h
            };
            double angle;
            if (Program.waveHandlers.Count > 0)
                angle = Program.waveHandlers[0].elapsedTime / Program.waveHandlers[0].RoundLength * 360;
            else
                angle = 0;
            //LOGIC CODE IN RENDER CODE, THIS IS ONLY DONE BECAUSE THERES NO SIMPLE OTHER WAY TO DO THIS
            if (windup != 0)
            {
                angle += windup;
                if (angle >= 360)
                {
                    base.Click();
                    windup = 0;
                }
            }
            SDL.SDL_RenderCopyEx(renderer, Program.entitiesTexture, ref srcrect, ref dstrect, angle, ref center, SDL.SDL_RendererFlip.SDL_FLIP_NONE);
            if (clickable && Program.roundInProgress && Program.roundBonus > 0) //This is here for convenience
                Program.RenderText("RoundBonusCounter", $"${Convert.ToString(Program.roundBonus)}", 35, 85, Program.smallFont);
            if (mouseClick != MouseClickStages.noTouch)
            {
                SDL.SDL_Rect dstrect2 = new()
                {
                    x = 100,
                    y = 0,
                    w = 100,
                    h = Program.nextRoundTextureHeight
                };
                SDL.SDL_RenderCopy(renderer, Program.nextRoundTexture, (nint)null, ref dstrect2);
            }
        }
    }
    class LevelSelectButton : Button
    {
        readonly nint levelTexture;
        public LevelSelectButton(int x, int y, Action<string> action, string parameter) : base(x, y)
        {
            this.parameter = parameter;
            action2 = action;
            this.x = x;
            this.y = y;
            textureName = "levelSelectButton";
            TextureSet();
            //Get texture of level
            Program.LoadMap(parameter);
            levelTexture = SDL.SDL_CreateTexture(Program.renderer, SDL.SDL_PIXELFORMAT_RGB888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, 100, 70);
            Program.RenderBackground(false);
            SDL.SDL_SetRenderTarget(Program.renderer, levelTexture);
            SDL.SDL_RenderCopy(Program.renderer, Program.backgroundTexture, (nint)null, (nint)null);
            SDL.SDL_SetRenderTarget(Program.renderer, (nint)null);
        }
        public override void Render(nint renderer)
        {
            base.Render(renderer);
            SDL.SDL_Rect dstrect = new()
            {
                x = (int)x - 50,
                y = (int)y - 41,
                w = 100,
                h = 70
            };
            SDL.SDL_RenderCopy(renderer, levelTexture, (nint)null, ref dstrect);
        }
        public override void Click()
        {
            base.Click();
            Ui.ClearScreen();
            Program.StartGame();
        }
    }
    class ValueChangingButton : Button
    {
        readonly object changeTo;
        public ValueChangingButton(int x, int y, object to) : base(x, y)
        {
            changeTo = to;
            this.x = x;
            this.y = y;
            textureName = Tower.towerStats[(Type)to].textureName;
            TextureSet();
        }
        public override void Render(nint renderer)
        {
            base.Render(renderer);
            if (mouseClick != MouseClickStages.noTouch) //If touching mouse
            {
                Program.RenderText("Cost", $"Cost:{Tower.towerStats[(Type)changeTo].price}", 400, Program.mapScreenHeight + 50, Program.mainFont);
            }
        }
        public override void Click()
        {
            if (Program.money >= Tower.towerStats[(Type)changeTo].price)
                Program.selectedTowerType = (Type)changeTo;
        }
    }
    class UpgradeButton : Button
    {
        readonly Tower tower;
        readonly int path;
        int upgrade;
        int price;
        string desc;
        bool locked = false;
        public UpgradeButton(int x, int y, Tower tower, int path) : base(x, y)
        {
            this.tower = tower;
            this.path = path;
            this.x = x;
            this.y = y;

            textureName = "button";
            TextureSet();

            SetUp();
        }
        public override void Click()
        {
            if (!locked) //If this button is not locked
            {
                if (Program.money >= price)
                {
                    tower.BuyUpgrade(path, upgrade);
                    tower.moneySpent += price;
                    Program.money -= price;
                }
#pragma warning disable IDE0220 // Add explicit cast
                foreach (UpgradeButton upgradeButton in Program.ui.FindAll(x => x.GetType() == typeof(UpgradeButton)))
                    upgradeButton.SetUp(); //This must be done so the path locking stuff works
#pragma warning restore IDE0220 // Stupidass visual studio doesn't understand my code. Intellicode? More like I've already written this same joke in the source code of another project because it's so STUPID
            }
        }
        public override void Render(nint renderer)
        {
            base.Render(renderer);
            //Upgrade price and description
            if (!locked)
            {
                Program.RenderText($"Cost{path}", $"{price}", (int)x - 20, Program.mapScreenHeight + 30, Program.smallFont);
                Program.RenderText($"Desc{path}", $"{desc}", (int)x - 20, Program.mapScreenHeight + 50, Program.smallFont);
            }
        }
        /// <summary>
        /// Sets up upgrade, path, price and texture and whatever idk y'know what this does you don't need a summary, just read the code dumbass
        /// </summary>
        void SetUp()
        {
            for (int i = 0; i < 4; i++)
            {
                if (tower.upgrades[path, i] == false) //This checks what is the lowest upgrade on the path that hasn't been bought yet
                {
                    if (i > 1 && tower.upgrades[(path + 1) % 2, 2] == true) //If 3 upgrades have been bought on one path, a third one can't be bought on the other path
                        locked = true;
                    else
                    {
                        upgrade = i;
                        price = Tower.towerStats[tower.GetType()].upgradePrices[path, upgrade];
                        desc = Tower.upgradeDescriptions[tower.GetType()][path, upgrade];
                    }
                    break;
                }
                else if (i == 3) //If all upgrades for this path have been bought
                {
                    locked = true;
                    //break not needed since this is the last one anyway
                }
            }
        }
    }
    /// <summary>
    /// Static class which contains all menu loadouts
    /// </summary>
    static class Ui
    {
        public static void LevelSelect()
        {
            //List<string> maps = Directory.GetFiles("data/").ToList(); //Load non embedded maps
            List<string> maps = Assembly.GetExecutingAssembly().GetManifestResourceNames().ToList(); //Load embedded maps
            maps.RemoveAll(x => !x.EndsWith(".map")); //Remove all that doesn't end with ".map" //Only left with maps
            for (int i = 0; i < maps.Count; i++)
            {
                Program.ui.Add(new LevelSelectButton(20 + (i - (Convert.ToInt32(i > 3) * 4)) * 150 + 75, 159 + (Convert.ToInt32(i > 3) * 130) + 50, new(Program.LoadMap), maps[i]));
            }
        }
        public static void BuyMenu()
        {
            Program.selectedTower = null;
            ClearScreen(typeof(UpgradeButton));
            ClearScreen(typeof(Button));
            //Towers are 32px high, 100(panel height) - 32*2(2 tower rows) leaves 36px, if we want space above below and in-between towers (/3), 12px. And to center, 32/2 = 16
            int i = 0;
            foreach (Type tower in Tower.towerStats.Keys)
            {
                i++; //Used to be infor loop
                Program.ui.Add(new ValueChangingButton(400 + i * 44, Program.mapScreenHeight + 12 + 16, tower));
            }
        }
        public static void TowerMenu(Tower selectedTower)
        {
            //Damage counter
            Program.selectedTower = selectedTower;
            ClearScreen(typeof(ValueChangingButton));
            //Upgrade buttons
            for (int i = 0; i < 2; i++)
                Program.ui.Add(new UpgradeButton(200 + (i * 200), Program.mapScreenHeight + 16, selectedTower, i));
            //Other stuff
            Program.ui.Add(new Button(Program.screenWidth - 50, Program.screenHeight - 40, new Action(selectedTower.SwitchTargeting)));
            Program.ui.Add(new Button(Program.screenWidth - 50, Program.screenHeight - 10, new Action(selectedTower.Sell)));
        }
        /// <summary>
        /// Removes all ui objects
        /// </summary>
        public static void ClearScreen()
        {
            Program.ui.Clear();
        }
        /// <summary>
        /// Removes all ui objects of the specified type
        /// </summary>
        public static void ClearScreen(Type type)
        {
            Program.ui.RemoveAll(x => x.GetType() == type);
        }
    }
}
