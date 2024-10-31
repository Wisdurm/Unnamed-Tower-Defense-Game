namespace TeamDefense2
{
    class WaveHandler(int round)
    {
        public float TimeLeft
        {
            get
            {
                return RoundLength - elapsedTime;
            }
        }
        public float RoundLength
        {
            get
            {
                return wave.length;
            }
        }
        public readonly int round = round;
        public float elapsedTime = 0; //Track time WaveHandler has been active
        public int enemiesAlive; //Track how many enemies spawned are still alive

        public bool spawningFinished = false;
        public bool finished = false;
        int index = 0;
        float delayTimer = 0f;
        public static readonly Wave[] waves;
        readonly Wave wave = waves[round];
        static WaveHandler()
        {
            //Jagged array since rounds aren't all the same lenght
            char[][] wavesChar = Program.LoadEmbeddedResource("rounds.dat").Split('\n').Select(item => item.ToArray()).ToArray(); //String array to 2d char array
            waves = new Wave[wavesChar.GetLength(0)];
            for (int i = 0; i < waves.GetLength(0); i++)
            {
                float length = 0;
                WaveObject[] wave = new WaveObject[wavesChar[i].Length / 2];
                int enemyId = 0;
                for (int j = 0; j < wavesChar[i].Length; j++) //Parse char array into WaveObject array
                {
                    if (j % 2 == 0)
                        enemyId = wavesChar[i][j] - 1; //-1 since unicode 0 doesn't work
                    else
                    {
                        int delay = wavesChar[i][j] - 1;
                        wave[j / 2] = new WaveObject(enemyId, delay);

                        //Ignore the next chain of comments, I changed my mind on the fix
                        //Ignore comments after this one, if the length is zero, then somewhere else in this project that causes a dividebyzero problem.
                        //Not exception though, it just causes the clock hand to not get drawn. And guess what happens if you click it? It sets your cash to -2147483648.
                        //What fun bug  //DON'T ignore this -> //Final delay shouldn't be counted since it doesn't do literally anything //-2 not -1 because of newline (read the next comments)
                        if (j < wavesChar[i].Length - 2)
                            length += delay;

                    }
                }
                if (length == 0)
                    length += 1000; //Read the stuff above for explanation as to why length can't be zero
                waves[i] = new Wave(wave, length); //Add finished wave to waves

                //Fun fact, the last item of each wave in wavesChar is a newline character, but since the character in question is interpreted as an enemyId and no
                //delay number comes after, it doesn't matter and the game works fine.
            }
        }
        /*
        public void NextRound(int round)
        {
            wave = waves[round];
            finished = false;
            index = 0;
            delayTimer = 0;
        }
        */
        public void Logic()
        {
            if (!spawningFinished)
            {
                elapsedTime += Program.deltaTime;
                if (delayTimer <= 0) //Spawn enemy
                {
                    if (index == wave.enemies.Length - 1)
                        spawningFinished = true;
                    Program.enemies.Add(new Enemy(wave.enemies[index].enemyId, this));
                    delayTimer = wave.enemies[index].delay;
                    index++;
                }
                else
                {
                    delayTimer -= Program.deltaTime;
                }
            }
            else if (enemiesAlive == 0) //If this wavehandler is finished spawning enemies, and all of those enemies are dead, it's finished
            {
                finished = true;
            }
        }
    }
    readonly struct WaveObject(int enemyId, int delay)
    {
        public readonly int enemyId = enemyId; //Enemy to spawn
        public readonly int delay = delay;   //Delay before spawning next enemy
    }
    readonly struct Wave(WaveObject[] waveObjects, float length)
    {
        public readonly WaveObject[] enemies = waveObjects;
        public readonly float length = length;
    }
}
