using System;
using System.Linq;
using Todo.Core.Model;
using Todo.Tui.Input;
using Todo.Tui.Model;
using Todo.Tui.Views;
using Xunit;

namespace Todo.CLI.Tests.Tui;

/// <summary>Terminal.Gui keeps process-wide state, so UI tests must not run in parallel.</summary>
[CollectionDefinition("TerminalGui", DisableParallelization = true)]
public class TerminalGuiCollection;

[Collection("TerminalGui")]
public class MainWindowTests
{
    private static FakeTodoService Seeded()
    {
        var service = new FakeTodoService();
        service.Add("Pay the electricity bill", "L1", important: true, due: new DateTime(2026, 10, 30));
        service.Add("Book flights", "L1", due: new DateTime(2026, 11, 5));
        service.Add("Write report", "L2");
        service.Add("Already done", "L1", done: true);
        return service;
    }

    [Fact]
    public void Renders_TitleMenuContextItemsStatusAndKeyBar()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Shot("main").Run();

        var screen = ui.Screen("main");
        var lines = screen.Split('\n');
        Assert.Contains("Todo - Microsoft To Do", lines[0]);
        Assert.Contains("Todo  File  Edit  View  Help", lines[1]);
        Assert.Contains("List: All lists", lines[2]);
        Assert.Contains("3 shown, 3 open of 4", lines[2]);
        Assert.Contains("☐ ★ Pay the electricity bill", screen);
        Assert.Contains("! due 10-30", screen);
        Assert.DoesNotContain("Already done", screen);
        Assert.Contains("F1 Help", screen);
        Assert.Contains("Alt+N New", screen);
    }

    // ---- layout, resize, accessibility -----------------------------------------------------------------

    [Fact]
    public void Layout_NarrowTerminal_TruncatesSubjectsAndWrapsKeyBar()
    {
        var service = Seeded();
        service.Add("A very long subject that certainly will not fit into a narrow terminal window at all", "L2");
        using var ui = UiSession.Create(service, width: 62, height: 14);

        ui.Shot("narrow").Run();

        var screen = ui.Screen("narrow");
        Assert.Contains("…", screen);
        var keyRows = screen.Split('\n').Count(l => l.Contains("Alt+") || l.Contains("F1 Help"));
        Assert.True(keyRows >= 2, "key bar should wrap onto additional rows");
        Assert.All(screen.Split('\n').Take(14), line => Assert.True(line.Length <= 62 + 2, line));
    }

    [Fact]
    public void Layout_TooSmall_ShowsWarningInsteadOfUi()
    {
        using var ui = UiSession.Create(Seeded(), width: 50, height: 10);

        ui.Shot("small").Run();

        var screen = ui.Screen("small");
        Assert.Contains("Terminal too small: 50x10", screen);
        Assert.Contains("at least 60x12", screen);
        Assert.DoesNotContain("Pay the electricity bill", screen);
    }

    [Fact]
    public void Layout_ResizeToTooSmallAndBack_TogglesWarningAndRestoresUi()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Resize(40, 8).Shot("small").Resize(100, 24).Shot("normal").Run();

        Assert.Contains("Terminal too small", ui.Screen("small"));
        Assert.DoesNotContain("Terminal too small", ui.Screen("normal"));
        Assert.Contains("Pay the electricity bill", ui.Screen("normal"));
    }

    [Fact]
    public void Layout_ResizeNarrower_RecomputesRowWidths()
    {
        var service = new FakeTodoService();
        service.Add("A reasonably long subject line for resize testing purposes here", "L1", due: new DateTime(2026, 11, 5));
        using var ui = UiSession.Create(service, width: 110, height: 20);

        ui.Shot("wide").Resize(64, 20).Shot("narrow").Run();

        Assert.Contains("A reasonably long subject line for resize testing purposes here", ui.Screen("wide"));
        Assert.DoesNotContain("A reasonably long subject line for resize testing purposes here", ui.Screen("narrow"));
        Assert.Contains("…", ui.Screen("narrow"));
    }

    [Fact]
    public void Layout_ManyItems_ShowsHowManyAreOutsideTheWindow()
    {
        var service = new FakeTodoService();
        for (var i = 1; i <= 40; i++)
            service.Add($"Item {i:00}", "L1");
        using var ui = UiSession.Create(service, width: 100, height: 20);

        ui.Shot("top").Press("F8").Press("F8").Shot("scrolled").Run();

        Assert.Contains("more below", ui.Screen("top").Split('\n')[2]);
        Assert.DoesNotContain("more above", ui.Screen("top").Split('\n')[2]);
        Assert.Contains("more above", ui.Screen("scrolled").Split('\n')[2]);
    }

    [Fact]
    public void Glyphs_AsciiFallback_DrawsNoNonAsciiRowCharacters()
    {
        var service = new FakeTodoService();
        service.Add("Plain item", "L1", important: true);
        using var ui = UiSession.Create(service, glyphs: RowGlyphs.Ascii);

        ui.Shot("ascii").Run();

        var row = ui.Screen("ascii").Split('\n').First(l => l.Contains("Plain item"));
        Assert.StartsWith("[ ] * Plain item", row);
    }

    [Fact]
    public void KeyBar_AltUnconfirmed_AdvertisesCtrlShortcuts()
    {
        using var ui = UiSession.Create(Seeded(), alt: AltKeyStatus.Unconfirmed);

        ui.Shot("keys").Run();

        Assert.Contains("Ctrl+N New", ui.Screen("keys"));
        Assert.DoesNotContain("Alt+N New", ui.Screen("keys"));
    }

    [Fact]
    public void Menus_FOpensMenuBarAndAltFOpensFileMenu()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+F").Shot("file").Run();

        var screen = ui.Screen("file");
        Assert.Contains("New Item...", screen);
        Assert.Contains("Refresh", screen);
        Assert.Contains("Exit", screen);
    }

    // ---- create ---------------------------------------------------------------------------------------

    [Fact]
    public void Create_TypeSubjectAndPressEnter_AddsItemToDefaultList()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+N").Shot("dialog").Type("Buy milk").Press("Enter").WaitIdle().Shot("after").Run();

        Assert.Contains("New item", ui.Screen("dialog"));
        Assert.Contains("[focus: TextField]", ui.Screen("dialog"));
        var added = Assert.Single(service.Added);
        Assert.Equal("Buy milk", added.Subject);
        Assert.Equal("L1", added.ListId);
        Assert.Contains("Buy milk", ui.Screen("after"));
        Assert.Contains("Item added.", ui.Window.StatusText);
        Assert.Contains("[focus: ItemListView]", ui.Screen("after"));
    }

    [Fact]
    public void Create_BlankSubject_ShowsErrorAndEnterDoesNotCloseDialog()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+N").Press("Enter").Shot("blank").Run();

        Assert.Contains("! Subject is required.", ui.Screen("blank"));
        Assert.Contains("[top: ItemDialog]", ui.Screen("blank"));
        Assert.Empty(service.Added);
    }

    [Fact]
    public void Create_InvalidDueDate_ShowsInlineErrorAndBlocksOk()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+N").Type("Task").Press("Tab").Type("tomorrow").Press("Enter").Shot("bad").Run();

        Assert.Contains("! Invalid due date. Use yyyy-MM-dd or MM-dd.", ui.Screen("bad"));
        Assert.Contains("[top: ItemDialog]", ui.Screen("bad"));
        Assert.Empty(service.Added);
    }

    [Fact]
    public void Create_ValidDueDateAndImportant_ArePassedToTheService()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+N").Type("Renew passport").Press("Tab").Type("2026-12-24").Press("Tab").Press("Space").Press("Enter")
            .WaitIdle().Run();

        var added = Assert.Single(service.Added);
        Assert.Equal(new DateTime(2026, 12, 24), added.DueDate);
        Assert.True(added.IsImportant);
    }

    [Fact]
    public void Create_Escape_DiscardsTheDialog()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+N").Type("Never saved").Press("Esc").Shot("after").Run();

        Assert.Empty(service.Added);
        Assert.Contains("[top: MainWindow]", ui.Screen("after"));
        Assert.Contains("[focus: ItemListView]", ui.Screen("after"));
    }

    [Fact]
    public void Create_CtrlAlternativeKeyWorksToo()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service, alt: AltKeyStatus.Unconfirmed);

        ui.Press("Ctrl+N").Settle().Type("Via ctrl").Settle().Press("Enter").WaitIdle().Run();

        Assert.Equal("Via ctrl", Assert.Single(service.Added).Subject);
    }

    [Fact]
    public void Create_WhileListFilteredToWork_DefaultsToThatList()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+L").Press("CursorDown", "CursorDown", "Enter").Press("Alt+N").Type("Work thing").Press("Enter").WaitIdle().Run();

        Assert.Equal("L2", Assert.Single(service.Added).ListId);
    }

    // ---- update ---------------------------------------------------------------------------------------

    [Fact]
    public void Edit_ShowsExistingValuesAndSavesChanges()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+M").Shot("edit").Press("End").Type(" now").Press("Enter").WaitIdle().Run();

        var screen = ui.Screen("edit");
        Assert.Contains("Modify item", screen);
        Assert.Contains("Pay the electricity bill", screen);
        Assert.Contains("2026-10-30", screen);
        var saved = Assert.Single(service.Updated);
        Assert.Equal("Pay the electricity bill now", saved.Subject);
        Assert.Equal(new DateTime(2026, 10, 30), saved.DueDate);
        Assert.True(saved.IsImportant);
        Assert.Contains("Item saved.", ui.Window.StatusText);
    }

    [Fact]
    public void Edit_EnterOnAnItemOpensTheEditor()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Enter").Shot("edit").Run();

        Assert.Contains("[top: ItemDialog]", ui.Screen("edit"));
    }

    [Fact]
    public void Edit_ClearingTheDueDate_SendsNullDueDate()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+M").Press("Tab").Press("Ctrl+A").Press("Delete").Press("Enter").WaitIdle().Run();

        Assert.Null(Assert.Single(service.Updated).DueDate);
    }

    [Fact]
    public void Edit_ListIsReadOnly()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+M").Shot("edit").Run();

        Assert.Contains("List:  Inbox", ui.Screen("edit"));
        Assert.DoesNotContain("⟦ Inbox", ui.Screen("edit"));
    }

    [Fact]
    public void Complete_SpaceCompletesThenReopens()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Space").WaitIdle().Do(s => Assert.True(service.Updated[0].IsCompleted))
            .Press("Alt+A").Shot("withDone").Run();

        Assert.True(service.Updated[0].IsCompleted);
        Assert.Equal("Completed", service.Updated[0].Status);
        Assert.Contains("☑", ui.Screen("withDone"));
    }

    [Fact]
    public void Complete_ReopenCompletedItem()
    {
        var service = new FakeTodoService();
        service.Add("Done thing", "L1", done: true);
        using var ui = UiSession.Create(service);

        ui.Press("Alt+A").Press("Space").WaitIdle().Run();

        Assert.False(Assert.Single(service.Updated).IsCompleted);
        Assert.Equal("NotStarted", service.Updated[0].Status);
    }

    [Fact]
    public void Important_AltIMarksTheSelectedItem()
    {
        var service = new FakeTodoService();
        service.Add("Plain", "L1");
        using var ui = UiSession.Create(service);

        ui.Press("Alt+I").WaitIdle().Shot("after").Run();

        Assert.True(Assert.Single(service.Updated).IsImportant);
        Assert.Contains("☐ ★ Plain", ui.Screen("after"));
    }

    // ---- delete ---------------------------------------------------------------------------------------

    [Fact]
    public void Remove_ConfirmWithEnter_DeletesTheItem()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Delete").Shot("confirm").Press("Enter").WaitIdle().Shot("after").Run();

        Assert.Contains("Remove \"Pay the electricity bill\"?", ui.Screen("confirm"));
        Assert.Equal("Pay the electricity bill", Assert.Single(service.Deleted).Subject);
        Assert.DoesNotContain("Pay the electricity bill", ui.Screen("after"));
        Assert.Contains("[focus: ItemListView]", ui.Screen("after"));
    }

    [Fact]
    public void Remove_CancelWithEscape_KeepsTheItem()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Press("Alt+R").Press("Esc").Shot("after").Run();

        Assert.Empty(service.Deleted);
        Assert.Contains("Pay the electricity bill", ui.Screen("after"));
    }

    // ---- read: filters, lists, search ------------------------------------------------------------------

    [Fact]
    public void ShowCompleted_TogglesWithAltA()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+A").Shot("all").Press("Alt+A").Shot("open").Run();

        Assert.Contains("Already done", ui.Screen("all"));
        Assert.Contains("incl. completed", ui.Screen("all"));
        Assert.DoesNotContain("Already done", ui.Screen("open"));
    }

    [Fact]
    public void ChooseList_FiltersToOneList()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+L").Shot("picker").Press("CursorDown", "CursorDown", "Enter").Shot("work").Run();

        Assert.Contains("All lists", ui.Screen("picker"));
        Assert.Contains("Work", ui.Screen("picker"));
        var work = ui.Screen("work");
        Assert.Contains("List: Work", work);
        Assert.Contains("Write report", work);
        Assert.DoesNotContain("Book flights", work);
    }

    [Fact]
    public void Search_QuestionMarkOpensSearchAndFilters()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("?").Shot("dialog").Type("book").Press("Enter").Shot("result").Run();

        Assert.Contains("[focus: TextField]", ui.Screen("dialog"));
        var result = ui.Screen("result");
        Assert.Contains("search: \"book\"", result);
        Assert.Contains("Book flights", result);
        Assert.DoesNotContain("Write report", result);
    }

    [Fact]
    public void Search_NoMatches_ShowsEmptyMessage()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+S").Type("zzz").Press("Enter").Shot("none").Run();

        Assert.Contains("No items match the search.", ui.Screen("none"));
    }

    [Fact]
    public void Search_EmptyTextClearsTheFilter()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+S").Type("book").Press("Enter").Press("Alt+S").Press("Ctrl+A").Press("Delete").Press("Enter").Shot("cleared").Run();

        Assert.DoesNotContain("search:", ui.Screen("cleared"));
        Assert.Contains("Write report", ui.Screen("cleared"));
    }

    [Fact]
    public void EmptyAccount_ShowsHowToAddFirstItem()
    {
        using var ui = UiSession.Create(new FakeTodoService());

        ui.Shot("empty").Run();

        Assert.Contains("No items yet. Press Alt+N to add one.", ui.Screen("empty"));
    }

    [Fact]
    public void NoSelection_CommandsReportInsteadOfFailing()
    {
        using var ui = UiSession.Create(new FakeTodoService());

        ui.Press("Alt+M").Do(s => Assert.Contains("No item selected.", s.Window.StatusText))
            .Press("Delete").Do(s => Assert.Contains("No item selected.", s.Window.StatusText)).Run();
    }

    // ---- global keys -----------------------------------------------------------------------------------

    [Fact]
    public void Escape_OnTheMainWindow_DoesNotExit()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Esc").Shot("after").Run();

        Assert.Contains("[top: MainWindow]", ui.Screen("after"));
    }

    [Theory]
    [InlineData("F3")]
    [InlineData("Alt+X")]
    public void ExitKeys_StopTheApplication(string key)
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press(key).Run();

        Assert.False(ui.Window.IsRunning);
    }

    [Fact]
    public void F5_ReloadsFromTheService()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);
        var before = service.LoadCount;

        ui.Do(s => s.Service.Add("Added elsewhere", "L1")).Press("F5").WaitIdle().Shot("after").Run();

        Assert.True(service.LoadCount > before);
        Assert.Contains("Added elsewhere", ui.Screen("after"));
    }

    [Fact]
    public void F1_ShowsHelpListingTheKeys()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("F1").Shot("help").Press("Esc").Shot("closed").Run();

        Assert.Contains("Help - Todo", ui.Screen("help"));
        Assert.Contains("Alt+N", ui.Screen("help"));
        Assert.Contains("[top: MainWindow]", ui.Screen("closed"));
    }

    [Fact]
    public void F3_InsideADialog_CancelsItInsteadOfExiting()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+N").Press("F3").Shot("after").Run();

        Assert.Contains("[top: MainWindow]", ui.Screen("after"));
    }

    [Fact]
    public void PageKeys_MoveThroughTheList()
    {
        var service = new FakeTodoService();
        for (var i = 1; i <= 40; i++)
            service.Add($"Item {i:00}", "L1");
        using var ui = UiSession.Create(service, height: 20);

        ui.Press("F8").Do(s => Assert.True(s.Window.ItemList.SelectedItem > 5))
            .Press("F7").Do(s => Assert.Equal(0, s.Window.ItemList.SelectedItem)).Run();
    }

    // ---- errors ----------------------------------------------------------------------------------------

    [Fact]
    public void ServiceFailure_ShowsErrorInStatusAndDialogAndKeepsData()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Do(s => s.Service.FailNext = new InvalidOperationException("Graph is unavailable"))
            .Press("Space").Shot("error").Press("Enter").Shot("after").Run();

        var error = ui.Screen("error");
        Assert.Contains("Graph is unavailable", error);
        Assert.Contains("[top: MessageDialog]", error);
        Assert.Contains("Pay the electricity bill", ui.Screen("after"));
        Assert.Contains("! Graph is unavailable", ui.Window.StatusText);
        Assert.False(ui.Window.IsBusy);
    }

    // ---- Ctrl alternatives must beat the list's own Emacs-style bindings -------------------------------

    [Theory]
    [InlineData("Ctrl+N", "New item")]
    [InlineData("Ctrl+E", "Modify item")]
    [InlineData("Ctrl+R", "Remove item")]
    [InlineData("Ctrl+L", "Choose list")]
    [InlineData("Ctrl+F", "Search")]
    public void CtrlShortcuts_OpenTheirDialogEvenWithManyItems(string key, string dialogTitle)
    {
        using var ui = UiSession.Create(Seeded(), alt: AltKeyStatus.Unconfirmed);

        ui.Press(key).Settle().Shot("dialog").Run();

        Assert.Contains(dialogTitle, ui.Screen("dialog"));
    }

    [Fact]
    public void CtrlShortcuts_ToggleCommandsActOnTheSelectedItem()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service, alt: AltKeyStatus.Unconfirmed);

        ui.Press("Ctrl+T").WaitIdle().Press("Ctrl+P").WaitIdle().Press("Ctrl+A").Shot("all").Run();

        Assert.Equal(2, service.Updated.Count);
        Assert.Contains("incl. completed", ui.Screen("all"));
    }

    [Fact]
    public void TypingInTheList_DoesNotActivateCommandsExceptSpaceAndSearch()
    {
        var service = Seeded();
        using var ui = UiSession.Create(service);

        ui.Type("xyz").Settle().Run();

        Assert.Empty(service.Updated);
        Assert.Empty(service.Added);
        Assert.Empty(service.Deleted);
    }
}
