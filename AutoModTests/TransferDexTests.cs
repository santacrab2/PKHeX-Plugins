﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using Xunit;
using static PKHeX.Core.GameVersion;

namespace AutoModTests
{
    public static class TransferDexTests
    {
        static TransferDexTests() => TestUtil.InitializePKHeXEnvironment();

        private static readonly GameVersion[] GetGameVersionsToTest =
        [
            RD,
            C,
            E,
            Pt,
            B,
            B2,
            X,
            OR,
            SN,
            US,
            SW,
            PLA,
            BD,
            SL,
        ];

        private static GenerateResult SingleSaveTest(this GameVersion s, LivingDexConfig cfg)
        {
            var sav = SaveUtil.GetBlankSAV(s, "ALMUT");
            RecentTrainerCache.SetRecentTrainer(sav);

            var expected = sav.GetExpectedDexCount(cfg);
            expected.Should().NotBe(0);

            var pkms = sav.GenerateTLivingDex(cfg).ToArray();
            var genned = pkms.Length;
            var val = new GenerateResult(genned == expected, expected, genned);
            return val;
        }

        public static IEnumerable<object[]> GetLivingDexTestData()
        {
            var cfgs = new LivingDexConfig[]
            {
                CFG_TFFF,
                CFG_TTFF,
                CFG_TTTF,
                CFG_TTTT,
                CFG_TFTF,
                CFG_TFFT,
                CFG_TFTT,
                CFG_TTFT,
                CFG_FTTT,
                CFG_FFTT,
                CFG_FFFT,
                CFG_FFFF,
                CFG_FTFT,
                CFG_FTTF,
                CFG_FTFF,
                CFG_FFTF,
            };
            foreach (var ver in GetGameVersionsToTest)
            {
                for (int i = Array.IndexOf(GetGameVersionsToTest, ver)+1; i < GetGameVersionsToTest.Length; i++)
                {
                    foreach (var cf in cfgs)
                    {
                        yield return new object[] { ver, cf, GetGameVersionsToTest[i] };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetLivingDexTestData))]
        public static void VerifyDex(GameVersion game, LivingDexConfig cfg, GameVersion dest)
        {
            APILegality.Timeout = 99999;
            Legalizer.EnableEasterEggs = false;
            APILegality.SetAllLegalRibbons = false;
            APILegality.EnableDevMode = true;
            cfg.TransferVersion = dest;
            var res = game.SingleSaveTest(cfg);
            res.Success.Should().BeTrue($"GameVersion: {game}\n{cfg}\nExpected: {res.Expected}\nGenerated: {res.Generated}");
        }

        private readonly record struct GenerateResult(bool Success, int Expected, int Generated);

        // Ideally should use purely PKHeX's methods or known total counts so that we're not verifying against ourselves.
        private static int GetExpectedDexCount(this SaveFile sav, LivingDexConfig cfg)
        {
            Dictionary<ushort, List<byte>> speciesDict = [];
            var personal = sav.Personal;
            var destpersonal = SaveUtil.GetBlankSAV(cfg.TransferVersion, "ALM");
            var species = Enumerable.Range(1, sav.MaxSpeciesID).Select(x => (ushort)x);
            foreach (ushort s in species)
            {
                if (!personal.IsSpeciesInGame(s))
                {
                    continue;
                }

                List<byte> forms = [];
                var formCount = personal[s].FormCount;
                var str = GameInfo.Strings;
                if (formCount == 1 && cfg.IncludeForms) // Validate through form lists
                {
                    formCount = (byte)FormConverter.GetFormList(s, str.types, str.forms, GameInfo.GenderSymbolUnicode, sav.Context).Length;
                }

                for (byte f = 0; f < formCount; f++)
                {
                    if (!destpersonal.Personal.IsPresentInGame(s, f) || FormInfo.IsFusedForm(s, f, sav.Generation) || FormInfo.IsBattleOnlyForm(s, f, sav.Generation) || (FormInfo.IsTotemForm(s, f) && sav.Context is not EntityContext.Gen7) || FormInfo.IsLordForm(s, f, sav.Context))
                    {
                        continue;
                    }

                    var valid = sav.GetRandomEncounter(s, f, cfg.SetShiny, cfg.SetAlpha, cfg.NativeOnly, out PKM? pk);
                    if (pk is not null && valid && pk.Form == f && !forms.Contains(f))
                    {
                        forms.Add(f);
                        if (!cfg.IncludeForms)
                        {
                            break;
                        }
                    }
                }

                if (forms.Count > 0)
                {
                    speciesDict.TryAdd(s, forms);
                }
            }

            return cfg.IncludeForms ? speciesDict.Values.Sum(x => x.Count) : speciesDict.Count;
        }

        // const configs
        private static readonly LivingDexConfig CFG_TFFF =
            new()
            {
                IncludeForms = true,
                SetShiny = false,
                SetAlpha = false,
                NativeOnly = false
            };
        private readonly static LivingDexConfig CFG_TTFF =
            new()
            {
                IncludeForms = true,
                SetShiny = true,
                SetAlpha = false,
                NativeOnly = false
            };
        private readonly static LivingDexConfig CFG_TTTF =
            new()
            {
                IncludeForms = true,
                SetShiny = true,
                SetAlpha = true,
                NativeOnly = false
            };
        private readonly static LivingDexConfig CFG_TTTT =
            new()
            {
                IncludeForms = true,
                SetShiny = true,
                SetAlpha = true,
                NativeOnly = true
            };

        private readonly static LivingDexConfig CFG_TFTF =
            new()
            {
                IncludeForms = true,
                SetShiny = false,
                SetAlpha = true,
                NativeOnly = false
            };
        private readonly static LivingDexConfig CFG_TFFT =
            new()
            {
                IncludeForms = true,
                SetShiny = false,
                SetAlpha = false,
                NativeOnly = true
            };
        private readonly static LivingDexConfig CFG_TFTT =
            new()
            {
                IncludeForms = true,
                SetShiny = false,
                SetAlpha = true,
                NativeOnly = true
            };
        private readonly static LivingDexConfig CFG_TTFT =
            new()
            {
                IncludeForms = true,
                SetShiny = true,
                SetAlpha = false,
                NativeOnly = true
            };

        private readonly static LivingDexConfig CFG_FTTT =
            new()
            {
                IncludeForms = false,
                SetShiny = true,
                SetAlpha = true,
                NativeOnly = true
            };
        private readonly static LivingDexConfig CFG_FFTT =
            new()
            {
                IncludeForms = false,
                SetShiny = false,
                SetAlpha = true,
                NativeOnly = true
            };
        private readonly static LivingDexConfig CFG_FFFT =
            new()
            {
                IncludeForms = false,
                SetShiny = false,
                SetAlpha = false,
                NativeOnly = true
            };
        private readonly static LivingDexConfig CFG_FFFF =
            new()
            {
                IncludeForms = false,
                SetShiny = false,
                SetAlpha = false,
                NativeOnly = false
            };

        private readonly static LivingDexConfig CFG_FTFT =
            new()
            {
                IncludeForms = false,
                SetShiny = true,
                SetAlpha = false,
                NativeOnly = true
            };
        private readonly static LivingDexConfig CFG_FTTF =
            new()
            {
                IncludeForms = false,
                SetShiny = true,
                SetAlpha = true,
                NativeOnly = false
            };
        private readonly static LivingDexConfig CFG_FTFF =
            new()
            {
                IncludeForms = false,
                SetShiny = true,
                SetAlpha = false,
                NativeOnly = false
            };
        private readonly static LivingDexConfig CFG_FFTF =
            new()
            {
                IncludeForms = false,
                SetShiny = false,
                SetAlpha = true,
                NativeOnly = false
            };
    }
}
