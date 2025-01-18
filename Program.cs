using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace GBVLightingPlayground;

public struct Light
{

    public byte R;
    public byte G;
    public byte B;
    public byte Brightness; // 0 to 15 ONLY.

    public Light(byte r, byte g, byte b, byte brightness)
    {
        R = r;
        G = g;
        B = b;
        Brightness = brightness;
    }

}

class Program
{
    
    static int worldSize = 128;
    static int tileSize = 12;
    static bool[,] map = new bool[worldSize, worldSize];
    static uint[,] lightmap = new uint[worldSize, worldSize];
    static Dictionary<(int x, int y), (uint r, uint g, uint b)> lights = new();
    private static Dictionary<(int x, int y), Light> _lights = new();
    private static Queue<(int x, int y)> _lightQueue = new();
    private static uint _selectorRedValue = 15;
    private static uint _selectorGreenValue = 15;
    private static uint _selectorBlueValue = 15;
    private static string _previousBench = "";
    private static float offsetX = 0;
    private static float offsetY = 0;

    enum vals : int
    {
        Hello = 0,
        World = 1
    }

    static void Main(string[] args)
    {
        
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(600, 400, "Have fun with lighting");

        bool set = false;
        int amt = 0;
        
        while (!Raylib.WindowShouldClose())
        {

            for (int x = 0; x < worldSize; x++)
            {

                for (int y = 0; y < worldSize; y++)
                {
                    
                    if (DoesIntersect(Raylib.GetMousePosition(), (int)offsetX + x * tileSize, (int)offsetY + y * tileSize, tileSize, tileSize))
                    {

                        if (Raylib.IsKeyDown(KeyboardKey.LeftControl))
                        {

                            if (Raylib.IsMouseButtonDown(MouseButton.Left))
                            {

                                if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
                                {
                                    
                                    if (lights.ContainsKey((x, y)))
                                    {
                                        
                                        RemoveAround((x,y), lights[(x,y)]);
                                        lights.Remove((x, y));
                                        SearchAndRecalculate((x,y));   
                                        
                                    }
                                    
                                }
                                else
                                {

                                    if (!lights.ContainsKey((x,y)))
                                    {
                                        
                                        lights.Add((x,y), (_selectorRedValue, _selectorGreenValue, _selectorBlueValue));
                                        _lightQueue.Enqueue((x,y));
                                        
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        else
                        {

                            if (Raylib.IsMouseButtonDown(MouseButton.Left))
                            {

                                if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
                                {

                                    if (map[x, y])
                                    {
                                        
                                        map[x, y] = false;
                                        SearchAndRecalculate((x,y));   
                                        
                                    }
                                    
                                }
                                else
                                {

                                    if (!map[x, y])
                                    {
                                        
                                        map[x, y] = true;
                                        
                                        if (lights.ContainsKey((x,y))) lights.Remove((x, y));
                                        RemoveAround((x,y), (15,15,15));
                                        SearchAndRecalculate((x,y));
                                        
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
            Stopwatch sw = Stopwatch.StartNew();
            while (_lightQueue.Count > 0)
            {

                if (_lightQueue.TryDequeue(out (int x, int y) lightPos))
                {
                    
                    set = true;
                    amt++;
                    AddLight(lightPos);
                    
                }
                
            }
            sw.Stop();
            if (set)
            {
                set = false;
                _previousBench =
                    $"Lighting calculations ({amt} lights) took {sw.Elapsed.TotalMilliseconds}ms";
                amt = 0;
            }
            
            
            Raylib.ClearBackground(Color.Gray);
            
            Raylib.BeginDrawing();
            
            // Console.WriteLine(Raylib.GetMousePosition());

            if (Raylib.IsKeyPressed(KeyboardKey.R))
            {
                
                // AddLight();
                
            }

            if (Raylib.IsKeyPressed(KeyboardKey.C))
            {

                lights.Clear();
                Array.Clear(lightmap);
                Array.Clear(map);
                
            }

            for (int x = 0; x < worldSize; x++)
            {

                for (int y = 0; y < worldSize; y++)
                {
                    
                    Color topLeft = UnpackColorToColor(lightmap[x,y]);;
                    Color topRight = UnpackColorToColor(lightmap[x,y]);;
                    Color bottomLeft = UnpackColorToColor(lightmap[x,y]);;
                    Color bottomRight = UnpackColorToColor(lightmap[x,y]);;
                    if (x - 1 >= 0 && x + 1 < worldSize && y - 1 >= 0 && y + 1 < worldSize)
                    {

                        Vector4 sample = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x, y]));
                        Vector4 sampleTop = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x, y - 1]));
                        Vector4 sampleBottom = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x, y + 1]));
                        Vector4 sampleLeft = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x - 1, y]));
                        Vector4 sampleRight = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x + 1, y]));
                        Vector4 sampleTopLeft = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x - 1, y - 1]));
                        Vector4 sampleTopRight = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x + 1, y - 1]));
                        Vector4 sampleBottomLeft = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x - 1, y + 1]));
                        Vector4 sampleBottomRight = Raylib.ColorNormalize(UnpackColorToColor(lightmap[x + 1, y + 1]));

                        Vector4 cTL = CosineInterpolate(CosineInterpolate(sampleTopLeft, sampleTop, 0.5f), CosineInterpolate(sampleLeft, sample, 0.5f), 0.5f);
                        Vector4 cBL = CosineInterpolate(CosineInterpolate(sampleLeft, sample, 0.5f),
                            CosineInterpolate(sampleBottomLeft, sampleBottom, 0.5f), 0.5f);
                        Vector4 cTR = CosineInterpolate(CosineInterpolate(sampleTop, sampleTopRight, 0.5f),
                            CosineInterpolate(sample, sampleRight, 0.5f), 0.5f);
                        Vector4 cBR = CosineInterpolate(CosineInterpolate(sample, sampleRight, 0.5f), CosineInterpolate(sampleBottom, sampleBottomRight, 0.5f), 0.5f);
                        
                        // topLeft = Raylib.ColorFromNormalized( (sampleTop + sampleTopLeft + sampleLeft + sample) / 4.0f );
                        // topRight = Raylib.ColorFromNormalized( (sampleTop + sampleTopRight + sampleRight + sample) / 4.0f );
                        // bottomLeft = Raylib.ColorFromNormalized( (sampleBottom + sampleBottomLeft + sampleLeft + sample) / 4.0f );
                        // bottomRight = Raylib.ColorFromNormalized( (sampleBottom + sampleBottomRight + sampleRight + sample) / 4.0f );

                        topLeft = Raylib.ColorFromNormalized(cTL);
                        topRight = Raylib.ColorFromNormalized(cTR);
                        bottomLeft = Raylib.ColorFromNormalized(cBL);
                        bottomRight = Raylib.ColorFromNormalized(cBR);

                    }
                    if (DoesIntersect(Raylib.GetMousePosition(), (int)offsetX + x * tileSize, (int)offsetY + y * tileSize, tileSize, tileSize))
                    {
                        topLeft = Color.Green;
                        topRight = Color.Green;
                        bottomLeft = Color.Green;
                        bottomRight = Color.Green;
                    }
                    if (map[x, y])
                    {
                        topLeft = Color.Red;
                        topRight = Color.Red;
                        bottomLeft = Color.Red;
                        bottomRight = Color.Red;
                    }
                    Raylib.DrawRectangleGradientEx(new Rectangle(offsetX + x * tileSize, offsetY + y * tileSize, tileSize, tileSize), topLeft, bottomLeft, bottomRight, topRight);
                    // Raylib.DrawRectangle((x * tileSize), (y * tileSize), tileSize, tileSize, resultColor);

                }
                
            }

            if (!Raylib.IsKeyDown(KeyboardKey.R) && !Raylib.IsKeyDown(KeyboardKey.G) &&
                !Raylib.IsKeyDown(KeyboardKey.B))
            {

                if (Raylib.IsKeyPressed(KeyboardKey.Up))
                {
                    
                    _selectorRedValue = (uint) Math.Clamp((int)_selectorRedValue + 1, 0, 15);
                    _selectorGreenValue = (uint) Math.Clamp((int)_selectorGreenValue + 1, 0, 15);
                    _selectorBlueValue = (uint) Math.Clamp((int)_selectorBlueValue + 1, 0, 15);
                    
                }

                if (Raylib.IsKeyPressed(KeyboardKey.Down))
                {
                    
                    _selectorRedValue = (uint) Math.Clamp((int)_selectorRedValue - 1, 0, 15);
                    _selectorGreenValue = (uint) Math.Clamp((int)_selectorGreenValue - 1, 0, 15);
                    _selectorBlueValue = (uint) Math.Clamp((int)_selectorBlueValue - 1, 0, 15);
                    
                }
                
            }

            if (Raylib.IsKeyDown(KeyboardKey.R))
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Up))
                {
                    _selectorRedValue = (uint) Math.Clamp((int)_selectorRedValue + 1, 0, 15);
                }

                if (Raylib.IsKeyPressed(KeyboardKey.Down))
                {
                    _selectorRedValue = (uint) Math.Clamp((int)_selectorRedValue - 1, 0, 15);
                }
            }

            if (Raylib.IsKeyDown(KeyboardKey.G))
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Up))
                {
                    _selectorGreenValue = (uint) Math.Clamp((int)_selectorGreenValue + 1, 0, 15);
                }

                if (Raylib.IsKeyPressed(KeyboardKey.Down))
                {
                    _selectorGreenValue = (uint) Math.Clamp((int)_selectorGreenValue - 1, 0, 15);
                }
            }
            
            if (Raylib.IsKeyDown(KeyboardKey.B))
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Up))
                {
                    _selectorBlueValue = (uint) Math.Clamp((int)_selectorBlueValue + 1, 0, 15);
                }

                if (Raylib.IsKeyPressed(KeyboardKey.Down))
                {
                    _selectorBlueValue = (uint) Math.Clamp((int)_selectorBlueValue - 1, 0, 15);
                }
            }

            if (Raylib.IsKeyDown(KeyboardKey.Space))
            {

                Vector2 delta = Raylib.GetMouseDelta();
                offsetX += delta.X;
                offsetY += delta.Y;

            }
            
            Raylib.DrawText($"Red: {_selectorRedValue}", 4, 4, 24, Color.White);
            Raylib.DrawText($"Green: {_selectorGreenValue}", 4, 4 + 24, 24, Color.White);
            Raylib.DrawText($"Blue: {_selectorBlueValue}", 4, 4 + 24 + 24, 24, Color.White);
            byte r = (byte) ((_selectorRedValue / 15.0f) * 255.0f);
            byte g = (byte) ((_selectorGreenValue / 15.0f) * 255.0f);
            byte b = (byte) ((_selectorBlueValue / 15.0f) * 255.0f);
            Raylib.DrawRectangle(4, 4 + 24 + 24 + 24, 50, 50, new Color(r,g,b));
            
            Raylib.EndDrawing();
            
        }

    }

    // from https://paulbourke.net/miscellaneous/interpolation/
    static Vector4 CosineInterpolate(Vector4 a, Vector4 b, float t)
    {

        float t2 = (float) ((1 - Math.Cos(t * Math.PI)) / 2.0);
        return new Vector4(
            (a.X*(1-t2)+b.X*t2),
            (a.Y*(1-t2)+b.Y*t2),
            (a.Z*(1-t2)+b.Z*t2),
            (a.W*(1-t2)+b.W*t2)
            );

    }

    static Color[] GetNeighborHorizontalInterpolated((int x, int y) sample)
    {
        
        Color[] colors = new Color[4];
        Color colorSampleTop = UnpackColorToColor(lightmap[sample.x, sample.y - 1]);
        Color colorSampleBottom = UnpackColorToColor(lightmap[sample.x, sample.y + 1]);
        Color colorSampleTopLeft = UnpackColorToColor(lightmap[sample.x - 1, sample.y - 1]);
        Color colorSampleTopRight = UnpackColorToColor(lightmap[sample.x + 1, sample.y - 1]);
        Color colorSampleBottomLeft = UnpackColorToColor(lightmap[sample.x - 1, sample.y + 1]);
        Color colorSampleBottomRight = UnpackColorToColor(lightmap[sample.x + 1, sample.y + 1]);

        colors[0] = Raylib.ColorLerp(colorSampleTop, colorSampleTopLeft, 0.5f);
        colors[1] = Raylib.ColorLerp(colorSampleBottom, colorSampleBottomLeft, 0.5f);
        colors[2] = Raylib.ColorLerp(colorSampleTop, colorSampleTopRight, 0.5f);
        colors[3] = Raylib.ColorLerp(colorSampleBottom, colorSampleBottomRight, 0.5f);
        
        return colors;

    }
    static uint PackColor((uint r, uint g, uint b) color)
    {
        
        uint c = color.r << 12 | color.g << 8 | color.b << 4;
        // Console.WriteLine($"Packed color of {color} turns to {UnpackColor(c)}");
        return c;

    }
    
    static (uint r, uint g, uint b) UnpackColor(uint packedColor)
    {
        
        return ((packedColor >> 12) & 15, (packedColor >> 8) & 15, (packedColor >> 4) & 15);
        
    }

    static (uint r, uint g, uint b) ColorMax((uint r, uint g, uint b) a, (uint r, uint g, uint b) b)
    {
        
        return (Math.Max(a.r, b.r), Math.Max(a.g, b.g), Math.Max(a.b, b.b));
        
    }
    static (float r, float g, float b) ColorDivide((uint r, uint g, uint b) numerator,
        (uint r, uint g, uint b) denominator)
    {
        
        // return (numerator.r / (float)denominator.r, numerator.g / (float)denominator.g, numerator.b / (float)denominator.b);
        float r = 0;
        float g = 0;
        float b = 0;
        if (denominator.r > 0) r = numerator.r / (float) denominator.r;
        if (denominator.g > 0) g = numerator.g / (float) denominator.g;
        if (denominator.b > 0) b = numerator.b / (float) denominator.b;
        
        return (r, g, b);
        
    }

    static (float r, float g, float b) ColorDivide((uint r, uint g, uint b) numerator, int denominator)
    {

        float r = 0;
        float g = 0;
        float b = 0;
        if (denominator > 0) r = numerator.r / (float) denominator;
        if (denominator > 0) g = numerator.g / (float) denominator;
        if (denominator > 0) b = numerator.b / (float) denominator;

        return (r, g, b);

    }
    static Color UnpackColorToColor(uint packedColor)
    {

        byte r = (byte) ((((packedColor >> 12) & 15) / 15.0) * 255.0);
        byte g = (byte) ((((packedColor >> 8) & 15) / 15.0) * 255.0);
        byte b = (byte) ((((packedColor >> 4) & 15) / 15.0) * 255.0);
        return new Color(r,g,b);

    }
    
    // currently, assume max radius is 15
    static void SearchAndRecalculate((int x, int y) searchPos)
    {
        
        for (int x = searchPos.x - 30 < 0 ? 0 : searchPos.x - 30; x < (searchPos.x + 30 > worldSize ? worldSize : searchPos.x + 30); x++)
        {

            for (int y = searchPos.y - 30 < 0 ? 0 : searchPos.y - 30; y < (searchPos.y + 30 > worldSize ? worldSize : searchPos.y + 30); y++)
            {
                
                if (lights.ContainsKey((x, y)) && !_lightQueue.Contains((x,y)))
                {

                    _lightQueue.Enqueue((x, y));
                    
                }
                
            }
            
        }
        
    }

    static void RemoveAround((int x, int y) lightPos, (uint r, uint g, uint b) light)
    {

        int lightValue = (int) Math.Max(light.r, Math.Max(light.g, light.b));
        for (int x = lightPos.x - lightValue < 0 ? 0 : lightPos.x - lightValue; x < (lightPos.x + lightValue > worldSize ? worldSize : lightPos.x + lightValue); x++)
        {

            for (int y = lightPos.y - lightValue < 0 ? 0 : lightPos.y - lightValue; y < (lightPos.y + lightValue > worldSize ? worldSize : lightPos.y + lightValue); y++)
            {

                lightmap[x, y] = 0;
                
            }
            
        }
        
    }

    // temp array for lighting calculations
    private static uint[,] _tempMap = new uint[15, 15];
    private static List<double> _times = new();
    static void AddLight((int x, int y) lightPos)
    {

        Stopwatch s = Stopwatch.StartNew();
        // Array.Clear(lightmap);
        int amountSkipped = 0;
        // foreach ((int x, int y) lightPos in lights.Keys)
        (uint r, uint g, uint b) lightUp = (0, 0, 0);
        (uint r, uint g, uint b) lightDown = (0, 0, 0);
        (uint r, uint g, uint b) lightLeft = (0, 0, 0);
        (uint r, uint g, uint b) lightRight = (0, 0, 0);
        
        if (lightPos.x - 1 >= 0 && lights.ContainsKey((lightPos.x - 1, lightPos.y))) lightLeft = lights[(lightPos.x - 1, lightPos.y)];
        if (lightPos.x + 1 < worldSize && lights.ContainsKey((lightPos.x + 1, lightPos.y))) lightRight = lights[(lightPos.x + 1, lightPos.y)];
        if (lightPos.y - 1 >= 0 && lights.ContainsKey((lightPos.x, lightPos.y - 1))) lightUp = lights[(lightPos.x, lightPos.y - 1)];
        if (lightPos.y + 1 < worldSize && lights.ContainsKey((lightPos.x, lightPos.y + 1))) lightDown = lights[(lightPos.x, lightPos.y + 1)];

        int lightMaxBrightness = (int) Math.Max(lights[lightPos].r, Math.Max(lights[lightPos].g, lights[lightPos].b));
        
        if (lightLeft != lights[lightPos] || lightRight != lights[lightPos] || lightUp != lights[lightPos] || lightDown != lights[lightPos])
        // if (valueLeft != lights[lightPos].r || valueRight != lights[lightPos].r || valueUp != lights[lightPos].r || valueDown != lights[lightPos].r)
        {
            
            for (int x = 0; x < lightMaxBrightness; x++)
            {
                for (int y = 0; y < lightMaxBrightness; y++)
                {
                    int globalX = x + lightPos.x;
                    int globalY = y + lightPos.y;
                    if (globalX >= 0 && globalY >= 0 && globalX < worldSize && globalY < worldSize)
                    {
                        // if (!map[globalX, globalY]) _tempMap[x, y] = lights[lightPos];
                        if (!map[globalX, globalY]) _tempMap[x, y] = PackColor(lights[lightPos]);
                        if (x == 0 && y == 0) lightmap[globalX, globalY] = PackColor(lights[lightPos]); // lights[lightPos];
                        if (x != 0 || y != 0) 
                        {
                            
                            // float sampleX = _tempMap[x - 1 < 0 ? x : x - 1, y] / (float)lights[lightPos];
                            // float sampleY = _tempMap[x, y - 1 < 0 ? y : y - 1] / (float)lights[lightPos];
                            // float value = _tempMap[x, y] * ((x * sampleX) + (y * sampleY)) / (x + y);

                            // _tempMap[x, y] = (byte)Math.Floor(value);
                            
                            (uint r, uint g, uint b) sampledColor = UnpackColor(_tempMap[x, y]);
                            (float r, float g, float b) sampleX =
                                ColorDivide(UnpackColor(_tempMap[x - 1 < 0 ? x : x - 1, y]), lights[lightPos]);
                            (float r, float g, float b) sampleY = 
                                ColorDivide(UnpackColor(_tempMap[x, y - 1 < 0 ? y : y - 1]), lights[lightPos]);

                            float r = sampledColor.r * (((x * sampleX.r) + (y * sampleY.r)) / (x + y));
                            float g = sampledColor.g * (((x * sampleX.g) + (y * sampleY.g)) / (x + y));
                            float b = sampledColor.b * (((x * sampleX.b) + (y * sampleY.b)) / (x + y));

                            _tempMap[x, y] = PackColor(((uint)Math.Floor(r), (uint)Math.Floor(g), (uint)Math.Floor(b)));
                            
                        }

                        // uint val = Math.Clamp(lightmap[globalX, globalY] + (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))), 0, 15);
                        
                        // lightmap[globalX, globalY] = Math.Max(lightmap[globalX, globalY], (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))));
                        (uint r, uint g, uint b) tempColor = UnpackColor(_tempMap[x, y]);
                        uint floorR = (uint)Math.Round(tempColor.r * (1.0 - ((Distance((x, y), (0, 0))) / (float)lightMaxBrightness)));
                        uint floorG = (uint)Math.Round(tempColor.g * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        uint floorB = (uint)Math.Round(tempColor.b * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        
                        lightmap[globalX, globalY] = PackColor(
                            ColorMax(UnpackColor(lightmap[globalX, globalY]), (floorR, floorG, floorB))
                            );
                        // lightmap[globalX, globalY] = _tempMap[x, y];
                        // if (y > 0) lightmap[globalX, globalY] = (uint) val;

                    }
                }
            }
            Array.Clear(_tempMap);
            for (int x = 0; x < lightMaxBrightness; x++)
            {
                for (int y = 0; y < lightMaxBrightness; y++)
                {
                    int globalX = x + lightPos.x;
                    int globalY = lightPos.y - y;
                    if (globalX >= 0 && globalY >= 0 && globalX < worldSize && globalY < worldSize)
                    {
                        // if (!map[globalX, globalY]) _tempMap[x, y] = lights[lightPos];
                        if (!map[globalX, globalY]) _tempMap[x, y] = PackColor(lights[lightPos]);
                        if (x == 0 && y == 0) lightmap[globalX, globalY] = PackColor(lights[lightPos]); // lights[lightPos];
                        if (x != 0 || y != 0) 
                        {
                            
                            // float sampleX = _tempMap[x - 1 < 0 ? x : x - 1, y] / (float)lights[lightPos];
                            // float sampleY = _tempMap[x, y - 1 < 0 ? y : y - 1] / (float)lights[lightPos];
                            // float value = _tempMap[x, y] * ((x * sampleX) + (y * sampleY)) / (x + y);

                            // _tempMap[x, y] = (byte)Math.Floor(value);
                            
                            // (float r, float g, float b) sampleX = UnpackColor(_tempMap[x-1 < 0 ? x : x-1, y]) / lights[lightPos];
                            (uint r, uint g, uint b) sampledColor = UnpackColor(_tempMap[x, y]);
                            (float r, float g, float b) sampleX =
                                ColorDivide(UnpackColor(_tempMap[x - 1 < 0 ? x : x - 1, y]), lights[lightPos]);
                            (float r, float g, float b) sampleY = 
                                ColorDivide(UnpackColor(_tempMap[x, y - 1 < 0 ? y : y - 1]), lights[lightPos]);

                            float r = sampledColor.r * ((x * sampleX.r) + (y * sampleY.r)) / (x + y);
                            float g = sampledColor.g * ((x * sampleX.g) + (y * sampleY.g)) / (x + y);
                            float b = sampledColor.b * ((x * sampleX.b) + (y * sampleY.b)) / (x + y);

                            _tempMap[x, y] = PackColor(((uint)Math.Floor(r), (uint)Math.Floor(g), (uint)Math.Floor(b)));

                        }

                        // uint val = Math.Clamp(lightmap[globalX, globalY] + (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))), 0, 15);
                        
                        // lightmap[globalX, globalY] = Math.Max(lightmap[globalX, globalY], (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))));
                        (uint r, uint g, uint b) tempColor = UnpackColor(_tempMap[x, y]);
                        uint floorR = (uint)Math.Round(tempColor.r * (1.0 - ((Distance((x, y), (0, 0))) / (float)lightMaxBrightness)));
                        uint floorG = (uint)Math.Round(tempColor.g * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        uint floorB = (uint)Math.Round(tempColor.b * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        lightmap[globalX, globalY] = PackColor(
                            ColorMax(UnpackColor(lightmap[globalX, globalY]), (floorR, floorG, floorB))
                            );

                        // if (y > 0) lightmap[globalX, globalY] = (uint) val;

                    }
                }
            }
            Array.Clear(_tempMap);
            for (int x = 0; x < lightMaxBrightness; x++)
            {
                for (int y = 0; y < lightMaxBrightness; y++)
                {
                    int globalX = lightPos.x - x;
                    int globalY = lightPos.y - y;
                    if (globalX >= 0 && globalY >= 0 && globalX < worldSize && globalY < worldSize)
                    {
                        // if (!map[globalX, globalY]) _tempMap[x, y] = lights[lightPos];
                        if (!map[globalX, globalY]) _tempMap[x, y] = PackColor(lights[lightPos]);
                        if (x == 0 && y == 0) lightmap[globalX, globalY] = PackColor(lights[lightPos]); // lights[lightPos];
                        if (x != 0 || y != 0) 
                        {
                            
                            // float sampleX = _tempMap[x - 1 < 0 ? x : x - 1, y] / (float)lights[lightPos];
                            // float sampleY = _tempMap[x, y - 1 < 0 ? y : y - 1] / (float)lights[lightPos];
                            // float value = _tempMap[x, y] * ((x * sampleX) + (y * sampleY)) / (x + y);

                            // _tempMap[x, y] = (byte)Math.Floor(value);
                            
                            // (float r, float g, float b) sampleX = UnpackColor(_tempMap[x-1 < 0 ? x : x-1, y]) / lights[lightPos];
                            (uint r, uint g, uint b) sampledColor = UnpackColor(_tempMap[x, y]);
                            (float r, float g, float b) sampleX =
                                ColorDivide(UnpackColor(_tempMap[x - 1 < 0 ? x : x - 1, y]), lights[lightPos]);
                            (float r, float g, float b) sampleY = 
                                ColorDivide(UnpackColor(_tempMap[x, y - 1 < 0 ? y : y - 1]), lights[lightPos]);

                            float r = sampledColor.r * ((x * sampleX.r) + (y * sampleY.r)) / (x + y);
                            float g = sampledColor.g * ((x * sampleX.g) + (y * sampleY.g)) / (x + y);
                            float b = sampledColor.b * ((x * sampleX.b) + (y * sampleY.b)) / (x + y);

                            _tempMap[x, y] = PackColor(((uint)Math.Floor(r), (uint)Math.Floor(g), (uint)Math.Floor(b)));

                        }

                        // uint val = Math.Clamp(lightmap[globalX, globalY] + (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))), 0, 15);
                        
                        // lightmap[globalX, globalY] = Math.Max(lightmap[globalX, globalY], (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))));
                        (uint r, uint g, uint b) tempColor = UnpackColor(_tempMap[x, y]);
                        uint floorR = (uint)Math.Round(tempColor.r * (1.0 - ((Distance((x, y), (0, 0))) / (float)lightMaxBrightness)));
                        uint floorG = (uint)Math.Round(tempColor.g * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        uint floorB = (uint)Math.Round(tempColor.b * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        lightmap[globalX, globalY] = PackColor(
                            ColorMax(UnpackColor(lightmap[globalX, globalY]), (floorR, floorG, floorB))
                            );

                        // if (y > 0) lightmap[globalX, globalY] = (uint) val;

                    }
                }
            }
            Array.Clear(_tempMap);
            for (int x = 0; x < lightMaxBrightness; x++)
            {
                for (int y = 0; y < lightMaxBrightness; y++)
                {
                    int globalX = lightPos.x - x;
                    int globalY = y + lightPos.y;
                    if (globalX >= 0 && globalY >= 0 && globalX < worldSize && globalY < worldSize)
                    {
                        // if (!map[globalX, globalY]) _tempMap[x, y] = lights[lightPos];
                        if (!map[globalX, globalY]) _tempMap[x, y] = PackColor(lights[lightPos]);
                        if (x == 0 && y == 0) lightmap[globalX, globalY] = PackColor(lights[lightPos]); // lights[lightPos];
                        if (x != 0 || y != 0) 
                        {
                            
                            // float sampleX = _tempMap[x - 1 < 0 ? x : x - 1, y] / (float)lights[lightPos];
                            // float sampleY = _tempMap[x, y - 1 < 0 ? y : y - 1] / (float)lights[lightPos];
                            // float value = _tempMap[x, y] * ((x * sampleX) + (y * sampleY)) / (x + y);

                            // _tempMap[x, y] = (byte)Math.Floor(value);
                            
                            // (float r, float g, float b) sampleX = UnpackColor(_tempMap[x-1 < 0 ? x : x-1, y]) / lights[lightPos];
                            (uint r, uint g, uint b) sampledColor = UnpackColor(_tempMap[x, y]);
                            (float r, float g, float b) sampleX =
                                ColorDivide(UnpackColor(_tempMap[x - 1 < 0 ? x : x - 1, y]), lights[lightPos]);
                            (float r, float g, float b) sampleY = 
                                ColorDivide(UnpackColor(_tempMap[x, y - 1 < 0 ? y : y - 1]), lights[lightPos]);

                            float r = sampledColor.r * ((x * sampleX.r) + (y * sampleY.r)) / (x + y);
                            float g = sampledColor.g * ((x * sampleX.g) + (y * sampleY.g)) / (x + y);
                            float b = sampledColor.b * ((x * sampleX.b) + (y * sampleY.b)) / (x + y);

                            _tempMap[x, y] = PackColor(((uint)Math.Floor(r), (uint)Math.Floor(g), (uint)Math.Floor(b)));

                        }

                        // uint val = Math.Clamp(lightmap[globalX, globalY] + (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))), 0, 15);
                        
                        // lightmap[globalX, globalY] = Math.Max(lightmap[globalX, globalY], (uint)Math.Floor(_tempMap[x, y] * (1.0 - (Math.Floor(Distance((x, y), (0, 0))) / (float)lights[lightPos]))));
                        (uint r, uint g, uint b) tempColor = UnpackColor(_tempMap[x, y]);
                        uint floorR = (uint)Math.Round(tempColor.r * (1.0 - ((Distance((x, y), (0, 0))) / (float)lightMaxBrightness)));
                        uint floorG = (uint)Math.Round(tempColor.g * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        uint floorB = (uint)Math.Round(tempColor.b * (1.0 - ((Distance((x,y), (0,0))) / (float)lightMaxBrightness)));
                        lightmap[globalX, globalY] = PackColor(
                            ColorMax(UnpackColor(lightmap[globalX, globalY]), (floorR, floorG, floorB))
                            );

                        // if (y > 0) lightmap[globalX, globalY] = (uint) val;

                    }
                }
            }
            Array.Clear(_tempMap);
            
        }
        else
        {
            amountSkipped++;
            lightmap[lightPos.x, lightPos.y] = PackColor(ColorMax(UnpackColor(lightmap[lightPos.x, lightPos.y]), lights[lightPos]));
            // lightmap[lightPos.x, lightPos.y] = Math.Max(lightmap[lightPos.x, lightPos.y], lights[lightPos]);
        }
        s.Stop();
        
        _times.Add(s.Elapsed.TotalMilliseconds);
        double times = 0.0;
        foreach (double t in _times) times += t;
        Console.WriteLine($"Lighting calculations ({lights.Count - amountSkipped} lights took {_times.Last()}ms (current avg: {times/_times.Count})");
        // _previousBench = $"({lights.Count - amountSkipped} lights took {_times.Last()}ms";
    }

    static void SearchAdditionalLights((int x, int y) lightPos)
    {

        // int searchPerimeterRadius = lights[lightPos];

    }
    static float Distance((float x, float y) a, (float x, float y) b)
    {

        return (float) Math.Sqrt(Math.Pow(b.x-a.x,2) + Math.Pow(b.y-a.y,2));

    }

    static float ChebyshevDistance((float x, float y) a, (float x, float y) b)
    {

        return Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

    }
    
    static float ManhattanDistance((float x, float y) a, (float x, float y) b)
    {
        
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        
    }

    static bool DoesIntersect(Vector2 sample, int x, int y, int width, int height)
    {
        return sample.X >= x && sample.X < x + width &&
               sample.Y >= y && sample.Y < y + height;
    }

}