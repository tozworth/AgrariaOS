using StardewValley;

namespace AgrariaOS.Mail;

internal static class MailText
{
    internal static bool HasItems(string raw)
    {
        foreach (string part in raw.Split("%item").Skip(1))
        {
            string? command = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
            if (command is "id" or "object" or "tools" or "bigobject" or "furniture" or "itemrecovery")
                return true;
        }
        return false;
    }
    // Only resolves text. Never construct a live LetterViewerMenu to preview mail:
    // its constructor grants money/recipes and executes %action commands.
    internal static string Resolve(string id, Dictionary<string, string> mail)
    {
        if (!id.StartsWith("passedOut", StringComparison.Ordinal))
            return mail.GetValueOrDefault(id, id.Contains('^') ? id : "");
        string[] args = ArgUtility.SplitBySpace(id);
        if (args.Length < 2 || !int.TryParse(args[1], out int cost))
            return mail.GetValueOrDefault(id, "");
        string key = args[0];
        if (key == "passedOut")
        {
            key = Utility.CreateDaySaveRandom(cost).Next(Game1.player.getSpouse()?.Name == "Harvey" ? 2 : 3) switch
            {
                0 => Game1.MasterPlayer.hasCompletedCommunityCenter() && !Game1.MasterPlayer.mailReceived.Contains("JojaMember")
                    ? "passedOut4" : $"passedOut1_{(cost > 0 ? "Billed" : "NotBilled")}_{(Game1.player.IsMale ? "Male" : "Female")}",
                1 => "passedOut2",
                _ => $"passedOut3_{(cost > 0 ? "Billed" : "NotBilled")}"
            };
        }
        return string.Format(Dialogue.applyGenderSwitchBlocks(Game1.player.Gender, mail.GetValueOrDefault(key, "")), cost);
    }

    internal static string Title(string id, string raw)
    {
        string[] parts = raw.Split("[#]");
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            return parts[1];
        if (id.StartsWith("passedOut", StringComparison.Ordinal))
            return "Recovery notice";
        string name = id.Replace("Cooking", "");
        return Game1.getCharacterFromName(name)?.displayName ?? (string.IsNullOrEmpty(id) ? "Letter" : id);
    }

    internal static string Body(string raw)
    {
        string body = raw.Split("[#]")[0].Replace("@", Game1.player.Name);
        body = Dialogue.applyGenderSwitch(Game1.player.Gender, body, altTokenOnly: true);
        body = Dialogue.applyGenderSwitchBlocks(Game1.player.Gender, body);
        foreach (string command in new[] { "%action", "%item" })
        {
            int start;
            while ((start = body.IndexOf(command, StringComparison.Ordinal)) >= 0)
            {
                int end = body.IndexOf("%%", start, StringComparison.Ordinal);
                if (end < 0) break;
                body = body.Remove(start, end + 2 - start);
            }
        }
        for (int start = body.IndexOf('['); start >= 0; start = body.IndexOf('[', start + 1))
        {
            int end = body.IndexOf(']', start);
            if (end < 0) break;
            string tag = body.Substring(start + 1, end - start - 1);
            if (tag.StartsWith("letterbg ", StringComparison.Ordinal) || tag.StartsWith("textcolor ", StringComparison.Ordinal))
            {
                body = body.Remove(start, end - start + 1);
                start--;
            }
        }
        if (body.Contains("%secretsanta", StringComparison.Ordinal))
            body = body.Replace("%secretsanta", Game1.season == Season.Winter && Game1.dayOfMonth >= 18 && Game1.dayOfMonth <= 25
                ? Utility.GetRandomWinterStarParticipant().displayName : "???", StringComparison.Ordinal);
        return body;
    }
}
