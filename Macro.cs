﻿using IL.JollyCoop;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace alphappy.TAMacro
{
    public class Macro
    {
        public List<Instruction> instructions = new List<Instruction>();
        public int lines;
        public List<int> lineNumbers = new List<int>();
        public List<string> lineTexts = new();
        public StringBuilder text = new StringBuilder();
        public Dictionary<string, int> labels = new Dictionary<string, int>();
        public List<int> newlinePositions = new() { 0 };

        public class Options
        {
            public enum Interference { Block, Overwrite, Pause, Kill }
            public Interference interference = Interference.Block;
            public string name = "";
            public Dictionary<string, string> unrecognized = new();
            public Macro macro;

            public Options(Macro macro) { this.macro = macro; }

            public void Set(string key, string value, bool store = true)
            {
                switch (key)
                {
                    case "PLAYER_INTERFERENCE":
                        interference = value switch
                        {
                            "block" => Interference.Block,
                            "overwrite" => Interference.Overwrite,
                            "pause" => Interference.Pause,
                            "kill" => Interference.Kill,
                            _ => throw new Exceptions.InvalidMacroOptionException("`PLAYER_INTERFERENCE` must have one of the following values:  `block`, `overwrite`, `pause`, `kill`"),
                        };
                        break;

                    case "NAME":
                        name = value; break;

                    case "GLOBAL_HOTKEY":
                        if (!Settings.allowMacroGlobalHotkeys.Value)
                            return;

                        if (!Enum.TryParse<KeyCode>(value, out var code))
                            throw new Exceptions.InvalidMacroOptionException($"`GLOBAL_HOTKEY` received an invalid KeyCode.  See https://docs.unity3d.com/ScriptReference/KeyCode.html for a list of valid KeyCodes.");

                        if (MacroLibrary.globalHotkeys.TryGetValue(code, out var otherMacro))
                            throw new Exceptions.InvalidMacroOptionException($"`GLOBAL_HOTKEY` received a KeyCode that is already in use by  macro `{otherMacro.FullName}`.");

                        if (Settings.AllKeys.FirstOrDefault(c => c.Value == code) is Configurable<KeyCode> configurable)
                            throw new Exceptions.InvalidMacroOptionException($"`GLOBAL_HOTKEY` received a KeyCode that is already in use by  `{configurable.key}` ({configurable.info.description}).");

                        MacroLibrary.globalHotkeys[code] = macro;

                        break;

                    default:
                        if (store) unrecognized[key] = value; break;
                }
            }

            public void SetFromCookbookMetadata(Dictionary<string, string> metadata)
            {
                foreach (var pair in metadata)
                {
                    if (pair.Key == "GLOBAL_HOTKEY") throw new Exceptions.InvalidMacroOptionException($"`{pair.Key}` cannot be a cookbook option.");
                    Set(pair.Key, pair.Value, false);
                }
            }
        }
        public Options options;

        public Instruction current => instructions[currentIndex];
        public int currentLine => lineNumbers[Mathf.Clamp(currentIndex, 0, instructions.Count - 1)];
        public string currentLineText { get { try { return lineTexts[currentLine]; } catch { return "<NONE>"; } } }
        public string name => options.name ?? "";
        public string FullName => $"{parent.FullName}/{name}";

        public int currentIndex = -1;
        public int hold;
        public bool readyToTick;
        public Player.InputPackage package;
        public int throwDirection;
        public Stack<object> stack = new Stack<object>();
        public bool returnNull = false;
        public MacroContainer parent;

        public Macro(MacroContainer parent)
        {
            this.parent = parent;
            options = new(this);
        }

        public static event Action<Macro> OnMacroEnded;
        public Player.InputPackage? GetPackage(Player player)
        {
            Mod.LogDebug($"  enter GetPackage");
            readyToTick = false;
            if (hold > 0) hold--; else currentIndex++;
            while (currentIndex < instructions.Count)
            {
                Mod.LogDebug($"  ({currentIndex:D4}) {current}");
                current.Enter(this, player);
                if (readyToTick) { return package; }
                if (returnNull) { returnNull = false; return null; }
                currentIndex++;
            }
            OnMacroEnded?.Invoke(this);
            return null;
        }

        public void Initialize(Player player)
        {
            currentIndex = -1; hold = 0; readyToTick = false; package = default; returnNull = false;
            throwDirection = player.ThrowDirection;
            stack.Clear();
            //DisplayPanel.TrackMe(this);
        }

        public void AddInstruction(Instruction inst)
        {
            instructions.Add(inst);
            lineNumbers.Add(lines);
            if (inst.type is InstructionType.DefineLabelFromString)
            {
                labels.Add((string)inst.value, instructions.Count);
            }
        }

        public static string RepFromInputList(List<List<Player.InputPackage>> lists, string setup = "")
        {
            var dt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            StringBuilder sb = new StringBuilder($"/NAME: {dt}\n{setup}");
            Player.InputPackage previous = default;
            int consecutive = -1;
            foreach (List<Player.InputPackage> list in lists)
            {
                foreach (Player.InputPackage input in list)
                {
                    if (consecutive == -1)
                    {
                        consecutive = 1;
                        previous = input;
                    }
                    else if (input.EqualTo(previous))
                    {
                        consecutive++;
                    }
                    else
                    {
                        sb.Append($"{previous.AsString()}~{consecutive}\n");
                        consecutive = 1;
                        previous = input;
                    }
                }
                if (!previous.IsNeutral() || !Settings.discardFinalNeutral.Value) sb.Append($"{previous.AsString()}~{consecutive}\n");
            }
            return sb.ToString();
        }
    }
}
