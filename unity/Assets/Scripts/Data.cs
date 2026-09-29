using System.Collections.Generic;
using UnityEngine;

public enum Ing { None = 0, Bread = 1, Patty = 2, Lettuce = 3, Tomato = 4, Cheese = 5 }
public enum IState { Raw = 0, Prepped = 1, Burnt = 2 }   // prepped = chopped or cooked

// Every item in the game is one int, so the whole kitchen syncs as a small array of numbers.
//   0                      nothing
//   ing | state << 4       a loose ingredient
//   0x100 | mask << 9      a plate holding the ingredients in mask (bit per Ing)
public static class It
{
    public const int Plate = 0x100;
    public static int Ingr(Ing i, IState s = IState.Raw) => (int)i | ((int)s << 4);
    public static bool IsPlate(int c) => (c & Plate) != 0;
    public static int Mask(int c) => IsPlate(c) ? c >> 9 : 0;
    public static int PlateWith(int mask) => Plate | (mask << 9);
    public static Ing IngOf(int c) => IsPlate(c) ? Ing.None : (Ing)(c & 0xF);
    public static IState StateOf(int c) => (IState)((c >> 4) & 0xF);
    public static int Bit(Ing i) => 1 << (int)i;

    public static bool NeedsChop(Ing i) => i == Ing.Lettuce || i == Ing.Tomato || i == Ing.Cheese;
    public static bool Cooks(Ing i) => i == Ing.Patty;

    // may this loose ingredient go onto a plate?
    public static bool Ready(int c)
    {
        if (c == 0 || IsPlate(c)) return false;
        var i = IngOf(c); var s = StateOf(c);
        if (i == Ing.Bread) return s != IState.Burnt;
        return s == IState.Prepped;
    }

    public static string Name(Ing i) => i switch
    {
        Ing.Bread => "BREAD", Ing.Patty => "PATTY", Ing.Lettuce => "LETTUCE", Ing.Tomato => "TOMATO", Ing.Cheese => "CHEESE", _ => "",
    };

    public static string Icon(Ing i) => i switch
    {
        Ing.Bread => "bread", Ing.Patty => "meat-patty", Ing.Lettuce => "cabbage", Ing.Tomato => "tomato-slice", Ing.Cheese => "cheese-cut", _ => "plate",
    };
}

public class Recipe
{
    public string id, name, model;
    public int mask, value;
    public Ing[] parts;
    public Recipe(string id, string name, string model, int value, params Ing[] parts)
    {
        this.id = id; this.name = name; this.model = model; this.value = value; this.parts = parts;
        foreach (var p in parts) mask |= It.Bit(p);
    }
}

public static class Recipes
{
    public static readonly Recipe Cheeseburger = new Recipe("cheeseburger", "CHEESEBURGER", "burger-cheese", 30, Ing.Bread, Ing.Patty, Ing.Cheese);
    public static readonly Recipe Deluxe = new Recipe("deluxe", "DELUXE", "burger", 50, Ing.Bread, Ing.Patty, Ing.Cheese, Ing.Lettuce, Ing.Tomato);
    public static readonly Recipe Salad = new Recipe("salad", "SALAD", "salad", 20, Ing.Lettuce, Ing.Tomato);
    public static readonly Recipe Sandwich = new Recipe("sandwich", "SANDWICH", "sandwich", 25, Ing.Bread, Ing.Cheese, Ing.Lettuce);
    public static readonly Recipe[] All = { Cheeseburger, Deluxe, Salad, Sandwich };
    public static Recipe ByMask(int mask) { foreach (var r in All) if (r.mask == mask) return r; return null; }
    public static int Index(Recipe r) => System.Array.IndexOf(All, r);
}

public class KitchenDef
{
    public string id, name, tagline;
    public string[] rows;             // top row first; see Kitchen.cs for the legend
    public Recipe[] menu;
    public int[] stars;               // score for 1/2/3 stars with one chef (scaled for teams)
    public Color floorA, floorB, wall, accent;
}

public static class Kitchens
{
    // Legend: . floor   # counter   | divider counter   space = nothing
    //         B bread  M meat  L lettuce  T tomato  C cheese (crates)
    //         K chopping board  S stove  P plates  W serving window  X trash
    public static readonly KitchenDef[] All =
    {
        new KitchenDef
        {
            id = "burgerbar", name = "BURGER BAR", tagline = "One open kitchen. Learn the line.",
            rows = new[]
            {
                "#B#M#SS#KK#",
                "L.........W",
                "T.........#",
                "#...###...P",
                "C.........#",
                "#.........X",
                "#K#.......#",
                "###########",
            },
            menu = new[] { Recipes.Cheeseburger, Recipes.Salad, Recipes.Sandwich },
            stars = new[] { 120, 260, 420 },
            floorA = Kit.Hex("#f3e4c8"), floorB = Kit.Hex("#e2c9a2"), wall = Kit.Hex("#ff7a59"), accent = Kit.Hex("#ffcf4a"),
        },
        new KitchenDef
        {
            id = "splitshift", name = "SPLIT SHIFT", tagline = "A wall down the middle. Pass it across!",
            rows = new[]
            {
                "#M#SS#|#B#K##",
                "L.....|.....W",
                "T.....|.....P",
                "#.....|.....#",
                "#.....|.....X",
                "#.....|.....C",
                "#...........#",
                "#KX########K#",
            },
            menu = new[] { Recipes.Cheeseburger, Recipes.Deluxe, Recipes.Salad },
            stars = new[] { 100, 230, 380 },
            floorA = Kit.Hex("#d9ecf2"), floorB = Kit.Hex("#b8d6e0"), wall = Kit.Hex("#4a7dff"), accent = Kit.Hex("#7cf5c6"),
        },
    };
    public static KitchenDef Get(string id) { foreach (var k in All) if (k.id == id) return k; return All[0]; }
}

// Chef looks: Kenney mini characters.
public static class Looks
{
    public static readonly string[] All = { "male-e", "female-b", "male-c", "female-e", "male-a", "female-d", "male-f", "female-a" };
    public static readonly Color[] Colors = { Kit.Hex("#ff5a5f"), Kit.Hex("#3ec7ff"), Kit.Hex("#ffd23f"), Kit.Hex("#8cff6b") };
}

[System.Serializable]
public class SaveData
{
    public string kitchen = "burgerbar";
    public int look;
    public bool muted, howto;
    public int shifts;
    public List<string> bestIds = new List<string>();
    public List<int> bestScores = new List<int>();
    public List<int> bestStars = new List<int>();
    public int Best(string id) { int i = bestIds.IndexOf(id); return i < 0 ? 0 : bestScores[i]; }
    public int Stars(string id) { int i = bestIds.IndexOf(id); return i < 0 ? 0 : bestStars[i]; }
    public void Record(string id, int score, int stars)
    {
        int i = bestIds.IndexOf(id);
        if (i < 0) { bestIds.Add(id); bestScores.Add(score); bestStars.Add(stars); return; }
        bestScores[i] = Mathf.Max(bestScores[i], score); bestStars[i] = Mathf.Max(bestStars[i], stars);
    }
}
