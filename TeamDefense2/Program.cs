using SDL2;
using System.Drawing;
using System.Reflection;
using System.Text.Json;

namespace TeamDefense2
{
    enum PathRule
    {
        AllPaths,
        Alternating
    }
    struct MapPath(Point[] points)
    {
        public Point[] path = points;
        public readonly MapPathEdit ToMapPathEdit()
        {
            return new MapPathEdit(path);
        }
    }
    struct MapPathEdit
    {
        public readonly List<Point> path;
        public MapPathEdit(Point[] points)
        {
            path = points.ToList();
        }
        public MapPathEdit()
        {
            path = [];
        }
    }
    static class Program
    {
        //Engine
        static nint window;
        public static nint renderer;
        //Data
        public const int tileSize = 32;
        public const int halfTile = tileSize / 2; //Just saves some time
        const int tilemapWidth = 5; //How many tiles wide
        const int mapWidth = 20; //How many tiles wide is the map
        const int mapHeight = 14; //How many tiles high is the map
        public static char[,] map; //The map
        //Note about map:
        //While the y axis works normally, whenever you have to use something from the x axis, you always have to times the x by two.
        //This is because map not only stores the tiles, but also tile restrictions, as in whether or not you can place something on it.
        //The "restrictions" are always next to the tiles, meaning that to get the restriction at x, you would do x*2+1
        public static MapPath[] mapPaths; //An array containing the path of the map
        static List<MapPathEdit> mapPathEdit = []; //An array where the path of the map is stored while it's being edited
        public static PathRule pathRule;
        static string mapName = string.Empty; //Currently loaded map path, not name, despite the variable name
        const int unicodeMargin = 31; //Since the first 30ish unicode symbols are control characters, let's not use them... believe me, I tried...
        //*Textures
        static nint tilemapTexture;
        static nint blockedTexture;//This has it's own texture since it's transparent unlike other tiles
        public static nint nextRoundTexture; //Texture which has info about the next round
        public static int nextRoundTextureHeight;
        public static nint entitiesTexture;//Enemies and towers
        public static readonly Dictionary<string, SDL.SDL_Rect> entitiesTextureMap = [];
        static readonly Dictionary<string, TextRender> cachedText = []; //This stores all text renders, so that they are reused as much as possible
        //*Audio
        static nint bellDongWav;
        //*Misc
        public static nint mainFont;
        public static nint smallFont;
        //Render
        public static nint backgroundTexture; //The reason this texture is in render is because this isn't from a file, it isn't "data", it's something that's constructed mid-run
        public const int mapScreenHeight = 448; //How much of the screen height is taken up by the map, required for some calculations. The bottom panel is 100px tall
        public const int screenWidth = 640; //Seperate from the actual size of the window
        public const int screenHeight = 548;
        static int windowWidth = 640;
        static int windowHeight = 548;

        static bool renderHitboxes = false;
        //*Memory
        static nint render; //The final render, which is then strechthed to screen size
        //Logic
        public static float deltaTime = 0f;
        public static List<Enemy> enemies = [];
        public static List<Tower> towers = [];
        public static List<Projectile> projectiles = [];
        public static List<Button> ui = [];

        static bool inEditor;
        static bool inGame;

        //*Editor
        public static int currentTile = 0;
        static char currentBlock; //Used to keep track of what to change blockers to when holding middle mouse down
        static int currentPath = 0; //What path to edit
        //*In game
        static bool autoPlay = false;

        public static bool roundInProgress = false;

        public static int round;
        public static int money;
        public static int health;
        public static Tower? selectedTower = null;
        public static Type? selectedTowerType = null;
        public static List<WaveHandler> waveHandlers = []; //This is a list so that the game can handle multiple waves at once

        public static int roundBonus; //This just happens to be used by two seperate classes so it's a public variable because I don't want to copy and paste the entire formula repeatedly
        //*Input
        public static int mouseX;
        public static int mouseY;
        public static bool leftMouseDown = false;
        static bool middleMouseDown = false;
        static bool shiftDown = false;
        static void Main()
        {
            //Initialize SDL
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) != 0)
            {
                Console.WriteLine($"SDL not initialized. Error {SDL.SDL_GetError()}");
            }
            SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);
            //Create 
            window = SDL.SDL_CreateWindow("TeamDefense2", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, windowWidth, windowHeight, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (window == IntPtr.Zero)
            {
                Console.WriteLine($"Unable to create window. Error {SDL.SDL_GetError()}");
            }
            //Initialize renderer
            renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            //Load images
            LoadTexture("data/mapTexture.png", ref tilemapTexture);
            LoadTexture("data/blocked.png", ref blockedTexture);
            LoadTexture("data/entitiesTexture.png", ref entitiesTexture);
            //Load fonts
            SDL_ttf.TTF_Init();
            mainFont = SDL_ttf.TTF_OpenFont("data/Sitka.ttc", 35); //Load font
            smallFont = SDL_ttf.TTF_OpenFont("data/Sitka.ttc", 15); //Load font
            //Load audio
            if (SDL_mixer.Mix_OpenAudio(44100, SDL_mixer.MIX_DEFAULT_FORMAT, 2, 2048) < 0)
            {
                Console.WriteLine($"SDL_mixer not initialized. Error {SDL_mixer.Mix_GetError()}");
            }
            bellDongWav = SDL_mixer.Mix_LoadWAV("data/Bell.wav");
            //Set up render
            render = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGB888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, screenWidth, screenHeight); //Init render
            //Entity texture map
            {
                string[] entitiesTextureMapInit = LoadEmbeddedResource("entitiesTexture.json").Split('\n');
                for (int i = 0; i < entitiesTextureMapInit.Length; i++)
                {
                    KeyValuePair<string, SRect> keyValuePair = JsonSerializer.Deserialize<KeyValuePair<string, SRect>>(entitiesTextureMapInit[i]);
                    entitiesTextureMap.Add(keyValuePair.Key, keyValuePair.Value.ToSDL_Rect());
                }
            }
            //Start
            Ui.LevelSelect();
            bool running = true;
            //Deltatime stuff
            DateTime currentTime;
            DateTime lastTime = DateTime.Now;
            while (running)
            {
                //Input
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
                {
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT:
                            {
                                running = false;
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEMOTION:
                            {
                                mouseX = (int)(e.motion.x * (screenWidth / Convert.ToDouble(windowWidth))); //Division rounding my HATED
                                mouseY = (int)(e.motion.y * (screenHeight / Convert.ToDouble(windowHeight)));
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                            {
                                if (inEditor)
                                {
                                    if (e.wheel.y > 0) //Scroll up //Maybe???
                                    {
                                        currentTile++;
                                    }
                                    else if (e.wheel.y < 0) //Scroll down
                                    {
                                        currentTile--;
                                    }
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                            {
                                switch (e.button.button)
                                {
                                    case (byte)SDL.SDL_BUTTON_LEFT:
                                        {
                                            if (inGame)
                                            {
                                                if (mouseY < mapScreenHeight)
                                                {
                                                    if (selectedTowerType != null) //Buy tower
                                                    {
                                                        int x = SnapToGrid(mouseX);
                                                        int y = SnapToGrid(mouseY);
                                                        int requiredMoney = Tower.towerStats[selectedTowerType].price;
                                                        int size = Tower.towerStats[selectedTowerType].size;

                                                        bool canBuild = true;
                                                        for (int i = 0; i < size; i++) //X
                                                        {
                                                            for (int j = 0; j < size; j++)//Y
                                                            {
                                                                try
                                                                {
                                                                    if (map[(y / tileSize) + (j % size), ((x / tileSize) + (i % size)) * 2 + 1] != '0') //If all the tiles aren't free, then you can't build the tower
                                                                    {
                                                                        canBuild = false;
                                                                        break;
                                                                    }
                                                                }
                                                                catch (IndexOutOfRangeException) { canBuild = false; break; }
                                                            }
                                                        }
                                                        if (canBuild && money >= requiredMoney)//If build not blocked and have cash
                                                        {
                                                            money -= requiredMoney;
                                                            towers.Add((Tower)Activator.CreateInstance(selectedTowerType, x + (halfTile * size), y + (halfTile * size))); //+ halfTile so it's centered
                                                            selectedTowerType = null;
                                                            //I'm so proud of myself for getting this code working
                                                        }
                                                    }
                                                    else //Close upgrade menu
                                                    {
                                                        Ui.BuyMenu();
                                                    }
                                                }
                                                else
                                                {
                                                    selectedTowerType = null;
                                                }
                                            }
                                            leftMouseDown = true;
                                            break;
                                        }
                                    case (byte)SDL.SDL_BUTTON_RIGHT:
                                        {
                                            if (inEditor)
                                            {
                                                //Add path node to pos
                                                int x;
                                                int y;
                                                if (shiftDown)
                                                {
                                                    x = SnapToGrid(mouseX);
                                                    y = SnapToGrid(mouseY);
                                                }
                                                else
                                                {
                                                    x = SnapToGrid(mouseX) + halfTile;
                                                    y = SnapToGrid(mouseY) + halfTile;
                                                }
                                                while (currentPath > mapPathEdit.Count - 1)
                                                    mapPathEdit.Add(new MapPathEdit());
                                                mapPathEdit[currentPath].path.Add(new Point(x, y));
                                            }
                                            if (inGame)
                                            {
                                                selectedTowerType = null;
                                            }
                                            break;
                                        }
                                    case (byte)SDL.SDL_BUTTON_MIDDLE:
                                        {
                                            middleMouseDown = true;
                                            int x = mouseX / tileSize;
                                            int y = mouseY / tileSize;

                                            if (map[y, x * 2 + 1] == '0')
                                                currentBlock = '1';
                                            else
                                                currentBlock = '0';
                                            break;
                                        }
                                    case (byte)SDL.SDL_BUTTON_X1:
                                        {
                                            currentPath--;
                                            break;
                                        }
                                    case (byte)SDL.SDL_BUTTON_X2:
                                        {
                                            currentPath++;
                                            break;
                                        }
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                            {
                                switch (e.button.button)
                                {
                                    case (byte)SDL.SDL_BUTTON_LEFT:
                                        {
                                            leftMouseDown = false;
                                            break;
                                        }
                                    case (byte)SDL.SDL_BUTTON_MIDDLE:
                                        {
                                            middleMouseDown = false;
                                            break;
                                        }
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_KEYDOWN:
                            {
                                switch (e.key.keysym.sym)
                                {
                                    case SDL.SDL_Keycode.SDLK_g:
                                        //enemies.Add(new Enemy(10, waveHandlers[0]));
                                        break;
                                    case SDL.SDL_Keycode.SDLK_s:
                                        if (inEditor)
                                            SaveMap(mapName);
                                        break;
                                    case SDL.SDL_Keycode.SDLK_e:
                                        if (inEditor)
                                        {
                                            inEditor = false;
                                            inGame = true;
                                        }
                                        else
                                        {
                                            inEditor = true;
                                            inGame = false;
                                        }

                                        RenderBackground(inEditor);
                                        break;
                                    case SDL.SDL_Keycode.SDLK_c:
                                        {
                                            if (inEditor)
                                                mapPathEdit[currentPath].path.Clear();
                                            break;
                                        }
                                    case SDL.SDL_Keycode.SDLK_m:
                                        {
                                            if (inGame)
                                                money += 100;
                                            break;
                                        }
                                    case SDL.SDL_Keycode.SDLK_p:
                                        {
                                            if (inEditor)
                                            {
                                                //Automatically fills the path with nobuild
                                                for (int i = 0; i < mapPathEdit.Count; i++)
                                                {
                                                    for (int j = 1; j < mapPathEdit[i].path.Count - 2; j++) //Cuts off the "caps"
                                                    {
                                                        //Copied this code from old project, which itself copied code from stack overflow... I don't even remember what this algorithm is called...
                                                        int x = mapPathEdit[i].path[j].X;
                                                        int y = mapPathEdit[i].path[j].Y;
                                                        int x2 = mapPathEdit[i].path[j + 1].X;
                                                        int y2 = mapPathEdit[i].path[j + 1].Y;

                                                        int w = x2 - x;
                                                        int h = y2 - y;
                                                        int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0; //??? idk ask stack overflow
                                                        if (w < 0)
                                                        {
                                                            dx1 = -1;
                                                            dx2 = -1;
                                                        }
                                                        else if (w > 0)
                                                        {
                                                            dx1 = 1;
                                                            dx2 = 1;
                                                        }
                                                        if (h < 0)
                                                            dy1 = -1;
                                                        else if (h > 0)
                                                            dy1 = 1;
                                                        int longest = Math.Abs(w);
                                                        int shortest = Math.Abs(h);
                                                        if (longest <= shortest) //Bruh
                                                        {
                                                            longest = Math.Abs(h);
                                                            shortest = Math.Abs(w);
                                                            if (h < 0) //Why is this here? I don't know!!!
                                                                dy2 = -1;
                                                            else if (h > 0)
                                                                dy2 = 1;
                                                            dx2 = 0;
                                                        }
                                                        int numerator = longest >> 1;  //e.g. longest / 2^1 //e.g. this code divides by two
                                                        for (int k = 0; k <= longest; k++) //Enough with the variables, THIS is the actual algorithm
                                                        {
                                                            numerator += shortest;
                                                            if (numerator >= longest)
                                                            {
                                                                numerator -= longest;
                                                                x += dx1;
                                                                y += dy1;
                                                            }
                                                            else
                                                            {
                                                                x += dx2;
                                                                y += dy2;
                                                            }
                                                            //THIS is the actual code I wrote
                                                            if (y / tileSize < mapHeight && x / tileSize < mapWidth)
                                                                map[y / tileSize, x / tileSize * 2 + 1] = '1';
                                                        }
                                                    }
                                                }
                                                RenderBackground(true);
                                            }
                                            break;
                                        }
                                    case SDL.SDL_Keycode.SDLK_LSHIFT:
                                        {
                                            shiftDown = true;
                                            break;
                                        }
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_KEYUP:
                            {
                                switch (e.key.keysym.sym)
                                {
                                    case SDL.SDL_Keycode.SDLK_LSHIFT:
                                        {
                                            shiftDown = false;
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }
                //Post input
                currentTime = DateTime.Now;
                deltaTime = (currentTime.Ticks - lastTime.Ticks) / 2000f; //This probably isn't how this should be implemented but it does work so should be fine?
                lastTime = currentTime;

                //deltaTime *= 4f;

                Render();
                Logic();
            }
            //Clear memory just in case sdl doesn't manage to do it itself
            SDL.SDL_DestroyTexture(render);
            SDL.SDL_DestroyTexture(backgroundTexture);
            SDL.SDL_DestroyTexture(blockedTexture);
            SDL.SDL_DestroyTexture(entitiesTexture);
            SDL.SDL_DestroyTexture(tilemapTexture);
            foreach (TextRender textRender in cachedText.Values) //Clear cache
            {
                SDL.SDL_DestroyTexture(textRender.texture);
            }
            //Other stuff
            SDL_ttf.TTF_Quit();
            SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();
        }
        static int SnapToGrid(int pos)
        {
            return tileSize * (pos / tileSize); //This works because the division result is automatically rounded
        }
        static SDL.SDL_Rect ToSDL_Rect(this SRect sRect)
        {
            SDL.SDL_Rect returnRect = new() { x = sRect.x, y = sRect.y, w = sRect.w, h = sRect.h };
            return returnRect;
        }
        /// <summary>
        /// Creates texture from specified image
        /// </summary>
        /// <param name="path"></param>
        /// <param name="textureName"></param>
        /// <returns></returns>
        static void LoadTexture(string path, ref nint textureName)
        {
            nint surface = SDL_image.IMG_Load(path);
            textureName = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            SDL.SDL_FreeSurface(surface);
        }
        static void Render()
        {
            //Render to a texture that can be stretched to the window size
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.SDL_SetRenderTarget(renderer, render);
            SDL.SDL_RenderClear(renderer);
            //Render stuff
            if (inGame || inEditor)
            {
                SDL.SDL_Rect mapRenderRect = new()
                {
                    x = 0,
                    y = 0,
                    w = screenWidth,
                    h = mapScreenHeight
                };
                SDL.SDL_RenderCopy(renderer, backgroundTexture, (nint)null, ref mapRenderRect);
                if (inEditor)
                {
                    RenderText(Convert.ToString(currentPath), 0, 0, mainFont);
                    RenderTile(currentTile, SnapToGrid(mouseX), SnapToGrid(mouseY));
                    for (int i = 0; i < mapPathEdit.Count; i++) //Draw paths
                    {
                        for (int j = 0; j < mapPathEdit[i].path.Count; j++) //Draw the path
                        {
                            Point point = mapPathEdit[i].path[j];
                            Point nextPoint = point;
                            if (j < mapPathEdit[i].path.Count - 1)
                                nextPoint = mapPathEdit[i].path[j + 1];
                            SDL.SDL_RenderDrawLine(renderer, point.X, point.Y, nextPoint.X, nextPoint.Y);
                        }
                    }
                }
                if (inGame)
                {
                    RenderText("Round", "Round:" + Convert.ToString(round), 10, mapScreenHeight + 10, mainFont);
                    RenderText("Health", "V" + Convert.ToString(health), 10, mapScreenHeight + 40, mainFont);
                    RenderText("Money", "$" + Convert.ToString(money), 100, mapScreenHeight + 40, mainFont);
                    if (selectedTowerType != null && mouseY < mapScreenHeight)
                    {
                        int size = Tower.towerStats[selectedTowerType].size;

                        SDL.SDL_Rect srcrect = entitiesTextureMap[Tower.towerStats[selectedTowerType].textureName];
                        SDL.SDL_Rect dstrect = new()
                        {
                            x = SnapToGrid(mouseX) - (srcrect.w / 2) + (halfTile * size),
                            y = SnapToGrid(mouseY) - (srcrect.h / 2) + (halfTile * size),
                            w = srcrect.w,
                            h = srcrect.h
                        };
                        SDL.SDL_RenderCopy(renderer, entitiesTexture, ref srcrect, ref dstrect);
                        RenderDrawCircle(renderer, SnapToGrid(mouseX) + halfTile, SnapToGrid(mouseY) + halfTile, (int)(Tower.towerStats[selectedTowerType].radius * tileSize));
                    }
                    if (selectedTower != null)
                    {
                        RenderText("DmgCount", Convert.ToString(selectedTower.damageDealt), screenWidth - 75, mapScreenHeight + 10, smallFont);
                        RenderText("TargetingMode", Convert.ToString(selectedTower.targetingMode), screenWidth - 75, mapScreenHeight + 30, smallFont);
                    }
                }
                foreach (Tower tower in towers)
                {
                    tower.Render(renderer);
                    if (renderHitboxes)
                    {
                        SDL.SDL_Rect hitbox = new()
                        {
                            x = tower.hitbox.X,
                            y = tower.hitbox.Y,
                            w = tower.hitbox.Width,
                            h = tower.hitbox.Height
                        };
                        SDL.SDL_RenderDrawRect(renderer, ref hitbox);
                    }
                }
                foreach (Enemy enemy in enemies)
                {
                    enemy.Render(renderer);
                    if (renderHitboxes)
                    {
                        SDL.SDL_Rect hitbox = new()
                        {
                            x = enemy.hitbox.X,
                            y = enemy.hitbox.Y,
                            w = enemy.hitbox.Width,
                            h = enemy.hitbox.Height
                        };
                        SDL.SDL_RenderDrawRect(renderer, ref hitbox);
                    }
                }
                foreach (Projectile projectile in projectiles)
                {
                    projectile.Render(renderer);
                    if (renderHitboxes)
                    {
                        SDL.SDL_Rect hitbox = new()
                        {
                            x = projectile.hitbox.X,
                            y = projectile.hitbox.Y,
                            w = projectile.hitbox.Width,
                            h = projectile.hitbox.Height
                        };
                        SDL.SDL_RenderDrawRect(renderer, ref hitbox);
                    }
                }
            }
            foreach (Button button in ui) //Ui is rendered even when not in game
            {
                button.Render(renderer);
                if (renderHitboxes)
                {
                    SDL.SDL_Rect hitbox = new()
                    {
                        x = button.hitbox.X,
                        y = button.hitbox.Y,
                        w = button.hitbox.Width,
                        h = button.hitbox.Height
                    };
                    SDL.SDL_RenderDrawRect(renderer, ref hitbox);
                }
            }
            //Render stuff finished
            SDL.SDL_SetRenderTarget(renderer, (nint)null);
            SDL.SDL_RenderCopy(renderer, render, (nint)null, (nint)null);
            SDL.SDL_RenderPresent(renderer);
        }
        static void RenderTile(int tileId, int x, int y)
        {
            SDL.SDL_Rect srcrect = new()
            {
                x = (tileId % tilemapWidth) * tileSize,
                y = (tileId / tilemapWidth) * tileSize, //God I hope it truncates the result of the division
                w = tileSize,
                h = tileSize
            };
            SDL.SDL_Rect dstrect = new()
            {
                x = x,
                y = y,
                w = tileSize,
                h = tileSize
            };
            SDL.SDL_RenderCopy(renderer, tilemapTexture, ref srcrect, ref dstrect);
        }
        /// <summary>
        /// Shortcut for rendercopy which renders full image at xy
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        static void RenderTexture(nint texture, int x, int y, int width, int height)
        {
            SDL.SDL_Rect dstrect = new()
            {
                x = x,
                y = y,
                w = width,
                h = height
            };
            SDL.SDL_RenderCopy(renderer, texture, (nint)null, ref dstrect);
        }
        /// <summary>
        /// Render text
        /// </summary>
        public static void RenderText(string key, string text, int x, int y, nint Font)
        {
            if (text.Contains('\n'))
            {
                string[] strings = text.Split('\n');
                for (int i = 0; i < strings.Length; i++)
                {
                    RenderText($"{key}{i}", strings[i].Replace("\n", null), x, y + (i * 15), Font);
                }
            }
            else
            {
                bool containsKey = cachedText.TryGetValue(key, out TextRender textRender);
                if (!containsKey || textRender.value != text) //We need to render the text
                {
                    SDL.SDL_Color white = new();
                    white.r = white.g = white.b = white.a = 255; //Color

                    nint messageSurface = SDL_ttf.TTF_RenderText_Solid(Font, text, white);
                    //Render to texture
                    nint messageTexture = SDL.SDL_CreateTextureFromSurface(renderer, messageSurface);
                    SDL_ttf.TTF_SizeText(Font, text, out int w, out int h); //How long will the string be once rendered?
                    var messageRect = new SDL.SDL_Rect
                    {
                        x = x,
                        y = y,
                        w = w,
                        h = h,
                    };
                    SDL.SDL_RenderCopy(renderer, messageTexture, (nint)null, ref messageRect);
                    SDL.SDL_FreeSurface(messageSurface);
                    //Cache
                    //TODO: This could probably be improved
                    if (containsKey) //Delete old one
                    {
                        SDL.SDL_DestroyTexture(textRender.texture);
                        cachedText.Remove(key);
                    }
                    //Add new one
                    cachedText.Add(key, new TextRender(messageTexture, text, messageRect));
                }
                else //If this exact string has already been rendered, lets just use that
                {
                    if (textRender.dstrect.x != x || textRender.dstrect.y != y) //If x has changed
                    {
                        cachedText.Remove(key);
                        var dstrect = new SDL.SDL_Rect
                        {
                            x = x,
                            y = y,
                            w = textRender.dstrect.w,
                            h = textRender.dstrect.h,
                        };
                        //We stil reuse the actual render, we just change the position
                        cachedText.Add(key, new TextRender(textRender.texture, text, dstrect));
                    }
                    SDL.SDL_RenderCopy(renderer, textRender.texture, (nint)null, ref textRender.dstrect);
                }
            }
        }
        /// <summary>
        /// Render text without cahching. Only use if the string is supposed to be rendered only once
        /// </summary>
        public static void RenderText(string text, int x, int y, nint Font)
        {
            SDL.SDL_Color white = new();
            white.r = white.g = white.b = white.a = 255; //Color

            nint messageSurface = SDL_ttf.TTF_RenderText_Solid(Font, text, white);
            //Render to texture
            nint messageTexture = SDL.SDL_CreateTextureFromSurface(renderer, messageSurface);
            SDL_ttf.TTF_SizeText(Font, text, out int w, out int h); //How long will the string be once rendered?
            var messageRect = new SDL.SDL_Rect
            {
                x = x,
                y = y,
                w = w,
                h = h,
            };
            SDL.SDL_RenderCopy(renderer, messageTexture, (nint)null, ref messageRect);
            SDL.SDL_FreeSurface(messageSurface);
            SDL.SDL_DestroyTexture(messageTexture);
        }
        public static void RenderBackground(bool editor)
        {
            //This isn't technically efficient, but since this is called so infrequently (the background is cached), it shoudn't be a probem
            SDL.SDL_DestroyTexture(backgroundTexture);
            backgroundTexture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGB888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, screenWidth, mapScreenHeight);
            SDL.SDL_SetRenderTarget(renderer, backgroundTexture);
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int tile = Convert.ToInt32(map[y, x * 2]) - unicodeMargin;
                    RenderTile(tile, x * tileSize, y * tileSize);
                    if (editor && map[y, x * 2 + 1] != '0')
                    {
                        RenderTexture(blockedTexture, x * tileSize, y * tileSize, tileSize, tileSize);
                    }
                }
            }
            SDL.SDL_SetRenderTarget(renderer, (nint)null);
        }
        /// <summary>
        /// Uses Midpoint circle algorithm to render circle
        /// </summary>
        public static void RenderDrawCircle(nint renderer, int centreX, int centryY, int radius)
        {
            int diameter = radius * 2;
            int x = radius - 1;
            int y = 0;
            int tx = 1;
            int ty = 1;
            int error = tx - diameter;
            while (x >= y)
            {
                //Apparently this is "very fast"
                SDL.SDL_RenderDrawPoint(renderer, centreX + x, centryY - y);
                SDL.SDL_RenderDrawPoint(renderer, centreX + x, centryY + y);
                SDL.SDL_RenderDrawPoint(renderer, centreX - x, centryY - y);
                SDL.SDL_RenderDrawPoint(renderer, centreX - x, centryY + y);
                SDL.SDL_RenderDrawPoint(renderer, centreX + y, centryY - x);
                SDL.SDL_RenderDrawPoint(renderer, centreX + y, centryY + x);
                SDL.SDL_RenderDrawPoint(renderer, centreX - y, centryY - x);
                SDL.SDL_RenderDrawPoint(renderer, centreX - y, centryY + x);

                if (error <= 0)
                {
                    ++y; //Is this the same as y++? Idk ported this code from C++
                    error += ty;
                    ty += 2;
                }
                if (error > 0)
                {
                    --x;
                    tx += 2;
                    error += tx - diameter;
                }
            }
        }
        static void Logic()
        {
            enemies.Sort((y, x) => x.progress.CompareTo(y.progress)); //Sort enemies by progress so towers correctly target the enemy furthest on the map
            waveHandlers.Sort((y, x) => x.TimeLeft.CompareTo(y.TimeLeft)); //Sort ongoing waves by which has the most time left so the clock works correctly

            if (inEditor)
            {
                if (leftMouseDown && mouseY < mapScreenHeight)
                {
                    //Set tile at pos
                    int x = mouseX / tileSize;
                    int y = mouseY / tileSize;
                    char oldTile = map[y, x * 2];
                    char newTile = Convert.ToChar(currentTile + unicodeMargin);
                    if (oldTile != newTile) //This saves a bit of resources due to not having to re-render the background as much
                    {
                        map[y, x * 2] = newTile;
                        RenderBackground(true);
                    }
                }
                else if (middleMouseDown && mouseY < mapScreenHeight)
                {
                    //Switch restriction at pos
                    int x = mouseX / tileSize;
                    int y = mouseY / tileSize;
                    map[y, x * 2 + 1] = currentBlock;
                    RenderBackground(true);
                }
            }
            if (inGame && roundInProgress)
            {
                if (waveHandlers.Count != 0) //Only count up when enemies are still being spawned
                {
                    //Handle waves
                    foreach (WaveHandler waveHandler in waveHandlers.ToList())
                    {
                        waveHandler.Logic();
                        if (waveHandler.finished)
                        {
                            //Round end
                            if (waveHandler.round <= 10)
                                money += waveHandler.round * 10;
                            else
                                money += (int)(100 * Math.Log10(waveHandler.round));
                            waveHandlers.Remove(waveHandler);
                        }
                    }
                }
                else //No waves ongoing
                {
                    RoundFinish();
                }
            }
            foreach (Enemy enemy in enemies.ToList())
                enemy.Logic();
            foreach (Tower tower in towers.ToList())
                tower.Logic();
            foreach (Projectile projectile in projectiles.ToList())
                projectile.Logic();
            foreach (Button button in ui.ToList())
                button.Logic();
        }
        public static void StartGame()
        {
            Ui.BuyMenu();
            ui.Add(new RoundButton(50, 50, NewRound));
            inGame = true;
            round = 0;
            money = 240;
            health = 100;
        }
        static void RoundFinish()
        {
            roundInProgress = false;
            if (autoPlay)
                NewRound();
        }
        static void NewRound()
        {
            //The earlier you start a new round, the more money you get. Kingdom Rush
            if (roundInProgress)
            {
                //roundBonus is calculated by the round button
                money += roundBonus;
            }
            SDL_mixer.Mix_PlayChannel(-1, bellDongWav, 0);
            round++;
            waveHandlers.Add(new WaveHandler(round));
            roundInProgress = true;

            //Next round preview texture
            SDL.SDL_DestroyTexture(nextRoundTexture);
            if (round != WaveHandler.waves.Length - 1)
            {
                Wave nextWave = WaveHandler.waves[round + 1];
                int uniqueEnemies = 0; //How many different enemy types are in the wave
                int[] enemyAmounts = new int[Enemy.enemyTypes]; //Stores how many of each enemy are in the next wave
                for (int i = 0; i < nextWave.enemies.Length; i++) //What enemies
                {
                    if (enemyAmounts[nextWave.enemies[i].enemyId] == 0)
                        uniqueEnemies++;
                    enemyAmounts[nextWave.enemies[i].enemyId]++;
                }
                nextRoundTextureHeight = (uniqueEnemies + 2) * 20;
                nextRoundTexture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGB888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, 100, nextRoundTextureHeight);
                SDL.SDL_SetRenderTarget(renderer, nextRoundTexture);
                SDL.SDL_SetRenderDrawColor(renderer, 68, 27, 0, 255); //Temporary because no proper texture for this exists yet
                SDL.SDL_RenderClear(renderer);
                RenderText("NextRound", "Next round:", 3, 3, smallFont);
                int j = 0;
                for (int i = 0; i < enemyAmounts.Length; i++) //Add enemies to texture
                {
                    if (enemyAmounts[i] > 0) //Contains this enemy type
                    {
                        j++; //This is used so idk too lazy to write it out you know now I trust you'll remember it
                        SDL.SDL_Rect srcrect = entitiesTextureMap[$"enemy{i}"];
                        SDL.SDL_Rect dstrect = new()
                        {
                            x = 10,
                            y = j * 20,
                            h = 20, //Always 20px tall
                            w = (int)((20f / srcrect.h) * srcrect.w) //But we need to calculate the width to be in the correct ratio
                        };
                        SDL.SDL_RenderCopyEx(renderer, entitiesTexture, ref srcrect, ref dstrect, 90, (nint)null, SDL.SDL_RendererFlip.SDL_FLIP_NONE);
                        RenderText(Convert.ToString(enemyAmounts[i]), 40, j * 20, smallFont);
                    }
                }
                SDL.SDL_SetRenderTarget(renderer, (nint)null);
            }
        }
        /// <summary>
        /// Loads map to map and mapPath variables
        /// </summary>
        public static void LoadMap(string fileName)
        {
            bool embeddedFile;
            string[] mapFile;
            if (File.Exists(fileName)) //If map is in build folder
            {
                embeddedFile = false;
                mapFile = File.ReadAllLines(fileName);//Full map file
            }
            else //Map is embedded resource
            {
                embeddedFile = true;
                mapFile = LoadEmbeddedResource(fileName).CleanString().Split('\n');
            }
            //Map
            map = mapFile.Take(mapHeight).Select(item => item.ToArray()).ToArray().To2D(); //Sheesh...
            for (int i = 0; i < map.GetLength(0); i++) //Remove tower space blockers mistankenly saved into the level
            {
                for (int j = 0; j < map.GetLength(1); j++)
                {
                    if (map[i, j] == '2' && j % 2 == 1)
                        map[i, j] = '0';
                }
            }
            //Path
            string[][] mapPathJSON = mapFile.Skip(mapHeight).SkipLast(1).ToArray().SplitBy("-");
            mapPaths = new MapPath[mapPathJSON.GetLength(0)];
            for (int i = 0; i < mapPaths.GetLength(0); i++)
            {
                if (mapPathJSON[i] == null) //Don't try to parse an empty path
                    continue;
                mapPaths[i].path = new Point[mapPathJSON[i].Length];

                for (int j = 0; j < mapPaths[i].path.Length; j++)
                {
                    mapPaths[i].path[j] = JsonSerializer.Deserialize<Point>(mapPathJSON[i][j]);
                }
            }
            mapPathEdit.Clear();
            foreach (MapPath path in mapPaths)
            {
                if (path.path != null)
                    mapPathEdit.Add(path.ToMapPathEdit());
            }
            RenderBackground(inEditor);
            mapName = fileName;
            string lastLine = mapFile.Last();
            pathRule = (PathRule)char.GetNumericValue(lastLine.Last());
        }
        static void SaveMap(string filePath)
        {
            List<string> mapFile = [];
            //Map
            string[] mapSave = new string[map.GetLength(0)];
            for (int i = 0; i < mapSave.Length; i++) //I'm sure there's a cleaner way of doing this with Ienumerable or something
            {
                mapSave[i] = new string(map.GetRow(i));
            }
            mapFile.AddRange(mapSave);//Saves map to file
            //Path
            for (int i = 0; i < mapPathEdit.Count; i++) //Paths
            {
                if (mapPathEdit[i].path.Count == 0) //Don't save empty paths
                    continue;
                //Here we add ending and start "caps" to the path (this makes the path start and end offscreen)
                if (mapPathEdit[i].path.Count > 0 && mapPathEdit[i].path[0].X > 0 && mapPathEdit[i].path[0].Y > 0) //If the caps haven't already been added (Count > 0 is just so it doesn't crash when empty)
                {
                    mapPathEdit[i].path.Insert(0, FindEdge(mapPathEdit[i].path[0])); //Start cap
                    mapPathEdit[i].path.Add(FindEdge(mapPathEdit[i].path[^1])); //End cap
                }

                string[] mapPathJSON = new string[mapPathEdit[i].path.Count];
                for (int j = 0; j < mapPathJSON.Length; j++)
                {
                    mapPathJSON[j] = JsonSerializer.Serialize(mapPathEdit[i].path[j]);
                }
                mapFile.AddRange(mapPathJSON);
                if (mapPathEdit.Count > 1) //Only add this line if there's actually multiple paths
                    mapFile.Add("-");
            }
            if (mapFile.Last() == "-")
                mapFile.RemoveAt(mapFile.Count - 1); //Shouldn't end with a seperator
            mapFile.Add(Convert.ToString((int)pathRule));
            //Save map
            File.WriteAllLines("data/map.map", mapFile);

            //Local function
            static Point FindEdge(Point position)
            {
                Point returnPoint = position;
                //X
                if (position.X == 0 + halfTile) //If at the left edge
                    returnPoint.X = 0 - halfTile; //Set return x to one tile to the left
                else if (position.X == screenWidth - halfTile) //If at the right edge
                    returnPoint.X = screenWidth + halfTile; //Set return x to one tile to the left
                //Y
                if (position.Y == 0 + halfTile) //If at the top edge
                    returnPoint.Y = 0 - halfTile; //Set return y to one tile up
                else if (position.Y == mapScreenHeight - halfTile) //If at the bottom edge
                    returnPoint.Y = mapScreenHeight + halfTile; //Set return y to one tile down
                //Finish
                return returnPoint;
            }
        }
        public static string LoadEmbeddedResource(string resourceName)
        {
            string result;
            var assembly = Assembly.GetExecutingAssembly();
            resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith(resourceName));

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }
        /// <summary>
        /// Removes characters from string which may fuck up code
        /// </summary>
        static string CleanString(this string str)
        {
            return str.Replace("\r", null);
        }
        static T[][] SplitBy<T>(this T[] array, T seperator)
        {
            int count = array.Count(x => EqualityComparer<T>.Default.Equals(x, seperator)); //How many seperators there are
            T[][] values = new T[count + 1][];
            int counter = 0;
            List<T> value = []; //I was really hoping I could make this function work without any lists, guess I'm not quite good enough yet
            for (int i = 0; i < array.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(array[i], seperator) || i == array.Length - 1) //If seperator or at end
                {
                    if (i == array.Length - 1) //Add last value
                        value.Add(array[i]);

                    values[counter] = [.. value]; //To list
                    counter++;
                    value.Clear();
                }
                else
                    value.Add(array[i]);
            }
            return values;
        }
        static T[] GetRow<T>(this T[,] matrix, int rowNumber)
        {
            return Enumerable.Range(0, matrix.GetLength(1)).Select(x => matrix[rowNumber, x]).ToArray();
        }
        static T[,] To2D<T>(this T[][] source)
        {
            try
            {
                int FirstDim = source.Length;
                int SecondDim = source.GroupBy(row => row.Length).Single().Key; // throws InvalidOperationException if source is not rectangular

                var result = new T[FirstDim, SecondDim];
                for (int i = 0; i < FirstDim; ++i)
                    for (int j = 0; j < SecondDim; ++j)
                        result[i, j] = source[i][j];

                return result;
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("The given jagged array is not rectangular.");
            }
        }
    }
    struct TextRender(nint texture, string value, SDL.SDL_Rect dstrect)
    {
        public nint texture = texture; //Render of text
        public string value = value; //Value of text rendered
        public SDL.SDL_Rect dstrect = dstrect; //Might aswell store this too
    }
    /// <summary>
    /// Simple rectangle struct
    /// </summary>
    struct SRect
    {
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }
    }
    abstract class RenderObject
    {
        //Couldn't figure out static inheritance with interfaces or fucking idk shit sucks
        public bool RenderDirection = true;
        public bool RenderDirEqualDirection = true; //Whether direction is to be used as the renderDir
        public SDL.SDL_Rect renderRect;
        /* Forgot rendercopy needs ref lol
        {
            get
            {
                if (sizeModifier != 1)
                {
                    return new SDL.SDL_Rect
                    {
                        x = (int)(renderRect.x * sizeModifier),
                        y = (int)(renderRect.y * sizeModifier),
                        w = (int)(renderRect.w * sizeModifier),
                        h = (int)(renderRect.h * sizeModifier),
                    };
                }
                else return renderRect;
            }
            set
            {
                renderRect = value;
            }
        }
        */
        public string textureName; //Position on entitiesTextureMap[]
        public float x;
        public float y;
        public double direction;
        public float renderDir;
        public float sizeModifier = 1;
        private const float degrees90 = 1.570796f;
        public MouseClickStages mouseClick = MouseClickStages.noTouch;
        public Rectangle hitbox //The hitbox of the object, based on x, y and renderRect
        {
            get
            {
                return new Rectangle()
                {
                    X = (int)(x - ((renderRect.w * sizeModifier) / 2)),
                    Y = (int)(y - ((renderRect.h * sizeModifier) / 2)),
                    Width = (int)(renderRect.w * sizeModifier),
                    Height = (int)(renderRect.h * sizeModifier)
                };
            }
        }
        /// <summary>
        /// Sets renderRect. To be run after textureName is set
        /// </summary>
        public void TextureSet()
        {
            renderRect = Program.entitiesTextureMap[textureName];
        }
        public void PointTowards(int targetX, int targetY)
        {
            direction = Math.Atan2(targetY - y, targetX - x) + degrees90;
        }
        public SDL.SDL_FRect DstRect() //This has to be a function since dstrect is called with ref in rendercopy, which doesn't accept a getter setter variable or whatever, what the fuck am I writing rn
        {
            SDL.SDL_FRect dstrect = new()
            {
                x = (x - ((renderRect.w * sizeModifier) / 2)),
                y = (y - ((renderRect.h * sizeModifier) / 2)),
                w = (renderRect.w * sizeModifier),
                h = (renderRect.h * sizeModifier)
            };
            return dstrect;
        }
        public virtual void Render(nint renderer)
        {
            SDL.SDL_FRect dstrect = DstRect();
            if (RenderDirection)//Render with direction
            {
                //SDL.SDL_FPoint centerPoint = new() { x = renderRect.w / 2, y = renderRect.h / 2 }; holy shit I can't believe (nint)null works for the center omfg
                if (RenderDirEqualDirection)//If direction is to be used as the render dir
                    SDL.SDL_RenderCopyExF(renderer, Program.entitiesTexture, ref renderRect, ref dstrect, RadiansToDegrees(direction), (nint)null, SDL.SDL_RendererFlip.SDL_FLIP_NONE);
                else
                    SDL.SDL_RenderCopyExF(renderer, Program.entitiesTexture, ref renderRect, ref dstrect, renderDir, (nint)null, SDL.SDL_RendererFlip.SDL_FLIP_NONE);
            }
            else
            {
                SDL.SDL_RenderCopyF(renderer, Program.entitiesTexture, ref renderRect, ref dstrect);
            }
        }
        /// <summary>
        /// Moves in the direction the object is facing at a specified speed
        /// </summary>
        public void Move(float speed)
        {
            x += (float)Math.Sin(direction) * speed * Program.deltaTime;
            y -= (float)Math.Cos(direction) * speed * Program.deltaTime;
        }
        public virtual void Logic()
        {
            CheckForClick();
        }
        /// <summary>
        /// Run this function in <see cref="Logic()"/> if you want <see cref="Click()"/> to be called when this object is clicked
        /// </summary>
        public void CheckForClick()
        {
            if (TouchingMouse())
            {
                if (Program.leftMouseDown)
                {
                    if (mouseClick == MouseClickStages.noTouch)
                        mouseClick = MouseClickStages.clickAndTouch;
                    else if (mouseClick == MouseClickStages.yesTouch)
                    {
                        Click();
                        mouseClick = MouseClickStages.clickAndTouch;
                    }
                }
                else
                    mouseClick = MouseClickStages.yesTouch;
            }
            else
                mouseClick = MouseClickStages.noTouch;
        }
        public virtual void Click()
        {
            //Clicked
        }
        /// <returns>Distance between to points</returns>
        public static int Distance(int x, int y, int x2, int y2)
        {
            int xDif = Math.Abs(x - x2);
            int yDif = Math.Abs(y - y2);
            return xDif + yDif; //Maybe?
        }
        /// <returns>Distance between to points</returns>
        public static float Distance(float x1, float y1, float x2, float y2)
        {
            //I swear to god man...
            return (float)Math.Sqrt(Math.Abs(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)));
        }
        static double RadiansToDegrees(double radians)
        {
            return radians * (180 / Math.PI);
        }
        public bool TouchingMouse()
        {
            return hitbox.Contains(Program.mouseX, Program.mouseY);
        }
    }
}
