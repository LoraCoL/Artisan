﻿using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static Recipe? SelectedRecipe = null;
        internal static string Search = "";
        internal unsafe static InventoryManager* invManager = InventoryManager.Instance();
        public static Dictionary<Recipe, bool> CraftableItems = new();
        internal static List<int> SelectedRecipeRawIngredients = new();
        internal static List<int> SelectedListMaterials = new();
        internal static Dictionary<int, bool> SelectedRecipesCraftable = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = String.Empty;
        internal static CraftingList selectedList = new();
        public static Dictionary<uint, Recipe> FilteredList = LuminaSheets.RecipeSheet.Values
                    .DistinctBy(x => x.ItemResult.Value.Name.RawString)
                    .OrderBy(x => x.RecipeLevelTable.Value.ClassJobLevel)
                    .ThenBy(x => x.ItemResult.Value.Name.RawString)
                    .ToDictionary(x => x.RowId, x => x);

        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static List<int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static uint selectedListItem;
        public static bool Processing = false;
        public static uint CurrentProcessedItem;
        private static bool renameMode = false;
        private static string renameList;

        internal static void Draw()
        {
            string hoverText = "!! 悬停以获取信息 !!";
            var hoverLength = ImGui.CalcTextSize(hoverText);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (hoverLength.Length() / 2));
            ImGui.TextColored(ImGuiColors.DalamudYellow, hoverText);
            if (ImGui.IsItemHovered())
            {
                ImGui.TextWrapped($"您可以使用此选项卡查看您可以使用背包中的物品制作哪些物品。您还可以使用它来创建Artisan将尝试完成的快速制作清单。");
                ImGui.TextWrapped($"请注意，由于繁重的计算需求，过滤了配方列表以仅显示您拥有其材料的配方，不会考虑任何制作品的材料。这可能会在未来得到解决。目前，它*仅*会查看给定配方的最终成分。");
                ImGui.TextWrapped($"制作清单从上到下处理，因此请确保高优先级的制作品排在第一位。");
                ImGui.TextWrapped("请确保为每个需要制作物品的职业保存套装。此外，如果您需要使用食物和/或药水，请在开始制作清单之前这样做，因为这不会自动进行。");
            }
            ImGui.Separator();

            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            if (ImGui.Button("新建清单"))
            {
                keyboardFocus = true;
                ImGui.OpenPopup("新的制作清单");
            }

            DrawNewListPopup();

            if (Processing)
            {
                ImGui.Text("Currently processing list...");
                return;
            }

            if (Service.Configuration.CraftingLists.Count > 0)
            {
                ImGui.BeginGroup();
                float longestName = 0;
                foreach (var list in Service.Configuration.CraftingLists)
                {
                    if (ImGui.CalcTextSize($"{list.Name}").Length() > longestName)
                        longestName = ImGui.CalcTextSize($"{list.Name}").Length();
                }

                longestName = Math.Max(150, longestName);
                ImGui.Text("制作清单");
                if (ImGui.BeginChild("###craftListSelector", new Vector2(longestName + 40, 0), true))
                {
                    foreach (CraftingList l in Service.Configuration.CraftingLists)
                    {
                        var selected = ImGui.Selectable($"{l.Name}###list{l.ID}", l.ID == selectedList.ID);

                        if (selected)
                        {
                            selectedList = l;
                            SelectedListMaterials.Clear();
                            listMaterials.Clear();
                        }
                    }

                    Teamcraft.DrawTeamCraftListButtons();

                    ImGui.EndChild();

                }

                if (selectedList.ID != 0)
                {
                    ImGui.SameLine();
                    if (ImGui.BeginChild("###selectedList", new Vector2(0, ImGui.GetContentRegionAvail().Y), false))
                    {
                        if (!renameMode)
                        {
                            ImGui.Text($"选择清单: {selectedList.Name}");
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Pen))
                            {
                                renameMode = true;
                            }
                        }
                        else
                        {
                            renameList = selectedList.Name;
                            if (ImGui.InputText("", ref renameList, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                selectedList.Name = renameList;
                                Service.Configuration.Save();

                                renameMode = false;
                                renameList = String.Empty;
                            }
                        }
                        if (ImGui.Button("删除清单（按住Ctrl）") && ImGui.GetIO().KeyCtrl)
                        {
                            Service.Configuration.CraftingLists.Remove(selectedList);

                            Service.Configuration.Save();
                            selectedList = new();

                            SelectedListMaterials.Clear();
                            listMaterials.Clear();
                        }

                        if (selectedList.Items.Count > 0)
                        {
                            if (ImGui.CollapsingHeader("清单物品"))
                            {

                                ImGui.Columns(2, null, false);
                                ImGui.Text("当前物品");
                                ImGui.Indent();
                                var loop = 1;
                                foreach (var item in CollectionsMarshal.AsSpan(selectedList.Items.Distinct().ToList()))
                                {
                                    var selected = ImGui.Selectable($"{loop}. {FilteredList[item].ItemResult.Value.Name.RawString} x{selectedList.Items.Count(x => x == item)} {(FilteredList[item].AmountResult > 1 ? $"({FilteredList[item].AmountResult * selectedList.Items.Count(x => x == item)} total)" : $"")}", selectedListItem == item);

                                    if (selected)
                                    {
                                        selectedListItem = item;
                                    }

                                    loop++;
                                }
                                ImGui.Unindent();
                                ImGui.NextColumn();
                                if (selectedListItem != 0)
                                {
                                    ImGui.Text("选项");
                                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                                    {
                                        selectedList.Items.RemoveAll(x => x == selectedListItem);
                                        selectedListItem = 0;
                                        Service.Configuration.Save();

                                        SelectedListMaterials.Clear();
                                        listMaterials.Clear();
                                    }
                                    ImGui.SameLine();
                                    var count = selectedList.Items.Count(x => x == selectedListItem);

                                    ImGui.PushItemWidth(100);
                                    if (ImGui.InputInt("调整数量", ref count))
                                    {
                                        if (count > 0)
                                        {
                                            var oldCount = selectedList.Items.Count(x => x == selectedListItem);
                                            if (oldCount < count)
                                            {
                                                var diff = count - oldCount;
                                                for (int i = 1; i <= diff; i++)
                                                {
                                                    selectedList.Items.Insert(selectedList.Items.IndexOf(selectedListItem), selectedListItem);
                                                }
                                                Service.Configuration.Save();

                                                SelectedListMaterials.Clear();
                                                listMaterials.Clear();
                                            }
                                            if (count < oldCount)
                                            {
                                                var diff = oldCount - count;
                                                for (int i = 1; i <= diff; i++)
                                                {
                                                    selectedList.Items.Remove(selectedListItem);
                                                }
                                                Service.Configuration.Save();

                                                SelectedListMaterials.Clear();
                                                listMaterials.Clear();
                                            }
                                        }
                                    }


                                    if (!selectedList.ListItemOptions.ContainsKey(selectedListItem))
                                    {
                                        selectedList.ListItemOptions.TryAdd(selectedListItem, new ListItemOptions());
                                    }
                                    selectedList.ListItemOptions.TryGetValue(selectedListItem, out var options);
                                    bool NQOnly = options.NQOnly;
                                    if (ImGui.Checkbox("简易制作该物品", ref NQOnly))
                                    {
                                        options.NQOnly = NQOnly;
                                        Service.Configuration.Save();
                                    }

                                    string preview = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var prevMacro) && prevMacro != null ? Service.Configuration.IndividualMacros[(uint)selectedListItem].Name : "";
                                    if (prevMacro is not null && !Service.Configuration.UserMacros.Where(x => x.ID == prevMacro.ID).Any())
                                    {
                                        preview = "";
                                        Service.Configuration.IndividualMacros[(uint)selectedListItem] = null;
                                        Service.Configuration.Save();
                                    }

                                    if (Service.Configuration.UserMacros.Count > 0)
                                    {
                                        ImGui.Spacing();
                                        ImGui.Text($"用宏制作此配方（仅当启用宏模式时）");
                                        if (ImGui.BeginCombo("", preview))
                                        {
                                            if (ImGui.Selectable(""))
                                            {
                                                Service.Configuration.IndividualMacros[selectedListItem] = null;
                                                Service.Configuration.Save();
                                            }
                                            foreach (var macro in Service.Configuration.UserMacros)
                                            {
                                                bool selected = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var selectedMacro) && selectedMacro != null;
                                                if (ImGui.Selectable(macro.Name, selected))
                                                {
                                                    Service.Configuration.IndividualMacros[(uint)selectedListItem] = macro;
                                                    Service.Configuration.Save();
                                                }
                                            }

                                            ImGui.EndCombo();
                                        }
                                    }
                                    ImGui.Spacing();


                                    if (selectedList.Items.Distinct().Count() > 1)
                                    {
                                        ImGui.Text("清单排序");
                                        ImGui.SameLine();

                                        bool isFirstItem = selectedList.Items.IndexOf(selectedListItem) == 0;
                                        bool isLastItem = selectedList.Items.LastIndexOf(selectedListItem) == selectedList.Items.Count - 1;

                                        if (!isFirstItem)
                                        {
                                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                                            {
                                                var loops = selectedList.Items.Count(x => x == selectedListItem);
                                                var previousNum = selectedList.Items[selectedList.Items.IndexOf(selectedListItem) - 1];
                                                var insertionIndex = selectedList.Items.IndexOf(previousNum);

                                                selectedList.Items.RemoveAll(x => x == selectedListItem);
                                                for (int i = 1; i <= loops; i++)
                                                {
                                                    selectedList.Items.Insert(insertionIndex, selectedListItem);
                                                }

                                            }
                                            if (!isLastItem) ImGui.SameLine();
                                        }

                                        if (!isLastItem)
                                        {
                                            if (isFirstItem)
                                            {
                                                ImGui.Dummy(new Vector2(22));
                                                ImGui.SameLine();
                                            }

                                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                                            {
                                                var nextNum = selectedList.Items[selectedList.Items.LastIndexOf(selectedListItem) + 1];
                                                var loops = selectedList.Items.Count(x => x == nextNum);
                                                var insertionIndex = selectedList.Items.IndexOf(selectedListItem);

                                                selectedList.Items.RemoveAll(x => x == nextNum);
                                                for (int i = 1; i <= loops; i++)
                                                {
                                                    selectedList.Items.Insert(insertionIndex, nextNum);
                                                }

                                            }
                                        }
                                    }

                                }
                            }
                            ImGui.Columns(1, null, false);
                            if (ImGui.CollapsingHeader("总材料"))
                            {
                                DrawTotalIngredientsTable();
                            }
                            if (ImGui.Button("开始制作清单", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                            {
                                CraftingListFunctions.CurrentIndex = 0;
                                Processing = true;
                            }
                        }
                        ImGui.Spacing();
                        ImGui.Separator();

                        DrawRecipeData();

                        ImGui.EndChild();
                    }
                }

            }
            else
            {
                Teamcraft.DrawTeamCraftListButtons();
            }
        }



        private static void DrawTotalIngredientsTable()
        {
            if (ImGui.BeginTable("###ListMaterialTableRaw", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("物品栏", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (SelectedListMaterials.Count == 0)
                {
                    foreach (var item in selectedList.Items)
                    {
                        Recipe r = FilteredList[item];
                        AddRecipeIngredientsToList(r, ref SelectedListMaterials, false);
                    }
                }

                if (listMaterials.Count == 0)
                    listMaterials = SelectedListMaterials.OrderByDescending(x => x).Distinct().ToList();

                try
                {
                    foreach (var item in CollectionsMarshal.AsSpan(listMaterials))
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item]) continue;
                            ImGui.PushID(item);
                            var name = sheetItem.Name.RawString;
                            var count = SelectedListMaterials.Count(x => x == item);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            if (NumberOfIngredient((uint)item) >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.Text($"{NumberOfIngredient((uint)item)}");
                            ImGui.PopID();
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }

            if (ImGui.BeginTable("###ListMaterialTableSub", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("物品栏", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (SelectedListMaterials.Count == 0)
                {
                    foreach (var item in selectedList.Items)
                    {
                        Recipe r = FilteredList[item];
                        AddRecipeIngredientsToList(r, ref SelectedListMaterials, false);
                    }
                }

                if (listMaterials.Count == 0)
                    listMaterials = SelectedListMaterials.OrderByDescending(x => x).Distinct().ToList();

                try
                {
                    foreach (var item in CollectionsMarshal.AsSpan(listMaterials))
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item])
                            {
                                ImGui.PushID(item);
                                var name = sheetItem.Name.RawString;
                                var count = SelectedListMaterials.Count(x => x == item);
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{name}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{count}");
                                ImGui.TableNextColumn();
                                if (NumberOfIngredient((uint)item) >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                                ImGui.Text($"{NumberOfIngredient((uint)item)}");
                                ImGui.PopID();
                            }
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }
        }

        private static void DrawNewListPopup()
        {
            if (ImGui.BeginPopup("新的制作清单"))
            {
                if (keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    keyboardFocus = false;
                }

                if (ImGui.InputText("清单名称###listName", ref newListName, 100, ImGuiInputTextFlags.EnterReturnsTrue) && newListName.Any())
                {
                    CraftingList newList = new();
                    newList.Name = newListName;
                    newList.SetID();
                    newList.Save(true);

                    newListName = String.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawRecipeData()
        {
            bool showOnlyCraftable = Service.Configuration.ShowOnlyCraftable;

            if (ImGui.Checkbox("只显示你有材料的配方（切换刷新）", ref showOnlyCraftable))
            {
                Service.Configuration.ShowOnlyCraftable = showOnlyCraftable;
                Service.Configuration.Save();
                CraftableItems.Clear();
            }

            string preview = SelectedRecipe is null ? "" : SelectedRecipe.ItemResult.Value.Name.RawString;
            if (ImGui.BeginCombo("选择配方", preview))
            {
                DrawRecipes();

                ImGui.EndCombo();
            }

            if (SelectedRecipe != null)
            {
                if (ImGui.CollapsingHeader("配方信息"))
                {
                    DrawRecipeOptions();
                }
                if (SelectedRecipeRawIngredients.Count == 0)
                    AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

                if (ImGui.CollapsingHeader("原材料"))
                {
                    ImGui.Text($"所需原材料");
                    DrawRecipeSubTable();
                }
                ImGui.PushItemWidth(-1f);
                if (ImGui.Button("添加到清单", new Vector2(ImGui.GetContentRegionAvail().X / 2, 30)))
                {
                    SelectedListMaterials.Clear();
                    listMaterials.Clear();

                    if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                    {
                        selectedList.Items.Add(SelectedRecipe.RowId);
                    }
                    else
                    {
                        var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                        selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                    }
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("添加到清单（包括所有子工艺）", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    SelectedListMaterials.Clear();
                    listMaterials.Clear();

                    foreach (var subItem in SelectedRecipe.UnkData5)
                    {
                        var subRecipe = GetIngredientRecipe(subItem.ItemIngredient);
                        if (subRecipe.RowId != 0)
                        {
                            foreach (var subsubItem in subRecipe.UnkData5)
                            {
                                var subsubRecipe = GetIngredientRecipe(subsubItem.ItemIngredient);
                                if (subsubRecipe.RowId != 0)
                                {
                                    for (int i = 1; i <= Math.Ceiling((double)subsubItem.AmountIngredient / (double)subsubRecipe.AmountResult); i++)
                                    {
                                        if (selectedList.Items.IndexOf(subsubRecipe.RowId) == -1)
                                        {
                                            selectedList.Items.Add(subsubRecipe.RowId);
                                        }
                                        else
                                        {
                                            var indexOfLast = selectedList.Items.IndexOf(subsubRecipe.RowId);
                                            selectedList.Items.Insert(indexOfLast, subsubRecipe.RowId);
                                        }

                                    }
                                }
                            }
                            for (int i = 1; i <= Math.Ceiling((double)subItem.AmountIngredient / (double)subRecipe.AmountResult); i++)
                            {
                                if (selectedList.Items.IndexOf(subRecipe.RowId) == -1)
                                {
                                    selectedList.Items.Add(subRecipe.RowId);
                                }
                                else
                                {
                                    var indexOfLast = selectedList.Items.IndexOf(subRecipe.RowId);
                                    selectedList.Items.Insert(indexOfLast, subRecipe.RowId);
                                }
                            }
                        }
                    }

                    if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                    {
                        selectedList.Items.Add(SelectedRecipe.RowId);
                    }
                    else
                    {
                        var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                        selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                    }

                    Service.Configuration.Save();
                }
            }
        }

        private static void DrawRecipeSubTable()
        {
            if (ImGui.BeginTable("###SubTable", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("物品栏", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (subtableList.Count == 0)
                    subtableList = SelectedRecipeRawIngredients.Distinct().OrderByDescending(x => x).ToList();

                try
                {
                    foreach (var item in CollectionsMarshal.AsSpan(subtableList))
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item]) continue;
                            ImGui.PushID(item);
                            var name = sheetItem.Name.RawString;
                            var count = SelectedRecipeRawIngredients.Count(x => x == item);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            if (NumberOfIngredient((uint)item) >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.Text($"{NumberOfIngredient((uint)item)}");
                            ImGui.PopID();
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }
        }

        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList, bool addSubList = true)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                        if (GetIngredientRecipe(ing.ItemIngredient).RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "ERROR");
            }
        }

        private static void DrawRecipes()
        {
            ImGui.Text("搜索");
            ImGui.SameLine();
            ImGui.InputText("###RecipeSearch", ref Search, 100);
            if (ImGui.Selectable("", SelectedRecipe == null))
            {
                SelectedRecipe = null;
            }

            if (Service.Configuration.ShowOnlyCraftable && CraftableItems.Count > 0)
            {
                foreach (var recipe in CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    ImGui.PushID((int)recipe.RowId);
                    var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }
                    ImGui.PopID();
                }
            }
            else
            {
                foreach (var recipe in CollectionsMarshal.AsSpan(FilteredList.Values.ToList()))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(recipe.ItemResult.Value.Name.RawString)) continue;
                        if (!recipe.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)) continue;
                        CheckForIngredients(recipe);
                        rawIngredientsList.Clear();
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            subtableList.Clear();
                            SelectedRecipeRawIngredients.Clear();
                            SelectedRecipe = recipe;
                        }

                    }
                    catch (Exception ex)
                    {
                        Dalamud.Logging.PluginLog.Error(ex, "DrawRecipeList");
                    }
                }
            }
        }

        public unsafe static bool CheckForIngredients(Recipe recipe, bool fetchFromCache = true)
        {
            if (fetchFromCache)
                if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.UnkData5.Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient);
                    int? invNumberHQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient, true);

                    if (value.AmountIngredient > (invNumberNQ + invNumberHQ))
                    {
                        invNumberHQ = null;
                        invNumberNQ = null;

                        CraftableItems[recipe] = false;
                        return false;
                    }

                    invNumberHQ = null;
                    invNumberNQ = null;
                }
                catch
                {

                }

            }

            CraftableItems[recipe] = true;
            return true;
        }

        private static bool HasRawIngredients(int itemIngredient, byte amountIngredient)
        {
            if (GetIngredientRecipe(itemIngredient).RowId == 0) return false;

            return CheckForIngredients(GetIngredientRecipe(itemIngredient));

        }

        private unsafe static int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true);

                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }
        private static void DrawRecipeOptions()
        {
            {
                List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == SelectedRecipe.ItemResult.Value.Name.RawString).Select(x => x.CraftType.Value.RowId + 8).ToList();
                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                ImGui.Text($"制作职业: {String.Join(", ", jobstrings)}");
            }
            var ItemsRequired = SelectedRecipe.UnkData5;

            if (ImGui.BeginTable("###RecipeTable", 5, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("物品栏", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("方法", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("资源", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();
                try
                {
                    foreach (var value in ItemsRequired.Where(x => x.AmountIngredient > 0))
                    {
                        jobs.Clear();
                        string ingredient = LuminaSheets.ItemSheet[(uint)value.ItemIngredient].Name.RawString;
                        Recipe? ingredientRecipe = GetIngredientRecipe(ingredient);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{ingredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{value.AmountIngredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{NumberOfIngredient((uint)value.ItemIngredient)}");
                        if (NumberOfIngredient((uint)value.ItemIngredient) >= value.AmountIngredient)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            if (ImGui.Button($"已制作###search{ingredientRecipe.RowId}"))
                            {
                                SelectedRecipe = ingredientRecipe;
                            }
                        }
                        else
                        {
                            ImGui.Text("已收集");
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            try
                            {
                                jobs.AddRange(FilteredList.Values.Where(x => x.ItemResult.Value.Name.RawString == ingredient).Select(x => x.CraftType.Value.RowId + 8));
                                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                                ImGui.Text(String.Join(", ", jobstrings));
                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "JobStrings");
                            }

                        }
                        else
                        {
                            try
                            {
                                var gatheringItem = LuminaSheets.GatheringItemSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (gatheringItem != null)
                                {
                                    var jobs = LuminaSheets.GatheringPointBaseSheet?.Values.Where(x => x.Item.Any(y => y == gatheringItem.RowId)).Select(x => x.GatheringType).ToList();
                                    List<string> tempArray = new();
                                    if (jobs.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.RawString);
                                    if (jobs.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.RawString);
                                    if (jobs.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.RawString);
                                    ImGui.Text($"{string.Join(", ", tempArray)}");
                                }
                                else
                                {
                                    var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.ItemIngredient).FirstOrDefault();
                                    if (spearfish != null && spearfish.Value.Value.Item.Value.Name.RawString == ingredient)
                                    {
                                        ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "JobStrings");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dalamud.Logging.PluginLog.Error(ex, "RecipeIngreds");
                }
                ImGui.EndTable();
            }

        }

        public unsafe static void DrawProcessingWindow()
        {
            if (Processing)
            {
                Service.Framework.RunOnFrameworkThread(() => CraftingListFunctions.ProcessList(selectedList));

                ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("加工制作清单", ref Processing, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
                {
                    ImGui.Text($"正在加工中: {selectedList.Name}");
                    ImGui.Separator();
                    ImGui.Spacing();
                    if (CurrentProcessedItem != 0)
                    {
                        ImGuiEx.TextV($"正在尝试制作: {FilteredList[CurrentProcessedItem].ItemResult.Value.Name.RawString}");
                        ImGuiEx.TextV($"总体进展: {CraftingListFunctions.CurrentIndex + 1} / {selectedList.Items.Count}");
                    }

                    if (ImGui.Button("取消"))
                    {
                        Processing = false;
                    }


                }
            }
        }

        public static Recipe? GetIngredientRecipe(string ingredient)
        {
            return FilteredList.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? FilteredList.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }

        public static Recipe GetIngredientRecipe(int ingredient)
        {
            if (FilteredList.Values.Any(x => x.ItemResult.Value.RowId == ingredient))
                return FilteredList.Values.First(x => x.ItemResult.Value.RowId == ingredient);
            else
                return new Recipe() { RowId = 0 };
        }
    }
}
