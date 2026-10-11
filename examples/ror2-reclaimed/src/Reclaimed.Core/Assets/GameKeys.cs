namespace Reclaimed.Core.Assets
{
    /// <summary>
    /// Addressables GUIDs shared by several survivors, from RoR2BepInExPack 1.45.0's asset catalog of the live
    /// game. Boss-specific keys live in each survivor assembly.
    /// </summary>
    public static class GameKeys
    {
        // Expansions (ExpansionDef). Also resolved by name at runtime, see SurvivorBuilder.
        public const string ExpansionDLC1 = "d4f30c23b971a9b428e2796dc04ae099";       // RoR2/DLC1/Common/DLC1.asset
        public const string ExpansionDLC2 = "851f234056d389b42822523d1be6a167";       // RoR2/DLC2/Common/DLC2.asset
        public const string ExpansionDLC3 = "234e83997deed274291470be69e7662e";       // RoR2/DLC3/DLC3.asset
        public const string ExpansionDLC4 = "61c8c007721744c4fa73c87e0c5d0e11";       // RoR2/DLC4/DLC4.asset

        // Crosshairs (GameObject)
        public const string StandardCrosshair = "47727af8137ed1249bea2a23b245cc69";
        public const string SimpleDotCrosshair = "8323d172701659846b133b5a84b01111";
        public const string StraightBracketCrosshair = "780565732a7103949bf39104825dd407";
        public const string TiltedBracketCrosshair = "ebca6cf10a098114380d29d951fdf0f3";
        public const string WideBracketCrosshair = "db2cdf551d14c7a4f97e250608aa6799";
        public const string SMGCrosshair = "f13072af364d6c345a5c1075af77e9b3";

        // Survivor pods (GameObject)
        public const string SurvivorPod = "659066785bfffe94fbbd9183a5b12618";
        public const string RoboCratePod = "99305b04bfde8d84b94c9498332b2fd3";
        public const string VoidSurvivorPod = "454d88518376a1049b66d5dea3d33aba";    // DLC1

        // Camera params (CharacterCameraParams)
        public const string CameraStandard = "1a74ca11b089ae049be7e822f99427a5";
        public const string CameraStandardMelee = "741fdfc223d3a6946a9b353cbc4ddfae";
        public const string CameraStandardTall = "c6de2658e787c8d4d979c75c33afc17a";
        public const string CameraStandardGiant = "efd5ff3f3e087dd4c9094ae7167c2798";
        public const string CameraStandardHuge = "04e4f6c9deb2bed42850abeba622112b";
        public const string CameraStandardRaidboss = "d282feb19b87a9647a60725b91c5bff2";

        // Generic survivor AI masters (GameObject), cloned for Reclaimed AI masters
        public const string CommandoMonsterMaster = "f146e1c7699e35b43b70e119b46875e8";
        public const string MercMonsterMaster = "dfed323f9af24684580c61fea4e9767d";

        // Common VFX (GameObject, EffectComponent)
        public const string OmniImpactVFX = "b6d9c5c6382e0f3489c53a561cabbe4b";
        public const string OmniImpactVFXMedium = "933588d0058f4314da97ea063689a420";
        public const string OmniImpactVFXLarge = "3b894feb7272b134e9191886f744811d";
        public const string OmniImpactVFXSlash = "f73a0e66b3a60aa438a034c416604b91";
        public const string OmniImpactVFXSlashMerc = "2d29caab267130a44bc9b227d51c6139";
        public const string OmniImpactVFXLightning = "f690b264f0a47f640b26f0b8ce3788d9";
        public const string OmniExplosionVFX = "389f7fab9bc92ef478636537a01a2f00";
        public const string OmniExplosionVFXQuick = "851521a751ef1cf45bde684db954c165";
        public const string Hitspark1 = "75a9cf01972ef924d9c434318a2b4483";
        public const string Muzzleflash1 = "39830da81586b134ba9a8138658461ad";
        public const string MuzzleflashBarrage = "7e7f1c63872ea194e9fd183871e41635";
        public const string TeleportOutBoom = "693027002194fcd4081d26578d81ee50";
        public const string HuntressBlinkEffect = "8afce31ca3044da4fbfd73e3eb1f7996";
        public const string MercSwordSlash = "020d3b39bd4f96b4c88acb646738d5e4";
        public const string MercSwordFinisherSlash = "add2601728367ff4ea7af75a73953373";
        public const string LoaderGroundSlam = "0c42c1ede3c560949b81b830428be207";
        public const string ImpactLoaderFistSmall = "0095a3c59ed74f3469423593bc97288e";
        public const string TracerCommandoDefault = "59cdcf63208b10244a7480252f191171";
        public const string ExplosionLunarSun = "17a3c074ce7d1af4694330eea5c15425";    // DLC1
        public const string VoidSurvivorMegaBlasterExplosion = "dcba9eb03813ba14491bbdd7ed1a3b29"; // DLC1

        // Icons (Texture2D)
        public const string TexMysteryIcon = "2205ae9eb97f98e42ac56ee4a9e32474";
    }
}
