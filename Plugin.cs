using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
namespace ImmersiveTraps
{
    [BepInPlugin("com.Morkerost.immersivetraps", "Immersive Traps", "1.2.5")]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            Harmony harmony = new Harmony("com.Morkerost.immersivetraps");
            harmony.PatchAll();

            Logger.LogInfo("Immersive Traps loaded!");
        }
    }

    internal static class TrapHelper
    {
        public static void BreakOrDismember(Limb limb)
        {
            Plugin.Log.LogInfo($"BreakOrDismember: {(limb == null ? "NULL" : limb.name)}");
            if (limb == null || limb.dismembered)
                return;

            float armor = Mathf.Max(1f, limb.GetArmorReduction());
            float chance = 0.5f / armor;

            if (limb.isArm || limb.isLegLimb)
            {
                if (Random.value < chance){
                    Plugin.Log.LogInfo("SPIKE -> DISMEMBER");
                    limb.Dismember();}
                else
                    limb.BreakBone();
            }
            else
            {
                limb.BreakBone();
            }
        }
    }

    [HarmonyPatch(typeof(BearTrap), "OnTriggerEnter2D")]
    internal class BearTrapPatch
    {
        [HarmonyPrefix]
        static bool Prefix(BearTrap __instance, Collider2D other,
            ref bool ___activated,
            ref Limb ___caughtLimb,
            ref Vector2 ___origPos)
        {
            Plugin.Log.LogInfo("BearTrap Prefix called");
            if (___activated)
                return false;

            if (other.TryGetComponent(out Body body))
            {
                body.shock = 20f;
                body.Ragdoll();
            }

            if (!other.TryGetComponent(out Limb limb))
                return false;

            ___activated = true;

            limb.body.shock = 100f;
            limb.pain += 100f;
            limb.body.adrenaline += 80f;

            if (limb.isArm || limb.isLegLimb)
            {
                if (Random.value < 0.5f)
                {
                    limb.Dismember();

                    Sound.Play("beartrap", __instance.transform.position, false, false, null, 1f, 1f, false, false);
                    Sound.Play("gore", limb.transform.position, false, true, null, 1f, 1f, false, false);

                    PlayerCamera.main.shaker.Shake(50f);
                    return false;
                }

                limb.BreakBone();
            }
            else
            {
                if (Random.value < 0.5f)
                    limb.BreakBone();
            }

            ___origPos = limb.transform.position;

            limb.skinHealth -= Random.Range(70f, 100f) / limb.GetArmorReduction();
            limb.muscleHealth -= Random.Range(50f, 100f) / limb.GetArmorReduction();
            limb.bleedAmount += Random.Range(14f, 18f) / limb.GetArmorReduction();
            limb.DamageWearables(0.9f);

            if (limb.isHead)
            {
                limb.body.consciousness = 0f;
                limb.body.brainHealth -= Random.Range(0f, 40f);

                if (Random.value > 0.6f)
                    limb.body.RemoveEye();
            }

            Sound.Play("beartrap", __instance.transform.position, false, false, null, 1f, 1f, false, false);
            Sound.Play("gore", limb.transform.position, false, true, null, 1f, 1f, false, false);

            ___caughtLimb = limb;
            limb.rb.constraints = RigidbodyConstraints2D.FreezeAll;

            __instance.GetComponent<SpriteRenderer>().sprite = __instance.closeSprite;
            UnityEngine.Object.Destroy(__instance.transform.GetChild(0).gameObject);

            PlayerCamera.main.shaker.Shake(50f);

            return false;
        }
    }

    [HarmonyPatch(typeof(SpikeStabberScript), "CheckStab")]
    internal class SpikePatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Plugin.Log.LogInfo("Spike transpiler applied");
            MethodInfo original = AccessTools.Method(typeof(Limb), nameof(Limb.BreakBone));
            MethodInfo replacement = AccessTools.Method(typeof(TrapHelper), nameof(TrapHelper.BreakOrDismember));

            foreach (var code in instructions)
            {
                if (code.Calls(original))
                {
                    Plugin.Log.LogInfo("Spike: BreakBone replaced");
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else
                    yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(WorldGeneration), "CreateExplosion")]
    internal class ExplosionPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(Limb), nameof(Limb.BreakBone));
            MethodInfo replacement = AccessTools.Method(typeof(TrapHelper), nameof(TrapHelper.BreakOrDismember));

            foreach (var code in instructions)
            {
                if (code.Calls(original))
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                else
                    yield return code;
            }
        }
    }


}