using System.Drawing;

namespace TeamDefense2
{
    enum BehaviourId
    {
        Axe,
        Fire
    }
    class Projectile : RenderObject
    {
        readonly BehaviourId behaviourId;
        int hitCounter = 0; //How many hits?
        readonly int[] hitEnemies; //The size of this array is how many times a projectile can hit an enemy before dying, 1 means it can hit a single enemy before dying
        int age;
        float speed;
        readonly int damage;
        readonly Tower owner;
        public Projectile(float x, float y, double direction, int damage, int pierce, BehaviourId behaviourId, string textureName, Tower owner)
        {
            age = 0;
            this.x = x;
            this.y = y;
            this.direction = direction;
            this.textureName = textureName;
            RenderDirEqualDirection = false;
            TextureSet();
            //Projectile stuff
            this.owner = owner;
            speed = 0.1f;
            this.damage = damage;
            hitEnemies = new int[pierce]; //Piercing
            this.behaviourId = behaviourId;
            for (int i = 0; i < 100; i++)
                Move(speed);
        }
        public override void Logic()
        {
            age++;
            switch (behaviourId)
            {
                case BehaviourId.Axe:
                    {
                        renderDir += 0.5f * Program.deltaTime;
                        break;
                    }
                case BehaviourId.Fire:
                    {
                        if (age % 15 == 0)
                        {
                            if (speed > 0)
                                speed -= 0.001f;
                            else
                                Delete();
                        }
                        break;
                    }
            }
            if (x > Program.screenWidth || x + renderRect.w < 0 || y > Program.mapScreenHeight || y + renderRect.h < 0)
                Delete();
            Move(speed);
            Collision();
        }
        //Checks for collisions with bloons, and then handles them
        public void Collision()
        {
            foreach (Enemy enemy in Program.enemies.ToList())
            {
                Rectangle enemyHitbox = new Rectangle()
                {
                    X = (int)enemy.x,
                    Y = (int)enemy.y,
                    Width = enemy.renderRect.w,
                    Height = enemy.renderRect.h
                };
                if (!hitEnemies.Contains(enemy.id) && hitbox.IntersectsWith(enemyHitbox)) //Colliding and haven't already hit this enemy
                {
                    if (enemy.disquised) //Cannot damage disquised enemies until their disquise is off
                    {
                        if (owner.seeDisquise) //Cannot remove disquise unless the tower can see see disquises
                            enemy.Unmask();
                    }
                    else
                    {
                        enemy.health -= damage;
                        if (enemy.tier == EnemyTier.Nightmare) //Extra damage against nightmare tier enemies
                            enemy.health -= owner.nightmareDamage;

                        owner.damageDealt += damage; //Allow tower to track damage
                        if (hitCounter == hitEnemies.Length - 1)
                        {   //Pierce ran out :(
                            Delete();
                        }
                        else
                        {   //Still got piercing :)
                            hitEnemies[hitCounter] = enemy.id;
                            hitCounter++;
                        }
                    }
                    break;
                }
            }
        }
        public void Delete()
        {
            Program.projectiles.Remove(this);
        }
    }
}
