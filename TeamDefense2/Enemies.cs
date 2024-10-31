using System.Drawing;

namespace TeamDefense2
{
    enum EnemyTier
    {
        Vivid,      //Normal/Strong enemies
        Haunted,    //Fast enemies
        Nightmare,  //Strong and fast enemies
        Disquised,  //Disquised enemies
        Eidolon,    //Boss enemies
    }
    class Enemy : RenderObject
    {
        public const int enemyTypes = 11;//Used somewhere
        //Logic
        static int idCounter = 0;
        public readonly int id; //Unique identifier for every enemy
        int pathIndex = 0; // At which node along the path the enemy is at
        readonly int path; //Which path is this enemy on
        float speed = 0.015f;
        public int health;
        public float progress = 0; //How far along the track the enemy is
        public bool disquised = false;
        public int strength; //How strong an enemy is? Used for strong targeting option
        public EnemyTier tier;
        int value; //Damage and money
        int children;
        int childEnemyId;

        readonly WaveHandler waveHandler; //WaveHandler to which to report spawning and death

        readonly int enemyId;
        public Enemy(int enemyId, float x, float y, double direction, int pathIndex, int path, float progress, WaveHandler waveHandler)
        {
            idCounter++;
            id = idCounter;
            this.enemyId = enemyId;

            this.waveHandler = waveHandler;
            this.x = x;
            this.y = y;
            this.pathIndex = pathIndex;
            this.direction = direction;
            this.progress = progress;
            //Enemy stuff
            EnemyStuff();
            //Finishing touches
            TextureSet();
        }
        public Enemy(int enemyId, WaveHandler waveHandler)
        {
            idCounter++;
            id = idCounter;
            this.enemyId = enemyId;
            if (Program.pathRule == PathRule.AllPaths) //Enemies spawns alternate between every enemy
            {
                path = id % Program.mapPaths.Length;
            }
            else//Enemies spawns alternate between every round
            {
                path = waveHandler.round % Program.mapPaths.Length;
            }

            this.waveHandler = waveHandler;
            x = Program.mapPaths[path].path[pathIndex].X;
            y = Program.mapPaths[path].path[pathIndex].Y;
            PointNextSpot();
            //Enemy stuff
            EnemyStuff();
            //Finishing touches
            TextureSet();
        }
        /// <summary>
        /// Stuff which needs to be in all constructors but which I don't want to copy and paste constantly
        /// </summary>
        void EnemyStuff()
        {
            waveHandler.enemiesAlive++;
            textureName = $"enemy{enemyId}";
            switch (enemyId)
            {
                case 0:
                    {
                        tier = EnemyTier.Vivid;
                        strength = 0;
                        value = 4;
                        health = 6;
                        break;
                    }
                case 1:
                    {
                        tier = EnemyTier.Vivid;
                        strength = 1;
                        value = 4;
                        health = 12;
                        break;
                    }
                case 2:
                    {
                        tier = EnemyTier.Vivid;
                        strength = 2;
                        value = 5;
                        health = 20;
                        break;
                    }
                case 3:
                    {
                        tier = EnemyTier.Haunted;
                        strength = 3;
                        value = 6;
                        health = 16;
                        speed = 0.02f;
                        break;
                    }
                case 4:
                    {
                        tier = EnemyTier.Haunted;
                        strength = 4;
                        value = 8;
                        health = 20;
                        speed = 0.025f;
                        break;
                    }
                case 5: //Ceramic
                    {
                        tier = EnemyTier.Nightmare;
                        strength = 5;
                        value = 4; //Also spawns two smaller enemies
                        health = 40;
                        speed = 0.035f;

                        children = 2;
                        childEnemyId = 4;
                        break;
                    }
                case 6: //MOAB
                    {
                        tier = EnemyTier.Eidolon;
                        strength = 10; //This is significantly bigger just to place this above all non moab-class enemies
                        value = 100;
                        health = 250;
                        speed = 0.01f;

                        children = 3;
                        childEnemyId = 5;
                        break;
                    }
                case 7: //Camo
                    {
                        tier = EnemyTier.Disquised;
                        strength = 4;
                        value = 8;
                        health = 16;
                        speed = 0.02f;
                        disquised = true;
                        break;
                    }
                case 8: //Camo ceramic
                    {
                        tier = EnemyTier.Disquised;
                        strength = 6;
                        value = 4;
                        health = 35; //A bit less health
                        speed = 0.02f; //Speed jumps up when disquise is removed
                        disquised = true;

                        children = 2; //Spawns two camos
                        childEnemyId = 7;
                        break;
                    }
                //9 does not exist because FUCKING NEWLINE
                case 10: //Super ceramic (not in the sense of replacing ceramics, these are just stronger enemies)
                    {
                        tier = EnemyTier.Nightmare;
                        strength = 7;
                        value = 4;
                        health = 50;
                        speed = 0.03f;

                        children = 4;
                        childEnemyId = 5;
                        break;
                    }
            }
        }
        /// <summary>
        /// Points towards the next spot
        /// </summary>
        void PointNextSpot()
        {
            pathIndex++;
            PointTowards(Program.mapPaths[path].path[pathIndex].X, Program.mapPaths[path].path[pathIndex].Y);
        }
        public override void Logic()
        {
            if (health <= 0)
                Die();
            Move(speed);
            progress += speed * Program.deltaTime; //Not sure if deltatime is really necessary here
            Point destination = Program.mapPaths[path].path[pathIndex];
            if (Math.Abs(destination.X - x) < 1 && Math.Abs(destination.Y - y) < 1)
            {
                if (pathIndex == Program.mapPaths[path].path.Length - 1) //If we've reached the end of the path
                {
                    GotPast();
                }
                else
                    PointNextSpot();
            }
        }
        /// <summary>
        /// Enemy dies
        /// </summary>
        public void Die()
        {
            if (children > 0) //Spawn children
            {
                for (int i = 0; i < children; i++)
                    Program.enemies.Add(new Enemy(childEnemyId, x, y, direction, pathIndex, path, progress, waveHandler));
                //Move one of the children forward so they aren't on top of eachother completely
                Enemy child = Program.enemies.Last();
                for (int i = 0; i < 500; i++) //Move forward 500 times... yeah I know, it sound like a lot but in practice it isn't actually much
                    child.Logic();
            }
            Program.money += value;
            waveHandler.enemiesAlive--;
            Program.enemies.Remove(this);
        }
        /// <summary>
        /// Enemy made it to the end
        /// </summary>
        public void GotPast()
        {
            Program.health -= value;
            waveHandler.enemiesAlive--;
            Program.enemies.Remove(this);
        }
        /// <summary>
        /// Unmask a hidden enemy
        /// </summary>
        public void Unmask()
        {
            switch (enemyId)
            {
                case 7: //Camo
                    {
                        disquised = false;
                        speed = 0.025f;
                        textureName = "enemy7-0";
                        TextureSet();
                        break;
                    }
                case 8: //Camo ceramic
                    {
                        disquised = false;
                        speed = 0.035f;
                        textureName = "enemy8-0";
                        TextureSet();
                        break;
                    }
            }
        }
    }
}
