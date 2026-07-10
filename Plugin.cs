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
            float armor = limb.GetArmorReduction();
            if (armor <= 0f)
                armor = 1f;
            if (limb.isArm || limb.isLegLimb)
            {
                if (Random.value < 0.5f / armor)
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
    [HarmonyPrefix]
    static bool Prefix(SpikeStabberScript __instance)
    {
        Plugin.Log.LogInfo("Spike CheckStab");

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            __instance.transform.position,
            __instance.transform.up,
            6f);

        bool hitSomething = false;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.transform == __instance.transform)
                continue;

            if (!hit.transform.TryGetComponent(out Limb limb))
                continue;

            if (Random.Range(0f, 1f) >= 0.5f)
                continue;

            limb.rb.AddForce(
                __instance.transform.up * 300f * __instance.damageMult,
                ForceMode2D.Impulse);

            limb.skinHealth -= Random.Range(50f, 80f) / limb.GetArmorReduction() * __instance.damageMult;
            limb.muscleHealth -= Random.Range(30f, 50f) / limb.GetArmorReduction() * __instance.damageMult;
            limb.bleedAmount += Random.Range(0f, 40f) / limb.GetArmorReduction() * __instance.damageMult;
            limb.pain += Random.Range(70f, 100f) / limb.GetArmorReduction() * __instance.damageMult;

            limb.body.shock = 70f * __instance.damageMult;
            limb.body.adrenaline = 100f;

            limb.DamageWearables(__instance.damageMult);

            float armor = limb.GetArmorReduction();
            if (armor <= 0f)
                armor = 1f;

            if (Random.value < (0.3f / armor) * __instance.damageMult)
            {
                if (limb.isArm || limb.isLegLimb)
                {
                    Plugin.Log.LogInfo($"SPIKE DISMEMBER -> {limb.name}");
                    limb.Dismember();
                }
                else
                {
                    Plugin.Log.LogInfo($"SPIKE BREAK -> {limb.name}");
                    limb.BreakBone();
                }
            }

            if (limb.isHead)
            {
                limb.body.consciousness = 0f;

                if (Random.Range(0f, 1f) < 0.5f * __instance.damageMult)
                {
                    limb.body.brainHealth -=
                        Random.Range(5f, 70f) *
                        __instance.damageMult /
                        armor;

                    limb.body.Disfigure();
                }
                else
                {
                    limb.body.RemoveEye();
                }
            }

            if (limb.isHead || limb.isVital)
            {
                limb.body.internalBleeding +=
                    Random.Range(16f, 30f) *
                    __instance.damageMult /
                    armor;
            }

            limb.body.Scream();

            hitSomething = true;

            PlayerCamera.main.shaker.Shake(50f);
        }

        if (hitSomething)
        {
            Sound.Play(
                "loudStab",
                __instance.transform.position,
                false,
                true,
                null,
                1f,
                1f,
                false,
                false);

            __instance.spikeRenderer.sprite = __instance.hitSpikeSprite;
        }

        foreach (Collider2D collider in Physics2D.OverlapCircleAll(
                     __instance.transform.position,
                     15f,
                     LayerMask.GetMask("Body", "Limb")))
        {
            if (collider.TryGetComponent(out Body body))
            {
                body.eyeScareTime = 4f;
                body.eyePanicTime = 0.5f;
            }

            if (collider.TryGetComponent(out Limb limb))
            {
                limb.body.eyeScareTime = 4f;
                limb.body.eyePanicTime = 0.5f;
            }
        }

        return false;
    }
}

internal static class ExplosionHelper
{
    public static void BreakOrDismember(Limb limb)
    {
        if (limb == null || limb.dismembered)
            return;

        if (limb.isArm || limb.isLegLimb)
        {
            Plugin.Log.LogInfo($"Explosion -> Dismember ({limb.name})");
            limb.Dismember();
        }
        else
        {
            Plugin.Log.LogInfo($"Explosion -> BreakBone ({limb.name})");
            limb.BreakBone();
        }
    }
}
[HarmonyPatch(typeof(WorldGeneration), "CreateExplosion")]
internal class ExplosionPatch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo breakBone = AccessTools.Method(typeof(Limb), nameof(Limb.BreakBone));
        MethodInfo replacement = AccessTools.Method(typeof(ExplosionHelper), nameof(ExplosionHelper.BreakOrDismember));

        Plugin.Log.LogInfo("Explosion transpiler applied");

        foreach (CodeInstruction code in instructions)
        {
            if (code.Calls(breakBone))
            {
                Plugin.Log.LogInfo("Explosion: BreakBone replaced");
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
            else
            {
                yield return code;
            }
        }
    }
}
}