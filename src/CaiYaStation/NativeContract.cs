using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Duckov.Economy;
using Duckov.MiniMaps;
using Duckov.Scenes;
using ItemStatsSystem;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Preflight for every native member this mod calls. A game update that renames a field or
    /// changes a signature disables the feature with one clear log line instead of failing halfway
    /// through a shipment (which could otherwise lose items).
    /// </summary>
    internal static class NativeContract
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly List<string> Missing = new List<string>();

        internal static int Checked { get; private set; }
        internal static bool Passed => Missing.Count == 0;
        internal static string Status { get; private set; } = "尚未检查";

        internal static void Validate(Action<string> log)
        {
            Missing.Clear();
            Checked = 0;

            Need(typeof(PlayerStorage), "Push", parameters: new[] { typeof(Item), typeof(bool) });
            Need(typeof(PlayerStorage), "IncomingItemBuffer", property: true);
            Need(typeof(PlayerStorage), "Loading", property: true);
            Need(typeof(PlayerStorageBuffer), "Buffer", property: true);
            Need(typeof(ItemUtilities), "GetItemCount", parameters: new[] { typeof(int) });

            Need(typeof(EconomyManager), "Money", property: true);
            Need(typeof(EconomyManager), "Cash", property: true);
            Need(typeof(EconomyManager), "Pay", parameters: new[] { typeof(Cost), typeof(bool), typeof(bool) });
            Need(typeof(EconomyManager), "IsEnough", parameters: new[] { typeof(Cost), typeof(bool), typeof(bool) });
            Checked++;
            if (typeof(Cost).GetConstructor(new[] { typeof(long), typeof(ValueTuple<int, long>[]) }) == null)
                Missing.Add("Cost(long, (int,long)[])");

            Need(typeof(Item), "TypeID", property: true);
            Need(typeof(Item), "DisplayName", property: true);
            Need(typeof(Item), "Value", property: true);
            Need(typeof(Item), "StackCount", property: true);
            Need(typeof(Item), "TotalWeight", property: true);
            Need(typeof(Item), "Tags", property: true);
            Need(typeof(Item), "Inventory", property: true);
            Need(typeof(Item), "CanDrop", property: true);
            Need(typeof(Item), "IsInCharacterSlot");
            Need(typeof(Item), "Detach");
            Need(typeof(Inventory), "Content", property: true);
            Need(typeof(Inventory), "Capacity", property: true);
            Need(typeof(Inventory), "RemoveItem", parameters: new[] { typeof(Item) });
            Need(typeof(ItemTreeExtensions), "DestroyTree", parameters: new[] { typeof(Item) });
            Need(typeof(ItemAssetsCollection), "GetMetaData", parameters: new[] { typeof(int) });
            Need(typeof(ItemAssetsCollection), "TryGetIDByName", parameters: new[] { typeof(string), typeof(bool) });

            Need(typeof(LevelManager), "Instance", property: true);
            Need(typeof(LevelManager), "IsRaidMap", property: true);
            Need(typeof(LevelManager), "IsBaseLevel", property: true);
            Need(typeof(LevelManager), "LevelInited", property: true);
            Need(typeof(LevelManager), "add_OnEvacuated");
            Need(typeof(LevelManager), "add_OnMainCharacterDead");
            Need(typeof(CharacterMainControl), "Main", property: true);
            Need(typeof(CharacterMainControl), "CharacterItem", property: true);
            Need(typeof(CharacterMainControl), "Health", property: true);
            Need(typeof(CharacterMainControl), "Trigger");
            Need(typeof(CharacterMainControl), "SetAdsInput");
            Need(typeof(MultiSceneCore), "Instance", property: true);
            Need(typeof(MultiSceneCore), "MainScene", property: true);
            Need(typeof(MultiSceneCore), "MainSceneID", property: true);
            Need(typeof(MultiSceneCore), "ActiveSubScene", property: true);
            Need(typeof(MultiSceneCore), "ActiveSubSceneID", property: true);
            Need(typeof(MultiSceneCore), "IsLoading", property: true);
            Need(typeof(SceneInfoCollection), "GetSceneID", parameters: new[] { typeof(int) });
            Need(typeof(CountDownArea), "RequiredExtrationTime", property: true);
            Need(typeof(MultiSceneTeleporter), "interactCollider", field: true);

            Need(typeof(InteractableBase), "StartInteract", parameters: new[] { typeof(CharacterMainControl) });
            Need(typeof(InteractableBase), "StopInteract");
            Need(typeof(InteractableBase), "IsInteractable");
            Need(typeof(InteractableBase), "InteractName", property: true);
            Need(typeof(InteractableBase), "otherInterablesInGroup", field: true);
            Need(typeof(InteractableBase), "_interactbleList", field: true);
            Need(typeof(InteractableBase), "interactTime", field: true);

            Need(typeof(SimplePointOfInterest), "Create",
                parameters: new[] { typeof(Vector3), typeof(string), typeof(string), typeof(Sprite), typeof(bool) });
            Need(typeof(Duckov.UI.NotificationText), "Push", parameters: new[] { typeof(string) });

            Need(typeof(Saves.SavesSystem), "Save");
            Need(typeof(Saves.SavesSystem), "Load");
            Need(typeof(Saves.SavesSystem), "KeyExisits", parameters: new[] { typeof(string) });
            Need(typeof(Saves.SavesSystem), "SaveFile", parameters: new[] { typeof(bool) });
            Need(typeof(Saves.SavesSystem), "add_OnSetFile");

            Status = Passed
                ? "通过（" + Checked + " 项）"
                : "缺少 " + Missing.Count + " 项：" + string.Join("、", Missing.Take(8)) + (Missing.Count > 8 ? "…" : "");
            log("Native contract " + (Passed ? "ok" : "FAILED") + " checked=" + Checked
                + (Passed ? "" : " missing=" + string.Join(", ", Missing)));
        }

        private static void Need(Type type, string member, bool property = false, bool field = false, Type[]? parameters = null)
        {
            Checked++;
            MemberInfo? info = null;
            try
            {
                if (parameters != null)
                    info = type.GetMethod(member, All, null, parameters, null);
                else if (property)
                    info = type.GetProperty(member, All);
                else if (field)
                    info = type.GetField(member, All);
                else
                    info = type.GetMethods(All).FirstOrDefault(method => method.Name == member)
                        ?? (MemberInfo?)type.GetProperties(All).FirstOrDefault(prop => prop.Name == member);
            }
            catch (AmbiguousMatchException)
            {
                info = type.GetMethods(All).FirstOrDefault(method => method.Name == member);
            }
            if (info == null) Missing.Add(type.Name + "." + member);
        }
    }
}
