using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>Two-column controls table shown on first play.</summary>
public static class ControlsIntroHud
{
    private readonly record struct ControlRow(string Control, string Description);

    private const int TitleSize = 36;
    private const int RowSize = 20;
    private const int HintSize = 18;
    private const int RowGap = 10;
    private const int ColumnGap = 40;
    private const int PanelPaddingX = 48;
    private const int PanelPaddingTop = 64;
    private const int PanelPaddingBottom = 56;

    private static readonly Color Accent = new(255, 220, 40, 255);
    private static readonly Color ControlColor = Color.RayWhite;
    private static readonly Color DescriptionColor = new(200, 200, 200, 255);
    private static readonly Color HintColor = new(180, 180, 180, 255);

    public static void Draw(int hudWidth, int hudHeight, bool awaitingStartInput)
    {
        var rows = BuildRows();

        int maxControlWidth = 0;
        int maxDescriptionWidth = 0;
        foreach (var row in rows)
        {
            maxControlWidth = Math.Max(maxControlWidth, MeasureText(row.Control, RowSize));
            maxDescriptionWidth = Math.Max(maxDescriptionWidth, MeasureText(row.Description, RowSize));
        }

        int tableWidth = maxControlWidth + ColumnGap + maxDescriptionWidth;
        int tableHeight = rows.Count * (RowSize + RowGap) - RowGap;
        int panelWidth = tableWidth + PanelPaddingX * 2;
        int panelHeight = PanelPaddingTop + tableHeight + PanelPaddingBottom;

        int panelX = (hudWidth - panelWidth) / 2;
        int panelY = (hudHeight - panelHeight) / 2;

        DrawRectangle(0, 0, hudWidth, hudHeight, new Color(0, 0, 0, 160));
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 20, 240));
        DrawRectangleLines(panelX, panelY, panelWidth, panelHeight, Accent);

        const string title = "CONTROLS";
        int titleWidth = MeasureText(title, TitleSize);
        DrawText(title, (hudWidth - titleWidth) / 2, panelY + 16, TitleSize, Accent);

        int controlX = panelX + PanelPaddingX;
        int descriptionX = controlX + maxControlWidth + ColumnGap;
        int rowY = panelY + PanelPaddingTop;

        foreach (var row in rows)
        {
            DrawText(row.Control, controlX, rowY, RowSize, ControlColor);
            DrawText(row.Description, descriptionX, rowY, RowSize, DescriptionColor);
            rowY += RowSize + RowGap;
        }

        string hint = awaitingStartInput
            ? "Press W, A, S, or D to start"
            : "Press C or Esc to close";
        int hintWidth = MeasureText(hint, HintSize);
        DrawText(hint, (hudWidth - hintWidth) / 2, panelY + panelHeight - 36, HintSize, HintColor);
    }

    private static List<ControlRow> BuildRows()
    {
        var rows = new List<ControlRow>
        {
            new("W A S D", "Move"),
            new("Mouse", "Look around"),
            new("Mouse left", "Fire"),
            new("1-4", "Select weapon"),
            new("C", "Toggle Controls HUD"),
            new("E", "Open doors, activate exit"),
            new("H", "Toggle high score board"),
            new("R", "Restart level"),
            new(OperatingSystem.IsBrowser() ? "Period" : "| (Pipe)", "Toggle console"),
        };

        if (OperatingSystem.IsBrowser())
        {
            rows.Add(new("F11", "Fullscreen (reload with Ctrl+R)"));
            rows.Add(new("Click canvas", "Activate game"));
        }

        rows.Add(new("Esc", "Options menu"));

        return rows;
    }
}
