using System;
using HarmonyLib;
using ItemStatsSystem;

namespace CaiYaStation
{
    /// <summary>
    /// The shipment panel is a modal IMGUI overlay. Without this guard, clicking a panel button
    /// also reaches the native input system and can fire a weapon, use an item, or start an
    /// interaction. Only combat/use/interaction entry points are blocked; movement remains free
    /// so the player can walk away and let the panel auto-close.
    /// </summary>
    internal static class PanelInputGuard
    {
        internal static bool Blocked { get; private set; }
        private static float _releaseAt;

        internal static void Set(bool blocked)
        {
            Blocked = blocked;
            _releaseAt = 0;
        }

        /// <summary>Keep blocking briefly after the panel closes so the closing click cannot fire.</summary>
        internal static void ReleaseDelayed(float seconds)
        {
            if (Blocked) _releaseAt = UnityEngine.Time.unscaledTime + seconds;
        }

        internal static void Tick()
        {
            if (Blocked && _releaseAt > 0 && UnityEngine.Time.unscaledTime >= _releaseAt) Set(false);
        }
    }

    internal sealed class NativePatches
    {
        private const string Owner = "CaiYaStation.PanelInput";
        private readonly Harmony _harmony = new Harmony(Owner);
        private bool _enabled;

        internal void Enable(Action<string> log)
        {
            if (_enabled) return;
            try
            {
                Patch(typeof(CharacterMainControl), "Attack", nameof(BlockPlayerBool));
                Patch(typeof(CharacterMainControl), "StartAction", nameof(BlockPlayerBool));
                Patch(typeof(CharacterMainControl), "PickupItem", nameof(BlockPlayerBool));
                Patch(typeof(CharacterMainControl), "UseItem", nameof(BlockPlayerVoid));
                Patch(typeof(CharacterMainControl), "DropAllItems", nameof(BlockPlayerVoid));
                Patch(typeof(CharacterMainControl), "Trigger", nameof(BlockPlayerVoid));
                Patch(typeof(CharacterMainControl), "SetAdsInput", nameof(BlockPlayerVoid));
                Patch(typeof(CharacterMainControl), "SetRunInput", nameof(BlockPlayerVoid));
                Patch(typeof(CharacterMainControl), "CanRun", nameof(BlockPlayerBool));
                Patch(typeof(CharacterMainControl), "CanUseHand", nameof(BlockPlayerBool));
                Patch(typeof(InteractableBase), "StartInteract", nameof(BlockPlayerInteract));
                Patch(typeof(ItemAgent_Gun), "TransToFire", nameof(BlockPlayerGun));
                Patch(typeof(ItemAgent_Gun), "ShootOneBullet", nameof(BlockPlayerGun));
                foreach (var method in typeof(CharacterMainControl).GetMethods())
                {
                    if (method.Name == "Interact")
                        _harmony.Patch(method, new HarmonyMethod(typeof(NativePatches), nameof(BlockPlayerVoid)));
                }
                _enabled = true;
                log("Panel input guard enabled (Harmony id=" + Owner + ")");
            }
            catch
            {
                Disable();
                throw;
            }
        }

        internal void Disable()
        {
            _enabled = false;
            PanelInputGuard.Set(false);
            try { _harmony.UnpatchAll(Owner); } catch { }
        }

        private void Patch(Type type, string name, string prefix)
        {
            var method = AccessTools.Method(type, name);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            _harmony.Patch(method, new HarmonyMethod(typeof(NativePatches), prefix));
        }

        private static bool Active => PanelInputGuard.Blocked;

        private static bool IsMain(CharacterMainControl? actor)
        {
            try { return actor != null && actor == CharacterMainControl.Main; }
            catch { return false; }
        }

        private static bool BlockPlayerBool(CharacterMainControl __instance, ref bool __result)
        {
            if (!Active || !IsMain(__instance)) return true;
            __result = false;
            return false;
        }

        private static bool BlockPlayerVoid(CharacterMainControl __instance)
        {
            return !Active || !IsMain(__instance);
        }

        private static bool BlockPlayerInteract(InteractableBase __instance, CharacterMainControl __0, ref bool __result)
        {
            if (!Active || !IsMain(__0)) return true;
            __result = false;
            return false;
        }

        private static bool BlockPlayerGun(ItemAgent_Gun __instance)
        {
            if (!Active) return true;
            try { return !IsMain(__instance.Holder); }
            catch { return true; }
        }
    }
}
