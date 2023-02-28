using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe class Handler
    {
        /*delegate IntPtr BeginSynthesis(IntPtr a1, IntPtr a2, IntPtr a3, int a4);
        [Signature("40 55 53 41 54 41 55 48 8B EC", DetourName = nameof(BeginSynthesisDetour), Fallibility = Fallibility.Infallible)]
        static Hook<BeginSynthesis>? BeginSynthesisHook;*/

        internal static bool Enable = false;
        internal static List<int>? HQData = null;
        internal static int RecipeID = 0;
        internal static string RecipeName { get => recipeName; set { if (value != recipeName) PluginLog.Verbose($"{value}"); recipeName = value; } }
        internal static CircularBuffer<long> Errors = new(5);
        private static string recipeName = "";



        internal static void Init()
        {
            SignatureHelper.Initialise(new Handler());
            //BeginSynthesisHook.Enable();
            Svc.Framework.Update += Framework_Update;
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
        }

        /*internal static IntPtr BeginSynthesisDetour(IntPtr a1, IntPtr a2, IntPtr a3, int a4)
        {
            var ret = BeginSynthesisHook.Original(a1, a2, a3, 4);
            var recipeId = *(int*)(a1 + 528);
            PluginLog.Debug($"Crafting recipe: {recipeId}");
            return ret;
        }*/

        private static void Toasts_ErrorToast(ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            if (Enable)
            {
                Errors.PushBack(Environment.TickCount64);
                if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000))
                {
                    //Svc.Chat.Print($"{Errors.Select(x => x.ToString()).Join(",")}");
                    Enable = false;
                }
            }
        }

        internal static void Dispose()
        {
            //BeginSynthesisHook?.Disable();
            //BeginSynthesisHook?.Dispose();
            Svc.Framework.Update -= Framework_Update;
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
        }

        private static void Framework_Update(Dalamud.Game.Framework framework)
        {
            if (Enable)
            {
                var isCrafting = Service.Condition[ConditionFlag.Crafting];
                var preparing = Service.Condition[ConditionFlag.PreparingToCraft];

                if (!Throttler.Throttle(0))
                {
                    return;
                }
                if (Service.Configuration.CraftingX && Service.Configuration.CraftX == 0)
                {
                    Enable = false;
                    Service.Configuration.CraftingX = false;
                    return;
                }
                if (Svc.Condition[ConditionFlag.Occupied39])
                {
                    Throttler.Rethrottle(1000);
                }
                if(AutocraftDebugTab.Debug) PluginLog.Verbose("Throttle success");
                if (HQData == null)
                {
                    ECommons.Logging.DuoLog.Error("HQ data is null");
                    Enable = false;
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ not null");
                if (Service.Configuration.Materia && Spiritbond.IsSpiritbondReadyAny())
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("Entered materia extraction");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    if (!Spiritbond.IsMateriaMenuOpen() && !isCrafting && !preparing)
                    {
                        Spiritbond.OpenMateriaMenu();
                    }
                    if (Spiritbond.IsMateriaMenuOpen() && !isCrafting && !preparing)
                    {
                        Spiritbond.ExtractFirstMateria();
                    }
                }
                else
                {
                    Spiritbond.CloseMateriaMenu();
                }

                if (Service.Configuration.Repair && !RepairManager.ProcessRepair(false) && ((Service.Configuration.Materia && !Spiritbond.IsSpiritbondReadyAny()) || (!Service.Configuration.Materia)))
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("Entered repair check");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Not crafting");
                        if (!Svc.Condition[ConditionFlag.Crafting]) RepairManager.ProcessRepair(true);
                    }
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Repair ok");
                if (Service.Configuration.AbortIfNoFoodPot && !ConsumableChecker.CheckConsumables(false))
                {
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting]) ConsumableChecker.CheckConsumables(true);
                    }
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Consumables success");
                {
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Addon visible");
                        if (addon->UldManager.NodeListCount >= 88 && !addon->UldManager.NodeList[88]->GetAsAtkTextNode()->AtkResNode.IsVisible)
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Error text not visible");
                            if (!HQManager.RestoreHQData(HQData, out var fin) || !fin)
                            {
                                if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ data finalised");
                                return;
                            }
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ data restored");
                            CurrentCraft.RepeatActualCraft();
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting])
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Addon invisible");
                            if (Throttler.Throttle(1000))
                            {
                                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Opening crafting log");
                                if (RecipeID == 0)
                                {
                                    CommandProcessor.ExecuteThrottled("/clog");
                                }
                                else
                                {
                                    if (AutocraftDebugTab.Debug) PluginLog.Debug($"Opening recipe {RecipeID}");
                                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal((uint)RecipeID);
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static void Draw()
        {
            ImGui.Checkbox("启用持续模式", ref Enable);
            ImGuiComponents.HelpMarker("开始持续模式制作前，您需要首先在制作笔记中选择配方和NQ/HQ材料分配。\n持续模式将自动重复选定的配方，类似于自动制作，但在这样做之前会考虑食药Buff。");
            ImGuiEx.Text($"配方: {RecipeName}\nHQ材料: {HQData?.Select(x => x.ToString()).Join(", ")}");
            bool requireFoodPot = Service.Configuration.AbortIfNoFoodPot;
            if (ImGui.Checkbox("使用食物和/或药水", ref requireFoodPot))
            {
                Service.Configuration.AbortIfNoFoodPot = requireFoodPot;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Artisan会寻找配置的食物或药水, 如果没有找到将会终止制作。");
            if (requireFoodPot)
            {
                {
                    ImGuiEx.TextV("食物:");
                    ImGui.SameLine(150f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == Service.Configuration.Food, out var item) ? $"{(Service.Configuration.FoodHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Food == 0 ? "无" : $"{(Service.Configuration.FoodHQ ? " " : "")}{Service.Configuration.Food}")}"))
                    {
                        if (ImGui.Selectable("禁用"))
                        {
                            Service.Configuration.Food = 0;
                        }
                        foreach (var x in ConsumableChecker.GetFood(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Food = x.Id;
                                Service.Configuration.FoodHQ = false;
                            }
                        }
                        foreach (var x in ConsumableChecker.GetFood(true, true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Food = x.Id;
                                Service.Configuration.FoodHQ = true;
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("药水:");
                    ImGui.SameLine(150f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == Service.Configuration.Potion, out var item) ? $"{(Service.Configuration.PotHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Potion == 0 ? "无" : $"{(Service.Configuration.PotHQ ? " " : "")}{Service.Configuration.Potion}")}"))
                    {
                        if (ImGui.Selectable("禁用"))
                        {
                            Service.Configuration.Potion = 0;
                        }
                        foreach (var x in ConsumableChecker.GetPots(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Potion = x.Id;
                                Service.Configuration.PotHQ = false;
                            }
                        }
                        foreach (var x in ConsumableChecker.GetPots(true, true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Potion = x.Id;
                                Service.Configuration.PotHQ = true;
                            }
                        }
                        ImGui.EndCombo();
                    }
                }
            }

            bool repairs = Service.Configuration.Repair;
            if (ImGui.Checkbox("自动修理", ref repairs))
            {
                Service.Configuration.Repair = repairs;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("如果启用，Artisan将在任何装备达到配置的修复阈值时自动使用暗物质修理您的装备。");
            if (Service.Configuration.Repair)
            {
                //ImGui.SameLine();
                ImGui.PushItemWidth(200);
                ImGui.SliderInt("##repairp", ref Service.Configuration.RepairPercent, 10, 100, $"{Service.Configuration.RepairPercent}%%");
            }


            bool materia = Service.Configuration.Materia;
            if (ImGui.Checkbox("自动精制魔晶石", ref materia))
            {
                Service.Configuration.Materia = materia;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("当身上的任意装备的精炼值达到100%之后将自动进行魔晶石精制。");

            ImGui.Checkbox("只制作X次", ref Service.Configuration.CraftingX);
            if (Service.Configuration.CraftingX)
            {
                ImGui.Text("制作次数:");
                ImGui.SameLine();
                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("###TimesRepeat", ref Service.Configuration.CraftX))
                {
                    if (Service.Configuration.CraftX < 0)
                        Service.Configuration.CraftX = 0;

                }
            }
        }

        internal static void DrawRecipeData()
        {
            if (HQManager.TryGetCurrent(out var d))
            {
                HQData = d;
            }
            var addonPtr = Service.GameGui.GetAddonByName("RecipeNote", 1);
            if (addonPtr == IntPtr.Zero)
            {
                return;
            }

            var addon = (AtkUnitBase*)addonPtr;
            if (addon == null)
            {
                return;
            }

            if (addon->IsVisible && addon->UldManager.NodeListCount >= 49)
            {
                try
                {
                    if (addon->UldManager.NodeList[49]->IsVisible)
                    {
                        var text = addon->UldManager.NodeList[49]->GetAsAtkTextNode()->NodeText;
                        var str = RawInformation.MemoryHelper.ReadSeString(&text);
                        var rName = "";

                        /*
                         *  0	3	2	Woodworking
                            1	1	5	Smithing
                            2	3	1	Armorcraft
                            3	2	4	Goldsmithing
                            4	3	4	Leatherworking
                            5	2	5	Clothcraft
                            6	4	6	Alchemy
                            7	5	6	Cooking

                            8	carpenter
                            9	blacksmith
                            10	armorer
                            11	goldsmith
                            12	leatherworker
                            13	weaver
                            14	alchemist
                            15	culinarian
                            (ClassJob - 8)
                         * 
                         * */

                        if (str.ExtractText().Length == 0) return;

                        if (str.ExtractText()[^1] == '')
                        {
                            rName += str.ExtractText().Remove(str.ExtractText().Length - 1, 1).Trim();
                        }
                        else
                        {
                          
                            rName += str.ExtractText().Trim();
                        }

                        if (Svc.Data.GetExcelSheet<Recipe>().TryGetFirst(x => x.ItemResult.Value.Name.RawString == rName, out var id))
                        {
                            RecipeID = (int)id.RowId;
                            RecipeName = id.ItemResult.Value.Name;
                        }
                    }

                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Setting Recipe ID");
                    RecipeID = 0;
                    RecipeName = "";
                }
            }
        }
    }
}
