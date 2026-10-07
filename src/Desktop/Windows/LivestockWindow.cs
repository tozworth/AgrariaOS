using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

internal sealed class LivestockWindow : OsWindow
{
    internal const string WindowId = "Livestock";
    private const int Padding = GridView<Character>.Padding;
    private const int IconSize = 20;
    private readonly GridView<Character> grid = new(
        GridColumn<Character>.Create("Name", 210, 44, GetDisplayName, GetDisplayName,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell),
        GridColumn<Character>.Create("Type", 140, 36, GetTypeName, GetTypeName,
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase),
        GridColumn<Character>.Create("Hearts", 64, 28, GetHeartsText, GetFriendship, rightAligned: true),
        GridColumn<Character>.Create("Petted", 80, 36, GetPetText, GetPetSort),
        GridColumn<Character>.Create("Treat", 80, 36, GetTreatText, GetTreatText,
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase));

    internal override string Id => WindowId;
    internal override string Title => "Livestock";
    internal override string TitleSubtext
    {
        get
        {
            int count = GetAnimals().Length;
            return count == 1 ? "1 animal" : $"{count} animals";
        }
    }
    internal override int Icon => 74;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        grid.SetRows(GetAnimals());
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, "No animals.");
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick) => grid.ReceiveClick(cursor, leftClick);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    // Mirrors AnimalPage.FindAnimals/GetAllAnimals: pets first, then horses (including
    // ridden mounts), then farm animals ordered by base type, type and friendship.
    private static Character[] GetAnimals()
    {
        if (!Context.IsWorldReady)
            return Array.Empty<Character>();
        var pets = new List<Character>();
        var horses = new List<Character>();
        var livestock = new List<FarmAnimal>();
        Utility.ForEachLocation(location =>
        {
            foreach (NPC character in location.characters)
            {
                if (character is Pet && !character.hideFromAnimalSocialMenu.Value)
                    pets.Add(character);
                else if (character is Horse && !character.hideFromAnimalSocialMenu.Value)
                    horses.Add(character);
            }
            foreach (FarmAnimal animal in location.animals.Values)
            {
                if (!animal.hideFromAnimalSocialMenu.Value)
                    livestock.Add(animal);
            }
            return true;
        });
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            if (farmer.mount != null && !horses.Any(horse => ReferenceEquals(horse, farmer.mount)))
                horses.Add(farmer.mount);
        }
        IEnumerable<FarmAnimal> ordered = livestock
            .OrderBy(animal => GetBaseType(animal), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(animal => animal.type.Value ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(animal => animal.friendshipTowardFarmer.Value);
        return pets.Concat(horses).Concat<Character>(ordered).ToArray();
    }

    private static string GetDisplayName(Character animal)
    {
        string name = animal.displayName;
        return string.IsNullOrWhiteSpace(name) ? GetTypeName(animal) : name;
    }

    private static string GetTypeName(Character animal) => animal switch
    {
        FarmAnimal farmAnimal => string.IsNullOrEmpty(farmAnimal.displayType)
            ? farmAnimal.type.Value ?? "Animal" : farmAnimal.displayType,
        Pet pet => string.IsNullOrEmpty(pet.petType.Value) ? "Pet" : pet.petType.Value,
        Horse => "Horse",
        _ => animal.displayName ?? "Animal"
    };

    private static string GetBaseType(FarmAnimal animal)
    {
        string type = animal.type.Value ?? "";
        return type.Contains(' ') ? type.Split(' ')[1] : type;
    }

    private static int? GetFriendship(Character animal) => animal switch
    {
        FarmAnimal farmAnimal => farmAnimal.friendshipTowardFarmer.Value,
        Pet pet => pet.friendshipTowardFarmer.Value,
        _ => null
    };

    private static string GetHeartsText(Character animal)
        => GetFriendship(animal) is { } friendship
            ? (friendship / 200.0).ToString("0.#", CultureInfo.InvariantCulture) : "";

    // Mirrors AnimalEntry.WasPetYet: 2 hand-petted, 1 auto-petted, 0 unpetted, null untracked.
    private static int? GetPetSort(Character animal) => animal switch
    {
        FarmAnimal farmAnimal => farmAnimal.wasPet.Value ? 2 : farmAnimal.wasAutoPet.Value ? 1 : 0,
        Pet pet => pet.grantedFriendshipForPet.Value ? 2 : 0,
        _ => null
    };

    private static string GetPetText(Character animal) => GetPetSort(animal) switch
    {
        2 => "Yes",
        1 => "Auto",
        0 => "No",
        _ => ""
    };

    private static string GetTreatText(Character animal) => animal switch
    {
        FarmAnimal farmAnimal => farmAnimal.hasEatenAnimalCracker.Value ? "Cracker" : "",
        Horse horse => horse.ateCarrotToday ? "Carrot" : "",
        _ => ""
    };

    private static void DrawNameCell(SpriteBatch batch, Character animal, Rectangle bounds, ModConfig config)
    {
        if (TryGetSprite(animal, out Texture2D? texture, out Rectangle source)
            && texture != null && source.Width > 0 && source.Height > 0)
        {
            float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
            if (scale >= 1)
                scale = MathF.Floor(scale);
            int width = Math.Max(1, (int)(source.Width * scale));
            int height = Math.Max(1, (int)(source.Height * scale));
            Rectangle destination = new(bounds.X + Padding + (IconSize - width) / 2,
                bounds.Y + (bounds.Height - height) / 2, width, height);
            batch.Draw(texture, destination, source, Color.White * config.Opacity);
        }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, GetDisplayName(animal), bounds, UiStyle.Primary, config);
    }

    // Mirrors AnimalEntry's portrait source rects so the grid icons match the vanilla tab.
    private static bool TryGetSprite(Character animal, out Texture2D? texture, out Rectangle source)
    {
        texture = null;
        source = Rectangle.Empty;
        try
        {
            switch (animal)
            {
                case FarmAnimal farmAnimal:
                {
                    var sprite = farmAnimal.GetOrLoadTexture();
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    if (sprite.SourceRect.Height > 16)
                    {
                        source = string.Equals(farmAnimal.type.Value, "Ostrich", StringComparison.Ordinal)
                            ? new Rectangle(0, sprite.SourceRect.Height * 2 - 32, sprite.SourceRect.Width, 28)
                            : new Rectangle(0, sprite.SourceRect.Height * 2 - 28, sprite.SourceRect.Width, 28);
                    }
                    else
                        source = new Rectangle(0, 16, 16, 16);
                    return true;
                }
                case Pet pet:
                {
                    var sprite = pet.Sprite;
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    source = new Rectangle(0, sprite.SourceRect.Height * 2 - 24, sprite.SourceRect.Width, 24);
                    return true;
                }
                case Horse horse:
                {
                    var sprite = horse.Sprite;
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    source = new Rectangle(0, sprite.SourceRect.Height * 2 - 26, sprite.SourceRect.Width, 24);
                    return true;
                }
            }
        }
        catch
        {
            texture = null;
            source = Rectangle.Empty;
        }
        return false;
    }
}
