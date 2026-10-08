using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

partial class NotesWindow : OsWindow
{
    private List<NoteRecord> Docs()
    {
        if (!Context.IsWorldReady || Game1.player == null)
            return new List<NoteRecord>();
        long id = Game1.player.UniqueMultiplayerID;
        if (id != farmerId)
        {
            farmerId = id;
            boundDocId = null;
            firstItem = 0;
            UnfocusAll();
        }
        if (!config.Notes.TryGetValue(id, out List<NoteRecord>? docs) || docs == null)
            config.Notes[id] = docs = new List<NoteRecord>();
        return docs;
    }

    private void UnfocusAll()
    {
        focusedEntry = null;
        titleEntry.Selected = bodyEntry.Selected = addEntry.Selected = false;
    }

    private void Focus(TextEntry entry)
    {
        UnfocusAll();
        focusedEntry = entry;
        entry.Selected = true;
        try { Game1.keyboardDispatcher.Subscriber = entry; }
        catch { }
    }

    private NoteRecord? SelectedDoc()
    {
        string? id = tree.SelectedId;
        if (id == null || !Context.IsWorldReady || Game1.player == null)
            return null;
        if (!config.Notes.TryGetValue(Game1.player.UniqueMultiplayerID, out List<NoteRecord>? docs) || docs == null)
            return null;
        return docs.FirstOrDefault(doc => doc.Id == id);
    }

    private void EnsureBound(NoteRecord? doc)
    {
        string? id = doc?.Id;
        bool isTodo = doc?.Kind == "todo";
        if (id == boundDocId && isTodo == boundIsTodo)
            return;
        boundDocId = id;
        boundIsTodo = isTodo;
        UnfocusAll();
        firstItem = 0;
        itemScrollDragOffset = null;
        titleEntry.Bind(doc?.Title ?? "");
        bodyEntry.Bind(doc?.Body ?? "");
        addEntry.Bind("");
    }

    private static List<TreeNode> BuildRoots(List<NoteRecord> docs)
    {
        var roots = new List<TreeNode>(docs.Count);
        foreach (NoteRecord doc in docs)
        {
            bool isTodo = doc.Kind == "todo";
            string title = string.IsNullOrWhiteSpace(doc.Title)
                ? isTodo ? "Untitled list" : "Untitled note" : doc.Title.Trim();
            if (!isTodo)
            {
                roots.Add(new TreeNode(doc.Id, title));
                continue;
            }
            int total = doc.Items.Count;
            int done = doc.Items.Count(item => item.Done);
            string label = total == 0 ? title : $"{title} ({done}/{total})";
            roots.Add(new TreeNode(doc.Id, label, total > 0 && done == total));
        }
        return roots;
    }

    private void CreateDoc(string kind)
    {
        List<NoteRecord> docs = Docs();
        var doc = new NoteRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Title = kind == "todo" ? "Untitled list" : "Untitled note"
        };
        docs.Add(doc);
        saveConfig();
        tree.Select(doc.Id);
        EnsureBound(doc);
    }

    private void DeleteDoc(string id)
    {
        List<NoteRecord> docs = Docs();
        NoteRecord? doc = docs.FirstOrDefault(candidate => candidate.Id == id);
        if (doc == null)
            return;
        docs.Remove(doc);
        saveConfig();
        if (tree.SelectedId == id)
            tree.Select(null);
        if (boundDocId == id)
        {
            boundDocId = null;
            UnfocusAll();
        }
    }

    private void AddTodoItem()
    {
        NoteRecord? doc = SelectedDoc();
        if (doc == null || doc.Kind != "todo" || doc.Id != boundDocId)
            return;
        string text = addEntry.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;
        doc.Items.Add(new TodoRecord { Id = Guid.NewGuid().ToString("N"), Text = text });
        addEntry.Bind("");
        saveConfig();
        firstItem = int.MaxValue;
    }

}
