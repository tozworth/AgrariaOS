using AgrariaOS.Configuration;
using AgrariaOS.Mail;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

internal sealed partial class MailWindow
{
    private int firstAttachment;
    private Rectangle previousAttachmentBounds;
    private Rectangle nextAttachmentBounds;
    private void DrawAttachments(SpriteBatch batch, MailRecord letter, ModConfig config, Point cursor)
    {
        batch.Draw(Game1.staminaRect, new Rectangle(attachmentsBounds.X, attachmentsBounds.Y, attachmentsBounds.Width, 1), UiStyle.Border * config.Opacity);
        const string label = "Attachments";
        int labelWidth = (int)MathF.Ceiling(UiText.Measure(label).X * config.FontScale) + Padding * 3;
        UiText.DrawCell(batch, label, new Rectangle(attachmentsBounds.X, attachmentsBounds.Y, labelWidth, attachmentsBounds.Height), UiStyle.Muted, config);
        int x = attachmentsBounds.X + labelWidth;
        int available = Math.Max(0, attachmentsBounds.Right - x - Padding);
        if (letter.Attachments.Count == 0)
        {
            string note = letter.Pending ? "Open letter to reveal contents" : "Previous contents unavailable";
            UiText.DrawCell(batch, note, new Rectangle(x, attachmentsBounds.Y, available, attachmentsBounds.Height), UiStyle.Muted, config);
            return;
        }
        int visible = Math.Max(1, available / 140);
        if (letter.Attachments.Count > visible)
        {
            available = Math.Max(0, available - 32);
            visible = Math.Max(1, available / 140);
            firstAttachment = Math.Clamp(firstAttachment, 0, letter.Attachments.Count - visible);
            Rectangle back = new(attachmentsBounds.Right - 32, attachmentsBounds.Y + 2, 14, attachmentsBounds.Height - 4);
            Rectangle next = new(attachmentsBounds.Right - 17, attachmentsBounds.Y + 2, 14, attachmentsBounds.Height - 4);
            UiText.DrawCell(batch, "<", back, firstAttachment > 0 ? UiStyle.Primary : UiStyle.Muted, config, padding: 1);
            UiText.DrawCell(batch, ">", next, firstAttachment + visible < letter.Attachments.Count ? UiStyle.Primary : UiStyle.Muted, config, padding: 1);
            if (firstAttachment > 0) previousAttachmentBounds = back;
            if (firstAttachment + visible < letter.Attachments.Count) nextAttachmentBounds = next;
        }
        else firstAttachment = 0;
        int width = available / Math.Min(visible, letter.Attachments.Count);
        foreach (MailAttachment attachment in letter.Attachments.Skip(firstAttachment).Take(visible))
        {
            Rectangle bounds = new(x, attachmentsBounds.Y + 2, Math.Max(0, width - 2), Math.Max(0, attachmentsBounds.Height - 4));
            if (bounds.Width <= IconSize + Padding * 2) break;
            bool claimable = attachment.Remaining > 0;
            if (claimable && bounds.Contains(cursor))
                batch.Draw(Game1.staminaRect, bounds, UiStyle.Border * config.Opacity);
            var data = ItemRegistry.GetDataOrErrorItem(attachment.ItemId);
            Rectangle source = data.GetSourceRect();
            float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
            if (scale >= 1) scale = MathF.Floor(scale);
            Rectangle icon = new(bounds.X + Padding, bounds.Y + (bounds.Height - (int)(source.Height * scale)) / 2,
                (int)(source.Width * scale), (int)(source.Height * scale));
            batch.Draw(data.GetTexture(), icon, source, Color.White * config.Opacity);
            string text = claimable ? $"Claim · {attachment.Name} x{attachment.Remaining}" : $"{attachment.Name} x{attachment.Quantity} · Claimed";
            UiText.DrawCell(batch, text, new Rectangle(bounds.X + IconSize + Padding, bounds.Y,
                Math.Max(0, bounds.Width - IconSize - Padding), bounds.Height), claimable ? UiStyle.Primary : UiStyle.Muted, config);
            if (claimable) attachmentButtons.Add((bounds, attachment));
            x += width;
        }
    }
}
