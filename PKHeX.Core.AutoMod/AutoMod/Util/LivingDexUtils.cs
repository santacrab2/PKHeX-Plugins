namespace PKHeX.Core.AutoMod
{
    public class LivingDexConfig
    {
        public bool IncludeForms { get; init; }
        public bool SetShiny { get; init; }
        public bool SetAlpha { get; init; }
        public bool NativeOnly { get; init; }
        public GameVersion TransferVersion { get; set; }

        public override string ToString()
        {
            return $"TransferVersion: {TransferVersion}\nIncludeForms: {IncludeForms}\nSetShiny: {SetShiny}\nSetAlpha: {SetAlpha}\nNativeOnly: {NativeOnly}";
        }
    }
}
