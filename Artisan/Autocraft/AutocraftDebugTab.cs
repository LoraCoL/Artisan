using Artisan.CraftingLogic;
using Artisan.RawInformation;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Autocraft
{
    internal unsafe static class AutocraftDebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;

        public static int DebugValue = 0;

        internal static void Draw()
        {
            ImGui.Checkbox("Debug logging", ref Debug);
            if (ImGui.CollapsingHeader("所有能工巧匠食物"))
            {
                foreach (var x in ConsumableChecker.GetFood())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("背包内的HQ食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }
            if (ImGui.CollapsingHeader("所有能工巧匠药水"))
            {
                foreach (var x in ConsumableChecker.GetPots())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("背包内的HQ药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }

            if (ImGui.CollapsingHeader("制作状态"))
            {
                ImGui.Text($"加工精度: {CharacterInfo.Control()}");
                ImGui.Text($"作业精度: {CharacterInfo.Craftsmanship()}");
                ImGui.Text($"当前耐久: {CurrentCraft.CurrentDurability}");
                ImGui.Text($"最大耐久: {CurrentCraft.MaxDurability}");
                ImGui.Text($"当前进展: {CurrentCraft.CurrentProgress}");
                ImGui.Text($"最大进展: {CurrentCraft.MaxProgress}");
                ImGui.Text($"当前品质: {CurrentCraft.CurrentQuality}");
                ImGui.Text($"最大品质: {CurrentCraft.MaxQuality}");
                ImGui.Text($"物品名称: {CurrentCraft.ItemName}");
                ImGui.Text($"当前状态: {CurrentCraft.CurrentCondition}");
                ImGui.Text($"当前步骤: {CurrentCraft.CurrentStep}");
                ImGui.Text($"当前快速制作步骤: {CurrentCraft.QuickSynthCurrent}");
                ImGui.Text($"最大快速制作步骤: {CurrentCraft.QuickSynthMax}");
                ImGui.Text($"内静+比尔格: {CurrentCraft.GreatStridesByregotCombo()}");
                ImGui.Text($"预期品质: {CurrentCraft.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
                ImGui.Text($"当前宏步骤: {CurrentCraft.MacroStep}");
                ImGui.Text($"Collectibility Low: {CurrentCraft.CollectabilityLow}");
                ImGui.Text($"Collectibility Mid: {CurrentCraft.CollectabilityMid}");
                ImGui.Text($"Collectibility High: {CurrentCraft.CollectabilityHigh}");
            }

            if (ImGui.CollapsingHeader("魔晶石精炼"))
            {
                ImGui.Text($"主手 精炼度: {Spiritbond.Weapon}");
                ImGui.Text($"副手 精炼度: {Spiritbond.Offhand}");
                ImGui.Text($"头部 精炼度: {Spiritbond.Helm}");
                ImGui.Text($"身体 精炼度: {Spiritbond.Body}");
                ImGui.Text($"手臂 精炼度: {Spiritbond.Hands}");
                ImGui.Text($"腿部 精炼度: {Spiritbond.Legs}");
                ImGui.Text($"脚部 精炼度: {Spiritbond.Feet}");
                ImGui.Text($"耳部 精炼度: {Spiritbond.Earring}");
                ImGui.Text($"颈部 精炼度: {Spiritbond.Neck}");
                ImGui.Text($"腕部 精炼度: {Spiritbond.Wrist}");
                ImGui.Text($"右指 精炼度: {Spiritbond.Ring1}");
                ImGui.Text($"左指 精炼度: {Spiritbond.Ring2}");

                ImGui.Text($"是否有已经满精炼度的装备: {Spiritbond.IsSpiritbondReadyAny()}");

            }
            ImGui.Separator();

            if (ImGui.Button("修复所有装备"))
            {
                RepairManager.ProcessRepair();
            }
            ImGuiEx.Text($"装备耐久: {RepairManager.GetMinEquippedPercent()}");
            ImGuiEx.Text($"选中的配方: {AgentRecipeNote.Instance()->SelectedRecipeIndex}");
            ImGuiEx.Text($"材料不足: {HQManager.InsufficientMaterials}");

            if (ImGui.Button($"打开长久模式的物品配方"))
            {
                CraftingLists.CraftingListFunctions.OpenRecipeByID((uint)Handler.RecipeID);
            }

            ImGui.InputInt("Debug Value", ref DebugValue);

            if (ImGui.Button($"打开并进行快速制作"))
            {
                CurrentCraft.QuickSynthItem(DebugValue);
            }
            if (ImGui.Button($"关闭快速制作窗口"))
            {
                CurrentCraft.CloseQuickSynthWindow();
            }
            if (ImGui.Button($"打开精制魔晶石窗口"))
            {
                Spiritbond.OpenMateriaMenu();
            }
            if (ImGui.Button($"精制其中一个魔晶石"))
            {
                Spiritbond.ExtractFirstMateria();
            }


            /*ImGui.InputInt("id", ref SelRecId);
            if (ImGui.Button("OpenRecipeByRecipeId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId((uint)SelRecId);
            }
            if (ImGui.Button("OpenRecipeByItemId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByItemId((uint)SelRecId);
            }*/
            //ImGuiEx.Text($"Selected recipe id: {*(int*)(((IntPtr)AgentRecipeNote.Instance()) + 528)}");




        }
    }
}
