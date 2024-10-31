using SDL2;

namespace TeamDefense2
{
    enum Targeting
    {
        First,
        Last,
        Strong
    }
    /// <summary>
    /// Struct which contains information related to a tower, used for code outside the tower class such as the tower placing preview, which needs to know the towers'
    /// render id and radius in order to show it.
    /// </summary>
    readonly struct TowerStats(int price, string textureName, float radius, int size, int[,] upgradePrices)
    {
        public readonly int price = price;
        public readonly string textureName = textureName;
        public readonly float radius = radius;
        public readonly int size = size;
        public readonly int[,] upgradePrices = upgradePrices;
    }
    abstract class Tower : RenderObject
    {
        public static readonly Dictionary<Type, TowerStats> towerStats = new()
        {
                                        //Tower price, textureName, radius, and prices for the 4 upgrades of the 2 paths
            {typeof(Dwarf) , new TowerStats(200, "dwarf", 2.5f, 1, new int[2,4] { {80,120,650,2000}, {50,420,850,1500} }) }, //I hate this. It works. It's efficient. Tt doesn't take up that much space. But it looks terrible.
            {typeof(FlameThrower) , new TowerStats(250, "flameThrower", 1.5f, 1, new int[2,4] { {380,330,420,1300}, {220,400,650,1000} }) }, //Actually now that I've used it for a while, it ain't so bad
            {typeof(InfernoTower) , new TowerStats(1000, "infernoTower", 2.5f, 1, new int[2,4] { {350,550,650,1300}, {350,850,500,1000} }) }, //But probably still could be better
            {typeof(Mine) , new TowerStats(725, "mine", 0, 2, new int[2,4] { {300,500,700,2000}, {250,0,0,0} }) },
        };
        /// <summary>
        /// Descriptions of each upgrade, of each path, for each tower
        /// </summary>
        public static readonly Dictionary<Type, string[,]> upgradeDescriptions = new()
        {
            //3000 - 2500 = 500; 500/3000 = 16%

            //I just don't feel like writing code for unpacking these values from a seperate file, so they'll just be written here for now
            {typeof(Dwarf), new string[,] { /*Path 0*/{"increased attack rate", "increased attack rate", "increased attack rate", "increased attack rate\nand pierce" }, /*Path 1*/{"increased range", "increased range\nand damage", "throw two axes\nat once", "massively increased\ndamage and range.\nExtra nightmare damage" } } },
            {typeof(FlameThrower), new string[,] { /*Path 0*/{ "increased range\nand damage", "increased range", "increased damage\nand pierce\nExtra nightmare damage", "increased damage\nExtra nightmare damage" }, /*Path 1*/{"increased attack rate", "increased attack rate\nand pierce", "increased rate", "massively increased\nattack rate" } } },
            {typeof(InfernoTower), new string[,] { /*Path 0*/{"increased attack speed", "increased attack speed", "increased attack speed", "increased attack speed\nand adds second laser" }, /*Path 1*/{"increased range", "increased charge speed", "increased range", "increased range\nand charge speed" } } },
            {typeof(Mine), new string[,] { /*Path 0*/{"more money per\ngold chunk", "more money per\ngold chunk", "more money per\ngold chunk", "more money per\ngold chunk" }, /*Path 1*/{"increased amount of\ngold per round", "", "3", "4" } } },
        };
        public bool[,] upgrades = new bool[2, 4]; //2 paths with 3 upgrades
        //Stats
        public int size;
        public float radius; //Within how many tiles this tower can target enemies
        public float firingSpeed = 1000;
        public int damage = 1;
        public int pierce = 1;
        public bool seeDisquise = false; //Whether or not this tower can attack disquised enemies
        public int nightmareDamage = 0; //Extra damage against nightmare tier enemies

        public int damageDealt = 0;

        public bool firing = false; //Whether the tower is currently firing
        public float firingCooldown = 0; //Float for increased deltatime accuracy

        public BehaviourId projectileBehaviour;
        public string projectileTexture;

        public int moneySpent = 0; //Money spent on this tower

        public Targeting targetingMode = Targeting.First;
        public Enemy target;
        public const int targetingModes = 3;
        public Tower(int x, int y)
        {
            radius = towerStats[GetType()].radius;
            textureName = towerStats[GetType()].textureName;
            size = towerStats[GetType()].size;
            moneySpent += towerStats[GetType()].price;
            this.x = x;
            this.y = y;

            for (int i = 0; i < size; i++) //X
            {
                for (int j = 0; j < size; j++)//Y
                {
                    Program.map[(y - Program.halfTile) / Program.tileSize + (j % size), ((x - Program.halfTile) / Program.tileSize + (i % size)) * 2 + 1] = '2'; //Block tile pos
                }
            }
        }
        /* This is not what destructors should be used for
        ~Tower()
        {
            Program.map[((int)y - Program.halfTile) / Program.tileSize, ((int)x - Program.halfTile) / Program.tileSize * 2 + 1] = '0'; //Remove block tile pos
        }
        */
        public virtual void BuyUpgrade(int path, int upgrade)
        {
            upgrades[path, upgrade] = true;
        }
        public override void Logic()
        {
            base.Logic();
            //Point at enemy
            PointInRadius(targetingMode);
            //Fire
            if (firingCooldown < 0) //This is seperate so firingCooldown goes down correctly // < not == because deltatime
            {
                if (firing)
                {
                    firingCooldown = firingSpeed;
                    Fire();
                }
            }
            else
                firingCooldown -= Program.deltaTime; //This should only really kick in at extreme lag
        }
        public virtual void Fire()
        {
            Program.projectiles.Add(new Projectile(x, y, direction, damage, pierce, projectileBehaviour, projectileTexture, this));
        }
        public override void Click()
        {
            Ui.TowerMenu(this);
        }
        public override void Render(nint renderer) //We use the base rendering function as normal, however we also need to add the radius circle
        {
            base.Render(renderer);
            if (mouseClick != MouseClickStages.noTouch && Program.selectedTowerType == null) //Only draw radius circle if touching mouse and not currently placing a new tower, which's radius is also shown
            {
                SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 0);
                Program.RenderDrawCircle(renderer, (int)x, (int)y, (int)(radius * Program.tileSize));
            }
        }
        /// <summary>
        /// Points to the first enemy within radius that can be targeted and starts firing. Basically bloons "First" targeting option
        /// </summary>
        public void PointInRadius(Targeting targeting)
        {
            firing = false;
            //Can't think of a better way to do this
            switch (targeting)
            {
                case Targeting.First:
                    {
                        for (int i = 0; i < Program.enemies.Count; i++)
                        {
                            Enemy enemy = Program.enemies[i];
                            if (CanAttack(enemy))
                            {
                                PointTowards((int)enemy.x, (int)enemy.y);
                                firing = true;
                                target = enemy;
                                break;
                            }
                        }
                        break;
                    }
                case Targeting.Last:
                    {
                        for (int i = Program.enemies.Count - 1; i > -1; i--)
                        {
                            Enemy enemy = Program.enemies[i];
                            if (CanAttack(enemy))
                            {
                                PointTowards((int)enemy.x, (int)enemy.y);
                                firing = true;
                                target = enemy;
                                break;
                            }
                        }
                        break;
                    }
                case Targeting.Strong: //Target strongest enemy
                    {
                        if (Program.enemies.Count > 0)
                        {
                            Enemy? strongestEnemy = null; //Needs to have some default value
                            for (int i = 0; i < Program.enemies.Count; i++)
                            {
                                Enemy enemy = Program.enemies[i];
                                if ((strongestEnemy == null || enemy.strength > strongestEnemy.strength) && CanAttack(enemy))
                                {
                                    strongestEnemy = enemy;
                                }
                            }
                            //Fire towards strongest enemy
                            if (strongestEnemy != null)
                            {
                                firing = true;
                                PointTowards((int)strongestEnemy.x, (int)strongestEnemy.y);
                                target = strongestEnemy;
                            }
                        }
                        break;
                    }
            }

            bool CanAttack(Enemy enemy) //Enemy within range that can be seen
            {
                return Distance(x, y, enemy.x, enemy.y) < radius * Program.tileSize && (!enemy.disquised || enemy.disquised && seeDisquise);
            }
        }
        /// <summary>
        /// Switches targeting mode. This function is used by buttons
        /// </summary>
        public void SwitchTargeting()
        {
            targetingMode = (Targeting)(((int)targetingMode + 1) % targetingModes);
        }
        /// <summary>
        /// Sell tower
        /// </summary>
        public void Sell()
        {
            //Clear build blockers
            for (int i = 0; i < size; i++) //X
            {
                for (int j = 0; j < size; j++)//Y
                {
                    Program.map[((int)y - Program.halfTile) / Program.tileSize + (j % size), (((int)x - Program.halfTile) / Program.tileSize + (i % size)) * 2 + 1] = '0'; //Unblock tile pos
                }
            }
            //Money
            Program.money += moneySpent/2;
            //Rest
            Program.towers.Remove(this);
            Ui.BuyMenu(); //Remove tower menu when sold
            
        }
    }
    class Dwarf : Tower
    {
        bool TwoAxes //This is just a shortcut for checking a value in upgrades[,]
        {
            get
            {
                return upgrades[1, 2];
            }
        }
        public Dwarf(int x, int y) : base(x, y)
        {
            damage = 3;
            firingSpeed = 3000;
            projectileBehaviour = BehaviourId.Axe;
            projectileTexture = "axe";
            seeDisquise = true;

            RenderDirection = true;
            TextureSet();
        }
        public override void Fire()
        {
            if (TwoAxes) //Pretty self explanatory
            {
                Program.projectiles.Add(new Projectile(x, y, direction + 0.261799, damage, pierce, projectileBehaviour, projectileTexture, this));
                Program.projectiles.Add(new Projectile(x, y, direction - 0.261799, damage, pierce, projectileBehaviour, projectileTexture, this));
            }
            else
                base.Fire();
        }
        public override void BuyUpgrade(int path, int upgrade)
        {
            base.BuyUpgrade(path, upgrade);
            if (path == 0)
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            firingSpeed -= 600;
                            break;
                        }
                    case 1:
                        {
                            firingSpeed -= 600;
                            break;
                        }
                    case 2:
                        {
                            firingSpeed -= 700;
                            break;
                        }
                    case 3:
                        {
                            pierce += 2;
                            firingSpeed -= 600;
                            break;
                        }
                }
            }
            else
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            radius += 0.5f;
                            break;
                        }
                    case 1:
                        {
                            radius += 0.5f;
                            damage += 3;
                            break;
                        }
                    case 2:
                        {
                            //Two axes
                            break;
                        }
                    case 3:
                        {
                            radius += 0.5f;
                            damage += 10;
                            nightmareDamage += 5;
                            break;
                        }
                }
            }
        }
    }
    class InfernoTower : Tower
    {
        //First laser
        int charge = 0; //The longer the beam has been on the same enemy, the more damage it does
        int targetMem; //Remembers the last target for ↑
                       //Second laser
        Enemy? target2; //Only needed for rendering the laser
        int charge2 = 0; //Charge but for second enemy
        int targetMem2; //Remembers the SECOND target
        float firingCooldown2 = 0; //You get it
        bool firing2; //This is needed for rendering the laser
                      //Non laser specific variables
        bool TwoLasers //This is just a shortcut for checking a value in upgrades[,]
        {
            get
            {
                return upgrades[0, 3];
            }
        }
        int chargeSpeed = 1; //How fast the charge increases

        public InfernoTower(int x, int y) : base(x, y)
        {
            firingSpeed = 1000;
            //Damage is based on charge

            RenderDirection = false;
            TextureSet();
        }
        public override void Logic()
        {
            CheckForClick(); //Using base.Logic() causes crash for some reason

            PointInRadius(targetingMode);

            //First laser cannot have same target as second laser
            if (target == target2)
                firing = false;
            //Fire first/only laser
            if (firing)
            {
                if (firingCooldown < 0)
                {
                    //Charge
                    if (targetMem == target.id)
                    {
                        charge += chargeSpeed;
                    }
                    else //Target has changed or target was null
                    {
                        targetMem = target.id;
                        charge = 1;
                    }

                    firingCooldown = firingSpeed; //Still has firing cooldown even if it's a laser beam
                    target.health -= charge;
                    if (target.tier == EnemyTier.Nightmare)
                        target.health -= charge/3;
                    damageDealt += charge; //In other towers the projectile deals with this
                }
                firingCooldown -= Program.deltaTime;
            }
            else
            {
                charge = 0;
            }
            //Fire second laser if it exists
            if (TwoLasers)
            {
                Enemy targetTemp = target;
                bool firingMem = firing; //Remember if the first laser is firing
                Targeting targetingMode2 = Targeting.First;
                while (targetingMode2 == targetingMode) //Switch targeting modes until it's not the same as the first laser
                {
                    targetingMode2 = (Targeting)(((int)targetingMode2 + 1) % targetingModes);
                }
                PointInRadius(targetingMode2); //We don't need target2 since this changes the target constantly anyways, we just need targetMem2 to remember the old target
                target2 = target;
                target = targetTemp;
                firing2 = firing;
                firing = firingMem;

                //Second laser cannot have same target as first laser
                if (target2 == target)
                {
                    firing2 = false;
                    target2 = null;
                }

                if (firing2) //We don't need firing2 since firing gets changed every time PointInRadius() is run
                {
                    if (firingCooldown2 < 0)
                    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        //Charge
                        if (targetMem2 == target2.id)
                        {
                            charge2 += chargeSpeed;
                        }
                        else //Target has changed or target was null
                        {
                            targetMem2 = target2.id;
                            charge2 = 1;
                        }

                        firingCooldown2 = firingSpeed; //Still has firing cooldown even if it's a laser beam
                        target2.health -= charge2;
                        if (target2.tier == EnemyTier.Nightmare)
                            target2.health -= charge2 / 3;
                        damageDealt += charge2;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    }
                    firingCooldown2 -= Program.deltaTime;
                }
                else
                {
                    charge2 = 0;
                }
            }
        }
        public override void Render(nint renderer) //We use the base rendering function as normal, however we also need to add the line
        {
            base.Render(renderer);
            if (firing)
            {
                RenderLaser((int)target.x, (int)target.y);
            }
            if (firing2)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                RenderLaser((int)target2.x, (int)target2.y);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            void RenderLaser(int targetX, int targetY)
            {
                SDL.SDL_SetRenderDrawColor(renderer, 255, 0, 0, 255);
                SDL.SDL_RenderDrawLine(renderer, (int)x, (int)y, targetX, targetY);
                SDL.SDL_SetRenderDrawColor(renderer, 255, (byte)Math.Clamp(100 - charge, 0, 100), 0, 255); //Gets redder the more charged it is
                SDL.SDL_RenderDrawLine(renderer, (int)x, (int)y + 2, targetX, targetY);
                SDL.SDL_SetRenderDrawColor(renderer, 255, (byte)Math.Clamp(100 - charge, 0, 100), 0, 255);
                SDL.SDL_RenderDrawLine(renderer, (int)x, (int)y - 2, targetX, targetY);
            }
        }
        public override void BuyUpgrade(int path, int upgrade)
        {
            base.BuyUpgrade(path, upgrade);
            if (path == 0)
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            firingSpeed -= 200;
                            break;
                        }
                    case 1:
                        {
                            firingSpeed -= 200;
                            break;
                        }
                    case 2:
                        {
                            firingSpeed -= 200;
                            break;
                        }
                    case 3: //Two lasers
                        {
                            firingSpeed -= 100;
                            break;
                        }
                }
            }
            else
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            radius += 0.5f;
                            break;
                        }
                    case 1:
                        {
                            chargeSpeed += 1;
                            break;
                        }
                    case 2:
                        {
                            radius += 0.5f;
                            break;
                        }
                    case 3:
                        {
                            chargeSpeed += 1;
                            radius += 0.5f;
                            break;
                        }
                }
            }
        }
    }
    class FlameThrower : Tower
    {
        public FlameThrower(int x, int y) : base(x, y)
        {
            pierce = 5;
            damage = 1;
            firingSpeed = 2200;
            projectileBehaviour = BehaviourId.Fire;
            projectileTexture = "fire";

            RenderDirection = true;
            TextureSet();
        }
        public override void BuyUpgrade(int path, int upgrade)
        {
            base.BuyUpgrade(path, upgrade);
            if (path == 0)
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            damage += 1;
                            radius += 0.5f;
                            break;
                        }
                    case 1:
                        {
                            radius += 0.5f;
                            break;
                        }
                    case 2:
                        {
                            damage += 1;
                            nightmareDamage += 3;
                            pierce += 4;
                            break;
                        }
                    case 3:
                        {
                            damage += 1;
                            nightmareDamage += 4;
                            //Buff nearby flamethrowers?

                            break;
                        }
                }
            }
            else
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            firingSpeed -= 200;
                            break;
                        }
                    case 1:
                        {
                            firingSpeed -= 300;
                            pierce += 3;
                            break;
                        }
                    case 2:
                        {
                            //Afterburn?
                            firingSpeed -= 400;
                            break;
                        }
                    case 3:
                        {
                            firingSpeed -= 600;
                            break;
                        }
                }
            }
        }
    }
    class MineWaveTracker(float roundLength, int round, int cashDropsPerRound) //Needs to be class because reference shit in c#
    {
        public readonly float roundLength = roundLength;
        public readonly int round = round;
        public int cashDrops = 0;
        public float firingCooldown = roundLength / cashDropsPerRound;
    }
    class Mine : Tower
    {
        int cashDropsPerRound = 4;
        int moneyPerDrop = 10;
        int roundMemory;
        readonly List<MineWaveTracker> waveTrackers = [];
        public Mine(int x, int y) : base(x, y)
        {
            RenderDirection = false;
            TextureSet();
        }
        public override void Logic()
        {
            CheckForClick();
            //Fire
            if (roundMemory != Program.round)
            {
                roundMemory = Program.round;
                waveTrackers.Add(new MineWaveTracker(WaveHandler.waves[roundMemory].length, roundMemory, cashDropsPerRound)); //Add wave to waveTrackers
            }
            for (int i = 0; i < waveTrackers.Count; i++)
            {
                if (waveTrackers[i].firingCooldown < 0)
                {
                    if (waveTrackers[i].cashDrops < cashDropsPerRound)
                    {
                        //Cashdrop
                        waveTrackers[i].firingCooldown = waveTrackers[i].roundLength / cashDropsPerRound;
                        Program.money += moneyPerDrop;
                        waveTrackers[i].cashDrops++;
                        damageDealt += moneyPerDrop;
                    }
                    else
                    {
                        waveTrackers.Remove(waveTrackers[i]);
                        i--;
                    }
                }
                else
                    waveTrackers[i].firingCooldown -= Program.deltaTime;
            }
        }
        public override void BuyUpgrade(int path, int upgrade)
        {
            base.BuyUpgrade(path, upgrade);
            if (path == 0)
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            moneyPerDrop += 2; //12
                            break;
                        }
                    case 1:
                        {
                            moneyPerDrop += 4; //14
                            break;
                        }
                    case 2:
                        {
                            moneyPerDrop += 6;
                            break;
                        }
                    case 3:
                        {
                            moneyPerDrop += 8;
                            break;
                        }
                }
            }
            else
            {
                switch (upgrade)
                {
                    case 0:
                        {
                            cashDropsPerRound += 1;
                            break;
                        }
                    case 1:
                        {
                            break;
                        }
                    case 2:
                        {
                            break;
                        }
                    case 3:
                        {
                            break;
                        }
                }
            }
        }
    }
}
