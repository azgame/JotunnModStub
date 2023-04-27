using BepInEx;
using HarmonyLib;
using Jotunn;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Jotunn.Configs;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace KezzyModPack
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Minor)]
    internal class KezzyModPack : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.KezzyModPack";
        public const string PluginName = "KezzyModPack";
        public const string PluginVersion = "0.0.1";

        public static Skills.SkillType SailingSkillType = 0;
        public static float SailingSkillImproveTimer = 0.0f;
        public static float SailingSkillImproveTimerMax = 1.0f;

        private Texture2D SailingSkillTexture;
        private Sprite SailingSkillSprite;

        private readonly Harmony harmony = new Harmony(KezzyModPack.PluginGUID);

        private void Awake()
        {
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo("[ KezzyModPack ] Initialize.");

            harmony.PatchAll();

            LoadAssets();
            LoadSkill();
        }

        private void LoadAssets()
        {
            // path to the folder where the mod dll is located
            string modPath = Path.GetDirectoryName(Info.Location);

            // Load texture from filesystem
            SailingSkillTexture = AssetUtils.LoadTexture(Path.Combine(modPath, "Assets/sailingskill.jpg"));
            SailingSkillSprite = Sprite.Create(SailingSkillTexture, new Rect(0f, 0f, SailingSkillTexture.width, SailingSkillTexture.height), Vector2.zero);
        }

        private void LoadSkill()
        {
            SailingSkillType = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = "com.jotunn.KezzyModPack.sailingskill_01",
                Name = "Sailing",
                Description = "Sailing skill affects how you handle bad winds. Sailing skill is gained by actively sailing ships.",
                Icon = SailingSkillSprite,
                IncreaseStep = 0.2f
            });
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetWindAngleFactor))]
        class GetWindAngleFactor_Patch
        {
            static float Postfix(float __result, ref List<Player> ___m_players)
            {
                float SkillValue = 0;
                foreach (Player Player in ___m_players)
                {
                    if (Player.GetControlledShip() != null)
                    {
                        SkillValue = Player.GetSkillLevel(KezzyModPack.SailingSkillType);
                    }
                }

                float SkillRatio = SkillValue / 100.0f;
                __result = Mathf.Lerp(__result, 1.0f, SkillRatio);
                return __result;
            }
        }

        [HarmonyPatch(typeof(Ship), "GetSailForce")]
        class GetSailForce_Patch
        {
            static Vector3 Postfix(Vector3 __result, ref List<Player> ___m_players, ref Vector3 ___m_sailForce, Ship __instance)
            {
                float SkillValue = 0;
                foreach (Player Player in ___m_players)
                {
                    if (Player.GetControlledShip() != null)
                    {
                        SkillValue = Player.GetSkillLevel(KezzyModPack.SailingSkillType);
                    }
                }

                float SkillRatio = SkillValue / 100.0f;
                float SailForceMagnitude = ___m_sailForce.magnitude;
                ___m_sailForce = Vector3.Normalize(Vector3.Lerp(___m_sailForce, __instance.transform.forward, SkillRatio));
                ___m_sailForce *= SailForceMagnitude;
                __result = ___m_sailForce;
                return __result;
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateDoodadControls")]
        class UpdateDoodadControls_Patch
        {
            static void Prefix(float dt, ref IDoodadController ___m_doodadController, Player __instance)
            {
                if (___m_doodadController == null)
                {
                    return;
                }

                ShipControlls ShipControlls = ___m_doodadController as ShipControlls;
                if (ShipControlls == null)
                {
                    return;
                }

                Ship Ship = ShipControlls.m_ship;
                if (Ship == null)
                {
                    return;
                }

                // RudderValue is between -1 to 1
                float ShipRudderModifier = Mathf.Clamp(Mathf.Abs(Ship.GetRudderValue()), 0.65f, 0.9f);
                float SpeedSettingValue = (int)Ship.GetSpeedSetting();
                float MaxSpeedSettingValue = (int)Ship.Speed.Full;
                float ShipSpeedModifier = SpeedSettingValue / MaxSpeedSettingValue;

                float SailingSkillModifier = ShipRudderModifier * ShipSpeedModifier * dt;

                SailingSkillImproveTimer += SailingSkillModifier;
                if (SailingSkillImproveTimer > SailingSkillImproveTimerMax)
                {
                    SailingSkillImproveTimer = 0.0f;
                    __instance.RaiseSkill(SailingSkillType);
                }
            }
        }
    }
}
