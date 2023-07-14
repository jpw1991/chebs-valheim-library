namespace ChebsValheimLibrary.Common
{
    public static class Weather
    {
        public enum Env
        {
            None,
            [InternalName("Clear")] Clear,
            [InternalName("Twilight_Clear")] TwilightClear,
            [InternalName("Misty")] Misty,
            [InternalName("Darklands_dark")] DarklandsDark,
            [InternalName("Heath_clear")] HeathClear,
            [InternalName("DeepForest")] DeepForest,
            [InternalName("Mist")] Mist,
            [InternalName("GDKing")] GDKing,
            [InternalName("Rain")] Rain,
            [InternalName("LightRain")] LightRain,
            [InternalName("ThunderStorm")] ThunderStorm,
            [InternalName("Eikthyr")] Eikthyr,
            [InternalName("GoblinKing")] GoblinKing,
            [InternalName("nofogts")] Nofogts,
            [InternalName("SwampRain")] SwampRain,
            [InternalName("Bonemass")] Bonemass,
            [InternalName("Snow")] Snow,
            [InternalName("Twilight_Snow")] TwilightSnow,
            [InternalName("Twilight_SnowStorm")] TwilightSnowStorm,
            [InternalName("SnowStorm")] SnowStorm,
            [InternalName("Moder")] Moder,
            [InternalName("Ashrain")] Ashrain,
            [InternalName("Crypt")] Crypt,
            [InternalName("SunkenCrypt")] SunkenCrypt
        }
    }
}