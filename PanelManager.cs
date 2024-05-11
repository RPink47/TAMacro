﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace alphappy.TAMacro
{
    public class PanelManager : Panel
    {
        public Panel lastHovered;
        public Panel lastClicked;
        public bool clicking;
        public Vector2 lastCursor;
        public PanelManager() : base(new(-Futile.stage.GetPosition(), RWCustom.Custom.rainWorld.options.ScreenSize), Futile.stage) { }
        public PanelManager(RainWorldGame game) : base(new(-Futile.stage.GetPosition(), RWCustom.Custom.rainWorld.options.ScreenSize), Futile.stage)
        {
            Panel main = new(new(300f, 300f, 300f, 250f), this);
            main.CreateBackdrop();

            main.CreateAndGotoPanel(new(5f, 235f, 290f, 15f), true, $"TAMacro v{Const.PLUGIN_VERSION}", "Drag to move", true)
                .CreateMouseEvent(PanelReactions.DragParent);

            main.CreateAndGotoPanel(new(5f, 190f, 30f, 30f), true, "<", $"Back one page [{Settings.kbPrevPage.Value}]", true)
                .CreateFireEvent(() => MacroLibrary.ChangePage(-1));

            main.CreateAndGotoPanel(new(45f, 190f, 30f, 30f), true, ">", $"Forward one page [{Settings.kbNextPage.Value}]", true)
                .CreateFireEvent(() => MacroLibrary.ChangePage(1));

            main.CreateAndGotoPanel(new(85f, 190f, 30f, 30f), true, "^", $"Up one level [{Settings.kbUpOne.Value}]", true)
                .CreateFireEvent(MacroLibrary.UpOne);

            main.CreateAndGotoPanel(new(125f, 190f, 30f, 30f), true, "L", $"Reload all macros [{Settings.kbReloadLibrary.Value}]", true)
                .CreateFireEvent(MacroLibrary.ReloadFromTopLevel);

            main.CreateAndGotoPanel(new(165f, 190f, 30f, 30f), true, "X", $"Interrupt currently running macro [{Settings.kbInterrupt.Value}]", true)
                .CreateFireEvent(MacroLibrary.TerminateMacro);

            Panel recordingButton = 
                main.CreateAndGotoPanel(new(205f, 190f, 30f, 30f), true, null, $"Toggle input recording [{Settings.kbToggleRecording.Value}]", true)
                .CreateFireEvent(MacroLibrary.ToggleRecording);
            recordingButton.CreateLabelCentered("title", "R", out var recordingButtonLabel);
            MacroLibrary.OnToggleRecording += now => recordingButtonLabel.text = now ? "!REC!" : "R";

            main.CreateLabel("curdir", ".", new(5f, 180f), out var curdirlabel);
            curdirlabel.alignment = FLabelAlignment.Left;
            MacroLibrary.OnDirectoryChange += c => { if (curdirlabel != null) curdirlabel.text = c.parent == null ? "<root>" : c.DisplayName; };

            for (int i = 0; i < 10; i++)
            {
                int j = i;
                Panel p = main.CreateAndGotoPanel(new(5f, 155f - (i * 15f), 290f, 13f), true, "", null, true)
                              .CreateFireEvent(() => MacroLibrary.SelectOnPage(j, game));
                MacroLibrary.OnPageChange += c =>
                {
                    var s = c.IsCookbook ? c.SelectMacroOnViewedPage(j)?.name : c.SelectContainerOnViewedPage(j)?.name;
                    if (s != null)
                    {
                        (p["title"] as FLabel).text = s;
                        p.isVisible = true;
                    }
                    else
                    { 
                        p.isVisible = false; 
                    }
                };
            }

            Panel macroPanel = new(new(600f, 300f, 300f, 450f), this);
            macroPanel.CreateBackdrop()
                .CreateAndGotoPanel(new(5f, 435f, 290f, 15f), true, null, "Drag to move", true)
                .CreateLabelCentered("title", "No macro selected", out var macroPanelTitle)
                .CreateMouseEvent(PanelReactions.DragParent);

            Panel macroTextPanel = macroPanel.CreateAndGotoPanel(new(5f, 5f, 290f, 420f), false);

            macroTextPanel.CreateLabel("text", "", default, out var macroLabel);
            macroLabel.alignment = FLabelAlignment.Left;

            FSprite macroCursor = new("Futile_White")
            {
                width = 290,
                height = macroLabel.FontLineHeight,
                isVisible = false,
                alpha = 0.15f,
                color = new Color(0.7f, 0.7f, 0.1f),
            };
            macroCursor.isVisible = false;
            macroTextPanel.AddChild(macroCursor, "cursor");

            int total_lines = 26;
            int initial_jump_at = 21;
            int jump_size = 20;

            MacroLibrary.OnMacroTick += macro =>
            {
                var line = macro.currentLine;
                var line_offset = line < initial_jump_at ? 0 : jump_size * (1 + (line - initial_jump_at) / jump_size);
                var firstLine = line_offset;
                var lastLine = Math.Min(line_offset + total_lines, macro.lines);
                if (Const.SUPER_DEBUG_MODE) Mod.Log($"{line} {line_offset} {firstLine} {lastLine} {macro.newlinePositions.Count}");
                var firstPos = macro.newlinePositions[firstLine];
                var lastPos = macro.newlinePositions[lastLine];
                macroLabel.text = macro.text.ToString().Substring(firstPos, lastPos - firstPos);

                macroLabel.SetPosition(5.05f, 425.05f - (macroLabel.GetFixedWidthBounds().height / 2));
                macroPanelTitle.text = macro.name;
                macroCursor.isVisible = MacroLibrary.activeMacro != null;
                macroCursor.SetPosition(150.05f, 425.05f - ((line - line_offset) * macroLabel.FontLineHeight));
            };
        }

        public void Update()
        {
            Vector2 nowCursor = Input.mousePosition;
            var nowClicking = Input.GetMouseButton(0);

            if (nowCursor != lastCursor)
            {
                var nowHovered = WhoIsHovered(nowCursor);

                if (nowHovered != lastHovered)
                {
                    nowHovered?.StartedHovering(nowCursor);
                    lastHovered?.StoppedHovering(nowCursor);
                    lastHovered = nowHovered;
                }

                if (clicking && nowClicking)
                {
                    lastClicked?.Dragged(nowCursor - lastCursor);
                }
                lastCursor = nowCursor;
            }

            if (!clicking && nowClicking)
            {
                lastHovered?.StartedClicking(nowCursor);
                lastClicked = lastHovered;
                clicking = true;
            }
            else if (clicking && !nowClicking)
            {
                lastClicked?.StoppedClicking(nowCursor);
                lastClicked = null;
                clicking = false;
            }
        }

        public static PanelManager instance;
        public static void Initialize(RainWorldGame game) { instance = new PanelManager(game); Futile.stage.AddChild(instance); }
        public static void Shutdown() { instance.Destroy(); MacroLibrary.ClearEvents(); }
        public static void Frame() => instance.Update();
    }
}
