﻿using Artisan.RawInformation;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.MacroSystem
{
    internal class MacroUI
    {
        private static string _newMacroName = string.Empty;
        private static string renameMacro = string.Empty;
        private static bool _keyboardFocus;
        private const string MacroNamePopupLabel = "Macro Name";
        private static Macro selectedMacro = new();
        private static int selectedActionIndex = -1;
        private static bool renameMode = false;

        internal static void Draw()
        {
            ImGui.TextWrapped("此选项卡将允许您添加Artisan可以使用的宏，而不是由它自己决定下一步。");
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("从剪切板导入宏"))
                OpenMacroNamePopup(MacroNameUse.FromClipboard);

            ImGui.SameLine();

            if (ImGui.Button("新建宏"))
                OpenMacroNamePopup(MacroNameUse.NewMacro);

            DrawMacroNamePopup(MacroNameUse.FromClipboard);
            DrawMacroNamePopup(MacroNameUse.NewMacro);

            if (Service.Configuration.UserMacros.Count > 0)
            {
                ImGui.BeginGroup();
                float longestName = 0;
                foreach (var macro in Service.Configuration.UserMacros)
                {
                    if (ImGui.CalcTextSize($"{macro.Name} (消耗的制作力: {GetCPCost(macro)})").Length() > longestName)
                        longestName = ImGui.CalcTextSize($"{macro.Name} (消耗的制作力: {GetCPCost(macro)})").Length();
                }

                longestName = Math.Max(150, longestName);
                ImGui.Text("宏列表");
                if (ImGui.BeginChild("##selector", new Vector2(longestName + 40, 0), true))
                {
                    foreach (Macro m in Service.Configuration.UserMacros)
                    {
                        uint cpCost = GetCPCost(m);
                        var selected = ImGui.Selectable($"{m.Name} (消耗的制作力: {cpCost})###{m.ID}", m.ID == selectedMacro.ID);

                        if (selected)
                        {
                            selectedMacro = m;
                        }
                    }
                    ImGui.EndChild();
                }
                if (selectedMacro.ID != 0)
                {
                    ImGui.SameLine();
                    ImGui.BeginChild("###selectedMacro", new Vector2(0, 0), false);
                    if (!renameMode)
                    {
                        ImGui.Text($"已选中的宏: {selectedMacro.Name}");
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Pen))
                        {
                            renameMode = true;
                        }
                    }
                    else
                    {
                        renameMacro = selectedMacro.Name;
                        if (ImGui.InputText("", ref renameMacro, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            selectedMacro.Name = renameMacro;
                            Service.Configuration.Save();

                            renameMode = false;
                            renameMacro = String.Empty;
                        }
                    }
                    if (ImGui.Button("删除宏 (按下Ctrl)") && ImGui.GetIO().KeyCtrl)
                    {
                        Service.Configuration.UserMacros.Remove(selectedMacro);
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = null;

                        Service.Configuration.Save();
                        selectedMacro = new();
                        selectedActionIndex = -1;

                        Artisan.CleanUpIndividualMacros();
                    }
                    ImGui.Spacing();
                    ImGui.SameLine();
                    bool skipQuality = selectedMacro.MacroOptions.SkipQualityIfMet;
                    if (ImGui.Checkbox("当品质达到100%时跳过提升品质的技能", ref skipQuality))
                    {
                        selectedMacro.MacroOptions.SkipQualityIfMet = skipQuality;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("当品质达到100%时, 将会跳过所有与提升品质相关的技能, 包括增益。");
                    ImGui.SameLine();
                    bool upgradeActions = selectedMacro.MacroOptions.UpgradeActions;
                    if (ImGui.Checkbox("自动升级技能", ref upgradeActions))
                    {
                        selectedMacro.MacroOptions.UpgradeActions = upgradeActions;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("当状态为高品质或最高品质, 且当前宏处于提升品质或推动进展的步骤上（不包括比尔格的祝福）那么它将根据原始操作将技能升级为集中加工或集中制作。");

                    ImGui.Columns(2, "actionColumns", false);
                    if (ImGui.Button("插入新技能"))
                    {
                        if (selectedMacro.MacroActions.Count == 0)
                            selectedMacro.MacroActions.Add(Skills.BasicSynth);
                        else
                            selectedMacro.MacroActions.Insert(selectedActionIndex + 1, Skills.BasicSynth);

                        Service.Configuration.Save();
                    }
                    ImGui.TextWrapped("宏步骤");
                    ImGui.Indent();
                    for (int i = 0; i < selectedMacro.MacroActions.Count(); i++)
                    {
                        var selectedAction = ImGui.Selectable($"{i+1}. {GetActionName(selectedMacro.MacroActions[i])}###selectedAction{i}", i == selectedActionIndex);

                        if (selectedAction)
                            selectedActionIndex = i;
                    }
                    ImGui.Unindent();
                    if (selectedActionIndex != -1)
                    {
                        if (selectedActionIndex >= selectedMacro.MacroActions.Count)
                            return;

                        ImGui.NextColumn();
                        ImGui.Text($"选择的技能: {GetActionName(selectedMacro.MacroActions[selectedActionIndex])}");
                        if (selectedActionIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowLeft))
                            {
                                selectedActionIndex--;
                            }
                        }

                        if (selectedActionIndex < selectedMacro.MacroActions.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowRight))
                            {
                                selectedActionIndex++;
                            }
                        }


                        if (ImGui.Button("删除技能（按住Ctrl）") && ImGui.GetIO().KeyCtrl)
                        {
                            selectedMacro.MacroActions.RemoveAt(selectedActionIndex);
                            Service.Configuration.Save();

                            if (selectedActionIndex == selectedMacro.MacroActions.Count)
                                selectedActionIndex--;
                        }

                        if (ImGui.BeginCombo("###ReplaceAction", "替换技能"))
                        {
                            foreach(var constant in typeof(Skills).GetFields().OrderBy(x => GetActionName((uint)x.GetValue(null))))
                            {
                                if (ImGui.Selectable($"{GetActionName((uint)constant.GetValue(null))}"))
                                {
                                    selectedMacro.MacroActions[selectedActionIndex] = (uint)constant.GetValue(null);
                                    if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                        Service.Configuration.SetMacro = selectedMacro;

                                    Service.Configuration.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.Text("技能排序");
                        if (selectedActionIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowUp))
                            {
                                selectedMacro.MacroActions.Reverse(selectedActionIndex -1, 2);
                                selectedActionIndex--;
                                if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                    Service.Configuration.SetMacro = selectedMacro;

                                Service.Configuration.Save();
                            }
                        }

                        if (selectedActionIndex < selectedMacro.MacroActions.Count - 1)
                        {
                            ImGui.SameLine();
                            if (selectedActionIndex == 0)
                            {
                                ImGui.Dummy(new Vector2(22));
                                ImGui.SameLine();
                            }

                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowDown))
                            {
                                selectedMacro.MacroActions.Reverse(selectedActionIndex, 2);
                                selectedActionIndex++;
                                if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                    Service.Configuration.SetMacro = selectedMacro;

                                Service.Configuration.Save();
                            }
                        }

                    }
                    ImGui.Columns(1);
                    ImGuiEx.ImGuiLineCentered("MTimeHead", delegate
                    {
                        ImGuiEx.TextUnderlined($"预计宏执行用时");
                    });
                    ImGuiEx.ImGuiLineCentered("MTimeArtisan", delegate
                    {
                        ImGuiEx.Text($"Artisan: {GetMacroLength(selectedMacro)} 秒");
                    });
                    ImGuiEx.ImGuiLineCentered("MTimeTeamcraft", delegate
                    {
                        ImGuiEx.Text($"正常宏: {GetTeamcraftMacroLength(selectedMacro)} 秒");
                    });
                    ImGui.EndChild();
                }
                else
                {
                    selectedActionIndex = -1;
                }

                ImGui.EndGroup();
            }
            else
            {
                selectedMacro = new();
                selectedActionIndex = -1;
            }
        }

        private static uint GetCPCost(Macro m)
        {
            uint previousAction = 0;
            uint output = 0;
            foreach (var act in m.MacroActions)
            {
                if ((act == Skills.StandardTouch && previousAction == Skills.BasicTouch) || (act == Skills.AdvancedTouch && previousAction == Skills.StandardTouch))
                {
                    output += 18;
                    previousAction = act;
                    continue;
                }

                if (act >= 100000)
                {
                    output += LuminaSheets.CraftActions[act].Cost;
                }
                else
                {
                    output += LuminaSheets.ActionSheet[act].PrimaryCostValue;
                }

                previousAction = act;
            }

            return output;
        }

        private static double GetMacroLength(Macro m)
        {
            double output = 0;
            var delay = (double)Service.Configuration.AutoDelay;
            var delaySeconds = delay / 1000;

            foreach (var act in m.MacroActions)
            {
                if (ActionIsLengthyAnimation(act))
                {
                    output += 2.5 + delaySeconds;
                }
                else
                {
                    output += 1.25 + delaySeconds;
                }
            }

            return Math.Round(output, 2);

        }

        private static float GetTeamcraftMacroLength(Macro m)
        {
            float output = 0;
            foreach (var act in m.MacroActions)
            {
                if (ActionIsLengthyAnimation(act))
                {
                    output += 3f;
                }
                else
                {
                    output += 2f;
                }
            }

            return output;

        }
        private static bool ActionIsLengthyAnimation(uint id)
        {
            switch (id)
            {
                case Skills.BasicSynth:
                case Skills.RapidSynthesis:
                case Skills.MuscleMemory:
                case Skills.CarefulSynthesis:
                case Skills.FocusedSynthesis:
                case Skills.Groundwork:
                case Skills.DelicateSynthesis:
                case Skills.IntensiveSynthesis:
                case Skills.PrudentSynthesis:
                case Skills.BasicTouch:
                case Skills.HastyTouch:
                case Skills.StandardTouch:
                case Skills.PreciseTouch:
                case Skills.PrudentTouch:
                case Skills.FocusedTouch:
                case Skills.Reflect:
                case Skills.PreparatoryTouch:
                case Skills.AdvancedTouch:
                case Skills.TrainedFinesse:
                case Skills.ByregotsBlessing:
                case Skills.MastersMend:
                    return true;
                    default:
                    return false;
            };
        }


        private static string GetActionName(uint action)
        {
            if (LuminaSheets.CraftActions.TryGetValue(action, out var act1))
            {
                return act1.Name.RawString;
            }
            else
            {
                LuminaSheets.ActionSheet.TryGetValue(action, out var act2);
                return act2.Name.RawString;
            }
        }

        private static void DrawMacroNamePopup(MacroNameUse use)
        {
            if (ImGui.BeginPopup($"{MacroNamePopupLabel}{use}"))
            {
                if (_keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    _keyboardFocus = false;
                }

                if (ImGui.InputText("宏名称##macroName", ref _newMacroName, 64, ImGuiInputTextFlags.EnterReturnsTrue)
                 && _newMacroName.Any())
                {
                    switch (use)
                    {
                        case MacroNameUse.NewMacro:
                            Macro newMacro = new();
                            newMacro.Name = _newMacroName;
                            newMacro.SetID();
                            newMacro.Save(true);
                            break;
                        case MacroNameUse.FromClipboard:
                            try
                            {
                                var text = ImGui.GetClipboardText();
                                ParseMacro(text, out Macro macro);
                                if (macro.ID != 0)
                                    if (macro.Save())
                                    {
                                        Service.ChatGui.Print($"{macro.Name} 已保存.");
                                    }
                                    else
                                    {
                                        Service.ChatGui.PrintError("无法保存宏。请检查您的剪贴板是否包含带有工匠技能的宏。");
                                    }
                                else
                                    Service.ChatGui.PrintError("无法解析剪贴板。请检查您的剪贴板是否包含带有工匠技能的宏。");
                            }
                            catch (Exception e)
                            {
                                Dalamud.Logging.PluginLog.Information($"无法从剪切板保存新的宏：\n{e}");
                            }

                            break;
                    }

                    _newMacroName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void ParseMacro(string text, out Macro macro)
        {
            macro = new();
            macro.Name = _newMacroName;
            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line = "";
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    if (parts[0].Equals("/ac", StringComparison.CurrentCultureIgnoreCase) || parts[0].Equals("/action", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (parts[i].Contains("<")) continue;
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var action = builder.ToString().Trim();
                        action = action.Replace("\"", "");
                        if (string.IsNullOrEmpty(action)) continue;

                        if (LuminaSheets.CraftActions.Values.Any(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0))
                        {
                            var act = LuminaSheets.CraftActions.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"无法解析技能: {action}");
                            }
                            macro.MacroActions.Add(act.RowId);
                            continue;

                        }
                        else
                        {
                            var act = LuminaSheets.ActionSheet.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"无法解析技能: {action}");
                            }
                            macro.MacroActions.Add(act.RowId);
                            continue;

                        }
                    }
                }
            }
            if (macro.MacroActions.Count > 0)
                macro.SetID();
        }

        private static void OpenMacroNamePopup(MacroNameUse use)
        {
            _newMacroName = string.Empty;
            _keyboardFocus = true;
            ImGui.OpenPopup($"{MacroNamePopupLabel}{use}");
        }

        internal enum MacroNameUse
        {
            SaveCurrent,
            NewMacro,
            DuplicateMacro,
            FromClipboard,
        }
    }
}
