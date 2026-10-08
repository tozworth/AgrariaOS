using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;

namespace AgrariaOS.Hud;

/// <summary>Thin contours outside the current sprite's alpha, in world pixels.</summary>
internal sealed class AnimalQueueOutline
{
    private readonly Dictionary<(Texture2D Texture, Rectangle Frame, int Layer), Rectangle[]> contours = new();

    internal void Clear() => contours.Clear();

    internal void Draw(SpriteBatch batch, Character animal, Color color, int layer)
    {
        if (animal.currentLocation != Game1.currentLocation || animal.Sprite?.Texture is not { IsDisposed: false } texture)
            return;
        AnimatedSprite sprite = animal.Sprite;
        Rectangle frame = sprite.SourceRect;
        if (frame.Width <= 0 || frame.Height <= 0)
            return;
        Vector2 anchor;
        Vector2 origin = Vector2.Zero;
        float scale = 1f;
        float rotation = 0f;
        bool flip = sprite.CurrentAnimation != null && sprite.CurrentAnimation[sprite.currentAnimationIndex].flip;
        if (animal is FarmAnimal livestock)
        {
            float offset = livestock.yJumpOffset * 2;
            bool swimming = livestock.IsActuallySwimming();
            if (swimming && livestock.GetAnimalData()?.GetShadow(livestock.isBaby(), swimming)?.Visible != false)
                offset += (int)((Math.Sin(Game1.currentGameTime.TotalGameTime.TotalSeconds * 4 + livestock.bobOffset) + 0.5) * 3);
            anchor = Utility.snapDrawPosition(Game1.GlobalToLocal(Game1.viewport,
                livestock.Position + new Vector2(0, -24 + offset)));
            flip |= livestock.FacingDirection == 3;
        }
        else if (animal is Pet pet)
        {
            if (pet.IsInvisible)
                return;
            anchor = pet.getLocalPosition(Game1.viewport)
                + new Vector2(sprite.SpriteWidth * 4 / 2, pet.GetBoundingBox().Height / 2);
            origin = new Vector2(sprite.SpriteWidth / 2, sprite.SpriteHeight * 3f / 4f) * 4;
            scale = Math.Max(0.2f, pet.Scale);
            rotation = pet.rotation;
            flip |= pet.flip;
        }
        else
            return;
        // Cull before reading/caching any texture data. Rotation needs a generous radius.
        float radius = Math.Max(frame.Width, frame.Height) * 4 * scale + 32;
        if (anchor.X + radius < 0 || anchor.Y + radius < 0
            || anchor.X - radius > Game1.viewport.Width || anchor.Y - radius > Game1.viewport.Height)
            return;
        var key = (texture, frame, layer);
        if (!contours.TryGetValue(key, out Rectangle[]? edges))
        {
            Color[] pixels = new Color[frame.Width * frame.Height];
            texture.GetData(0, frame, pixels, 0, pixels.Length);
            edges = BuildContour(pixels, frame.Width, frame.Height, 1 + layer * 2);
            contours.Add(key, edges);
        }
        Color tint = Color.Lerp(color, Color.White, 0.45f) * 0.45f;
        Matrix turn = Matrix.CreateRotationZ(rotation);
        foreach (Rectangle edge in edges)
        {
            int x = flip ? frame.Width * 4 - edge.Right : edge.X;
            Vector2 offset = (new Vector2(x, edge.Y) - origin) * scale;
            Vector2 position = anchor + Vector2.Transform(offset, turn);
            batch.Draw(Game1.staminaRect, position, null, tint, rotation, Vector2.Zero,
                new Vector2(edge.Width * scale, edge.Height * scale), SpriteEffects.None, 1f);
        }
    }

    /// <summary>Upscale alpha to native 4x pixels, then trace a one-world-pixel outer ring.</summary>
    internal static Rectangle[] BuildContour(Color[] pixels, int width, int height, int radius)
    {
        int stride = width * 4 + radius * 2;
        int rows = height * 4 + radius * 2;
        int[] distance = new int[stride * rows];
        Array.Fill(distance, int.MaxValue);
        Queue<int> frontier = new();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].A == 0)
                    continue;
                for (int dy = 0; dy < 4; dy++)
                {
                    for (int dx = 0; dx < 4; dx++)
                    {
                        int index = (y * 4 + radius + dy) * stride + x * 4 + radius + dx;
                        distance[index] = 0;
                        frontier.Enqueue(index);
                    }
                }
            }
        }
        while (frontier.TryDequeue(out int index))
        {
            int next = distance[index] + 1;
            if (next > radius)
                continue;
            void Visit(int neighbor)
            {
                if (distance[neighbor] <= next)
                    return;
                distance[neighbor] = next;
                frontier.Enqueue(neighbor);
            }
            if (index % stride > 0) Visit(index - 1);
            if (index % stride < stride - 1) Visit(index + 1);
            if (index >= stride) Visit(index - stride);
            if (index < distance.Length - stride) Visit(index + stride);
        }
        List<Rectangle> edges = new();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < stride; x++)
            {
                if (distance[y * stride + x] != radius)
                    continue;
                int start = x;
                while (x + 1 < stride && distance[y * stride + x + 1] == radius)
                    x++;
                edges.Add(new Rectangle(start - radius, y - radius, x - start + 1, 1));
            }
        }
        return edges.ToArray();
    }
}
