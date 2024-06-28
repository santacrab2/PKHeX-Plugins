using System;
using System.Collections.Generic;
using System.Linq;
using static PKHeX.Core.Species;

namespace PKHeX.Core.AutoMod
{
    public static class SimpleEdits
    {
        static SimpleEdits()
        {
            // Make PKHeX use our own marking method
            MarkingApplicator.MarkingMethod = FlagIVsAutoMod;
        }

        internal static readonly int[] RoamingMetLocationBDSP =
        [
            197,
            201,
            354,
            355,
            356,
            357,
            358,
            359,
            361,
            362,
            364,
            365,
            367,
            373,
            375,
            377,
            378,
            379,
            383,
            385,
            392,
            394,
            395,
            397,
            400,
            403,
            404,
            407,
            411,
            412,
            414,
            416,
            420,
        ];

        internal static readonly HashSet<int> AlolanOriginForms =
            [
                019, // Rattata
                020, // Raticate
                027, // Sandshrew
                028, // Sandslash
                037, // Vulpix
                038, // Ninetales
                050, // Diglett
                051, // Dugtrio
                052, // Meowth
                053, // Persian
                074, // Geodude
                075, // Graveler
                076, // Golem
                088, // Grimer
                089, // Muk
            ];

        public static bool IsShinyLockedSpeciesForm(int species, int form)
        {
            var tuple = ((Species)species, form);
            return ShinyLockedSpeciesForm.Contains(tuple);
        }

        private static readonly HashSet<(Species, int)> ShinyLockedSpeciesForm =
            [
                // Cap Pikachus
                (Pikachu, 1),
                (Pikachu, 2),
                (Pikachu, 3),
                (Pikachu, 4),
                (Pikachu, 5),
                (Pikachu, 6),
                (Pikachu, 7),
                (Pikachu, 9),
                (Pichu, 1),
                (Victini, 0),
                (Keldeo, 0),
                (Keldeo, 1),
                (Meloetta, 0),
                // Vivillons
                (Scatterbug, 19),
                (Spewpa, 19),
                (Vivillon, 19),
                // Hoopa
                (Hoopa, 0),
                (Hoopa, 1),
                (Volcanion, 0),
                (Cosmog, 0),
                (Cosmoem, 0),
                (Magearna, 0),
                (Magearna, 1),
                (Marshadow, 0),
                (Kubfu, 0),
                (Urshifu, 0),
                (Urshifu, 1),
                (Zarude, 0),
                (Zarude, 1),
                (Glastrier, 0),
                (Spectrier, 0),
                (Calyrex, 0),
                (Calyrex, 1),
                (Calyrex, 2),
                (Enamorus, 0),
                (Enamorus, 1),
                (Gimmighoul, 1),
                (WoChien, 0),
                (ChienPao, 0),
                (TingLu, 0),
                (ChiYu, 0),
                (Koraidon, 0),
                (Koraidon, 1),
                (Koraidon, 2),
                (Koraidon, 3),
                (Koraidon, 4),
                (Miraidon, 0),
                (Miraidon, 1),
                (Miraidon, 2),
                (Miraidon, 3),
                (Miraidon, 4),
                (WalkingWake, 0),
                (IronLeaves, 0),
                (Okidogi,0),
                (Munkidori,0),
                (Fezandipiti,0),
                (Ogerpon,0),
                (Ogerpon,1),
                (Ogerpon,2),
                (Ogerpon,3),
                (Ogerpon,4),
                (Ogerpon,5),
                (Ogerpon,6),
                (Ogerpon,7),
                (GougingFire, 0),
                (RagingBolt,0),
                (IronBoulder,0),
                (IronCrown,0),
                (Terapagos,0),
                (Terapagos,1),
                (Terapagos,2),
            ];

        public static readonly HashSet<int> Gen1TradeEvos = [(int)Kadabra, (int)Machoke, (int)Graveler, (int)Haunter];

        private static Func<int, int, int> FlagIVsAutoMod(PKM pk)
        {
            return pk.Format < 7 ? GetSimpleMarking : GetComplexMarking;

            // value, index
            static int GetSimpleMarking(int val, int _) => val == 31 ? 1 : 0;
            static int GetComplexMarking(int val, int _)
            {
                return val switch
                {
                    31 => 1,
                    1 => 2,
                    0 => 2,
                    _ => 0,
                };
            }
        }

        /// <summary>
        /// Set Encryption Constant based on PKM Generation
        /// </summary>
        /// <param name="pk">PKM to modify</param>
        /// <param name="enc">Encounter details</param>
        public static void SetEncryptionConstant(this PKM pk, IEncounterable enc)
        {
            if (pk.Format < 6)
                return;

            if ((pk.Species == 658 && pk.Form == 1) || APILegality.IsPIDIVSet(pk, enc)) // Ash-Greninja or raids
                return;

            int gen = pk.Generation;
            if (gen is 3 or 4 or 5)
            {
                var ec = pk.PID;
                pk.EncryptionConstant = ec;
                var pidxor = ((pk.TID16 ^ pk.SID16 ^ (int)(ec & 0xFFFF) ^ (int)(ec >> 16)) & ~0x7) == 8;
                pk.PID = pidxor ? ec ^ 0x80000000 : ec;
                return;
            }
            var wIndex = WurmpleUtil.GetWurmpleEvoGroup(pk.Species);
            if (wIndex != WurmpleEvolution.None)
            {
                pk.EncryptionConstant = WurmpleUtil.GetWurmpleEncryptionConstant(wIndex);
                return;
            }

            if (enc is not ITeraRaid9 && ((pk.Species == (ushort)Maushold && pk.Form == 0) || (pk.Species == (ushort)Dudunsparce && pk.Form == 1)))
            {
                pk.EncryptionConstant = pk.EncryptionConstant / 100 * 100;
                return;
            }

            if (pk.EncryptionConstant != 0)
                return;

            pk.EncryptionConstant = enc is WC8 { PIDType: ShinyType8.FixedValue, EncryptionConstant: 0 } ? 0 : Util.Rand32();
        }

        /// <summary>
        /// Sets shiny value to whatever Boolean is specified. Takes in specific shiny as a Boolean. Ignores it for stuff that is gen 5 or lower. Cant be asked to find out all legality quirks
        /// </summary>
        /// <param name="pk">PKM to modify</param>
        /// <param name="isShiny">Shiny value that needs to be set</param>
        /// <param name="enc">Encounter details</param>
        /// <param name="shiny">Set is shiny</param>
        public static void SetShinyBoolean(this PKM pk, bool isShiny, IEncounterable enc, Shiny shiny)
        {
            if (SimpleEdits.IsShinyLockedSpeciesForm(pk.Species, pk.Form))
                return;

            if (pk.IsShiny == isShiny)
                return; // don't mess with stuff if pk is already shiny. Also do not modify for specific shinies (Most likely event shinies)

            if (!isShiny)
            {
                pk.SetUnshiny();
                return;
            }

            if (enc is EncounterStatic8N or EncounterStatic8NC or EncounterStatic8ND or EncounterStatic8U)
            {
                pk.SetRaidShiny(shiny, enc);
                return;
            }

            if (enc is WC8 w8)
            {
                var isHOMEGift = w8.Location == 30018 || w8.GetOT(2) == "HOME";
                if (isHOMEGift)
                {
                    // Set XOR as 0 so SID comes out as 8 or less, Set TID based on that (kind of like a setshinytid)
                    pk.TID16 = (ushort)(0 ^ (pk.PID & 0xFFFF) ^ (pk.PID >> 16));
                    pk.SID16 = (ushort)Util.Rand.Next(8);
                    return;
                }
            }

            if (pk.Generation > 5 || pk.VC)
            {
                if (enc.Shiny is Shiny.FixedValue or Shiny.Never)
                {
                    return;
                }

                while (true)
                {
                    pk.SetShiny();
                    switch (shiny)
                    {
                        case Shiny.AlwaysSquare when pk.ShinyXor != 0:
                            continue;
                        case Shiny.AlwaysStar when pk.ShinyXor == 0:
                            continue;
                    }
                    return;
                }
            }

            if (enc is MysteryGift mg)
            {
                if (mg.IsEgg || mg is PGT { IsManaphyEgg: true })
                {
                    pk.SetShinySID(); // not SID locked
                    return;
                }

                pk.SetShiny();
                if (pk.Format < 6)
                    return;

                do
                {
                    pk.SetShiny();
                } while (IsBit3Set());

                bool IsBit3Set() => ((pk.TID16 ^ pk.SID16 ^ (int)(pk.PID & 0xFFFF) ^ (int)(pk.PID >> 16)) & ~0x7) == 8;
                return;
            }

            pk.SetShinySID(); // no mg = no lock
            if (isShiny && (pk.Generation == 1 || pk.Generation == 2))
                pk.SetShiny();
            if (pk.Generation != 5)
                return;

            while (true)
            {
                pk.PID = EntityPID.GetRandomPID(Util.Rand, pk.Species, pk.Gender, pk.Version, pk.Nature, pk.Form, pk.PID);
                if (shiny == Shiny.AlwaysSquare && pk.ShinyXor != 0)
                    continue;

                if (shiny == Shiny.AlwaysStar && pk.ShinyXor == 0)
                    continue;

                var validg5sid = pk.SID16 & 1;
                pk.SetShinySID();
                pk.EncryptionConstant = pk.PID;
                var result = (pk.PID & 1) ^ (pk.PID >> 31) ^ (pk.TID16 & 1) ^ (pk.SID16 & 1);
                if ((validg5sid == (pk.SID16 & 1)) && result == 0)
                    break;
            }
        }

        public static void SetRaidShiny(this PKM pk, Shiny shiny, IEncounterable enc)
        {
            if (pk.IsShiny)
                return;

            while (true)
            {
                pk.SetShiny();
                if (pk.Format <= 7)
                    return;

                var xor = pk.ShinyXor;
                if (enc is EncounterStatic8U && xor != 1 && shiny != Shiny.AlwaysSquare)
                    continue;

                if ((shiny == Shiny.AlwaysStar && xor == 1) || (shiny == Shiny.AlwaysSquare && xor == 0) || ((shiny is Shiny.Always or Shiny.Random) && xor < 2)) // allow xor1 and xor0 for den shinies
                    return;
            }
        }

        public static void ClearRelearnMoves(this PKM pk)
        {
            pk.RelearnMove1 = 0;
            pk.RelearnMove2 = 0;
            pk.RelearnMove3 = 0;
            pk.RelearnMove4 = 0;
        }

        public static uint GetShinyPID(int tid, int sid, uint pid, int type)
        {
            return (uint)(((tid ^ sid ^ (pid & 0xFFFF) ^ type) << 16) | (pid & 0xFFFF));
        }

        public static void ApplyHeightWeight(this PKM pk, IEncounterable enc, bool signed = true)
        {
            if (pk.Generation < 8 && pk.Format >= 8 && !pk.GG) // height and weight don't apply prior to GG
                return;

            if (pk is IScaledSizeValue obj) // Deal with this later -- restrictions on starters/statics/alphas, for now roll with whatever encounter DB provides
            {
                obj.HeightAbsolute = obj.CalcHeightAbsolute;
                obj.WeightAbsolute = obj.CalcWeightAbsolute;
                if (pk is PB7 pb1)
                    pb1.ResetCP();

                return;
            }
            if (pk is not IScaledSize size)
                return;

            // fixed height and weight
            if (enc is EncounterStatic9 { Size: not 0 })
                return;

            if (enc is EncounterTrade8b or EncounterTrade9)
                return;

            if (enc is EncounterStatic8a { HasFixedHeight: true } || enc is EncounterStatic8a { HasFixedWeight: true })
                return;

            if (enc is WC8 w8)
            {
                var isHOMEGift = w8.Location == 30018 || w8.GetOT(2) == "HOME";
                if (isHOMEGift)
                    return;
            }

            if (enc is WC9 wc9)
            {
                size.WeightScalar = (byte)wc9.WeightValue;
                size.HeightScalar = (byte)wc9.HeightValue;
                return;
            }

            if (APILegality.IsPIDIVSet(pk, enc) && !(enc is EncounterStatic8N or EncounterStatic8NC or EncounterStatic8ND) && !(enc is EncounterEgg && GameVersion.BDSP.Contains(enc.Version)))
                return;

            if (enc is EncounterStatic8N or EncounterStatic8NC or EncounterStatic8ND)
                return;

            var height = 0x12;
            var weight = 0x97;
            var scale = 0xFB;
            if (signed)
            {
                if (GameVersion.SWSH.Contains(pk.Version) || GameVersion.BDSP.Contains(pk.Version) || GameVersion.SV.Contains(pk.Version))
                {
                    var top = (int)(pk.PID >> 16);
                    var bottom = (int)(pk.PID & 0xFFFF);
                    height = (top % 0x80) + (bottom % 0x81);
                    weight = ((int)(pk.EncryptionConstant >> 16) % 0x80) + ((int)(pk.EncryptionConstant & 0xFFFF) % 0x81);
                    scale = ((int)(pk.PID >> 16)*height % 0x80) + ((int)(pk.PID &0xFFFF)*height % 0x81);
                }
                else if (pk.GG)
                {
                    height = (int)(pk.PID >> 16) % 0xFF;
                    weight = (int)(pk.PID & 0xFFFF) % 0xFF;
                    scale = (int)(pk.PID >> 8) % 0xFF;
                }
            }
            else
            {
                height = Util.Rand.Next(255);
                weight = Util.Rand.Next(255);
                scale = Util.Rand.Next(255);
            }
            size.HeightScalar = (byte)height;
            size.WeightScalar = (byte)weight;
            if (pk is IScaledSize3 sz3 && enc is not EncounterFixed9 && sz3.Scale != 128)
                    sz3.Scale = (byte)scale;
        }

        public static void ClearHyperTraining(this PKM pk)
        {
            if (pk is IHyperTrain h)
                h.HyperTrainClear();
        }

        public static string? GetBatchValue(this IBattleTemplate set, string key)
        {
            var batchexists = set is RegenTemplate rt && rt.Regen.HasBatchSettings;
            if (!batchexists)
                return null;

            rt = (RegenTemplate)set;
            foreach (var instruction in rt.Regen.Batch.Instructions)
            {
                if (instruction.PropertyName != key)
                    continue;

                return instruction.PropertyValue;
            }
            return null;
        }

        public static void SetFriendship(this PKM pk, IEncounterable enc)
        {
            bool neverOT = !HistoryVerifier.GetCanOTHandle(enc, pk, enc.Generation);
            if (enc.Generation <= 2)
            {
                pk.OriginalTrainerFriendship = (byte)GetBaseFriendship(EntityContext.Gen7, pk.Species, pk.Form); // VC transfers use SM personal info
            }
            else if (neverOT)
            {
                pk.OriginalTrainerFriendship = (byte)GetBaseFriendship(enc);
            }
            else
            {
                pk.CurrentFriendship = pk.HasMove(218) ? (byte)0 : (byte)255;
            }
        }

        public static void SetBelugaValues(this PKM pk)
        {
            if (pk is PB7 pb7)
                pb7.ResetCalculatedValues();
        }

        public static void SetAwakenedValues(this PKM pk, IBattleTemplate set)
        {
            if (pk is not IAwakened pb7)
                return;

            Span<byte> result = stackalloc byte[6];
            AwakeningUtil.SetExpectedMinimumAVs(result, (PB7)pb7);
            var EVs = set.EVs;
            var MaxAV = (byte)200;
            pb7.AV_HP  = Math.Clamp((byte)EVs[0], result[0], MaxAV);
            pb7.AV_ATK = Math.Clamp((byte)EVs[1], result[1], MaxAV);
            pb7.AV_DEF = Math.Clamp((byte)EVs[2], result[2], MaxAV);
            pb7.AV_SPA = Math.Clamp((byte)EVs[4], result[3], MaxAV);
            pb7.AV_SPD = Math.Clamp((byte)EVs[5], result[4], MaxAV);
            pb7.AV_SPE = Math.Clamp((byte)EVs[3], result[5], MaxAV);
        }

        public static void SetHTLanguage(this PKM pk, byte prefer)
        {
            var pref_lang = (LanguageID)prefer;
            if (pref_lang == LanguageID.Hacked || pref_lang == LanguageID.UNUSED_6)
                prefer = 2; // prefer english

            if (pk is IHandlerLanguage pkm)
                pkm.HandlingTrainerLanguage = prefer;
        }

        public static void SetGigantamaxFactor(this PKM pk, IBattleTemplate set, IEncounterable enc)
        {
            if (pk is not IGigantamax gmax || gmax.CanGigantamax == set.CanGigantamax)
                return;

            if (Gigantamax.CanToggle(pk.Species, pk.Form, enc.Species, enc.Form))
                gmax.CanGigantamax = set.CanGigantamax; // soup hax
        }

        public static void SetGimmicks(this PKM pk, IBattleTemplate set)
        {
            if (pk is IDynamaxLevel d)
                d.DynamaxLevel = d.GetSuggestedDynamaxLevel(pk, requested: set.DynamaxLevel);

            if (pk is ITeraType t && set.TeraType != MoveType.Any && t.GetTeraType() != set.TeraType)
                t.SetTeraType(set.TeraType);
        }

           
           


        

        public static void RestoreIVs(this PKM pk, int[] IVs)
        {
            pk.IVs = IVs;
            pk.ClearHyperTraining();
        }

        public static void HyperTrain(this PKM pk, int[]? IVs = null)
        {
            if (pk is not IHyperTrain t || pk.CurrentLevel != 100)
                return;

            IVs ??= pk.IVs;
            t.HT_HP = pk.IV_HP != 31;
            t.HT_ATK = pk.IV_ATK != 31 && IVs[1] > 2;
            t.HT_DEF = pk.IV_DEF != 31;
            t.HT_SPA = pk.IV_SPA != 31 && IVs[4] > 2;
            t.HT_SPD = pk.IV_SPD != 31;
            t.HT_SPE = pk.IV_SPE != 31 && IVs[3] > 2;

            if (pk is PB7 pb)
                pb.ResetCP();
        }

        public static void SetSuggestedMemories(this PKM pk)
        {
            switch (pk)
            {
                case PK9 pk9 when !pk.IsUntraded:
                    pk9.ClearMemoriesHT();
                    break;
                case PA8 pa8 when !pk.IsUntraded:
                    pa8.ClearMemoriesHT();
                    break;
                case PB8 pb8 when !pk.IsUntraded:
                    pb8.ClearMemoriesHT();
                    break;
                case PK8 pk8 when !pk.IsUntraded:
                    pk8.SetTradeMemoryHT8();
                    break;
                case PK7 pk7 when !pk.IsUntraded:
                    pk7.SetTradeMemoryHT6(true);
                    break;
                case PK6 pk6 when !pk.IsUntraded:
                    pk6.SetTradeMemoryHT6(true);
                    break;
            }
        }

        private static int GetBaseFriendship(IEncounterTemplate enc) => enc switch
            {
                IFixedOTFriendship f => f.OriginalTrainerFriendship,
                { Version: GameVersion.BD or GameVersion.SP } => PersonalTable.SWSH.GetFormEntry(enc.Species, enc.Form).BaseFriendship,
                _ => GetBaseFriendship(enc.Context, enc.Species, enc.Form),
            };

        private static int GetBaseFriendship(EntityContext context, ushort species, byte form)
        {
            return context switch
            {
                EntityContext.Gen1 => PersonalTable.USUM[species].BaseFriendship,
                EntityContext.Gen2 => PersonalTable.USUM[species].BaseFriendship,
                EntityContext.Gen6 => PersonalTable.AO[species].BaseFriendship,
                EntityContext.Gen7 => PersonalTable.USUM[species].BaseFriendship,
                EntityContext.Gen7b => PersonalTable.GG[species].BaseFriendship,
                EntityContext.Gen8 => PersonalTable.SWSH.GetFormEntry(species, form).BaseFriendship,
                EntityContext.Gen8a => PersonalTable.LA.GetFormEntry(species, form).BaseFriendship,
                EntityContext.Gen8b => PersonalTable.BDSP.GetFormEntry(species, form).BaseFriendship,
                EntityContext.Gen9 => PersonalTable.SV.GetFormEntry(species, form).BaseFriendship,
                _ => throw new IndexOutOfRangeException(),
            };
        }

        /// <summary>
        /// Set TID, SID and OT
        /// </summary>
        /// <param name="pk">PKM to set trainer data to</param>
        /// <param name="trainer">Trainer data</param>
        public static void SetTrainerData(this PKM pk, ITrainerInfo trainer)
        {
            pk.TID16 = trainer.TID16;
            pk.SID16 = pk.Generation >= 3 ? trainer.SID16 : (ushort)0;
            pk.OriginalTrainerName = trainer.OT;
        }

        /// <summary>
        /// Set Handling Trainer data for a given PKM
        /// </summary>
        /// <param name="pk">PKM to modify</param>
        /// <param name="trainer">Trainer to handle the <see cref="pk"/></param>
        public static void SetHandlerandMemory(this PKM pk, ITrainerInfo trainer, IEncounterable enc)
        {
            if (IsUntradeableEncounter(enc))
                return;

            var expect = trainer.IsFromTrainer(pk) ? 0 : 1;
            if (pk.CurrentHandler == expect && expect == 0)
                return;

            pk.CurrentHandler = 1;
            pk.HandlingTrainerName = trainer.OT;
            pk.HandlingTrainerGender = trainer.Gender;
            pk.SetHTLanguage((byte)trainer.Language);
            pk.SetSuggestedMemories();
        }

        /// <summary>
        /// Set trainer data for a legal PKM
        /// </summary>
        /// <param name="pk">Legal PKM for setting the data</param>
        /// <param name="trainer"></param>
        /// <returns>PKM with the necessary values modified to reflect trainer data changes</returns>
        public static void SetAllTrainerData(this PKM pk, ITrainerInfo trainer)
        {
            pk.SetBelugaValues(); // trainer details changed?

            if (pk is not IGeoTrack gt)
                return;

            if (trainer is IRegionOrigin o)
            {
                gt.ConsoleRegion = o.ConsoleRegion;
                gt.Country = o.Country;
                gt.Region = o.Region;
                if (pk is PK7 pk7 && pk.Generation <= 2)
                    pk7.FixVCRegion();

                if (pk.Species is (int)Vivillon or (int)Spewpa or (int)Scatterbug)
                    pk.FixVivillonRegion();

                return;
            }

            gt.ConsoleRegion = 1; // North America
            gt.Country = 49; // USA
            gt.Region = 7; // California
        }

        /// <summary>
        /// Sets a move set which is suggested based on calculated legality.
        /// </summary>
        /// <param name="pk">Legal PKM for setting the data</param>
        /// <param name="random">True for Random assortment of legal moves, false if current moves only.</param>
        public static void SetSuggestedMoves(this PKM pk, bool random = false)
        {
            Span<ushort> m = stackalloc ushort[4];
            pk.GetMoveSet(m, random);
            var moves = m.ToArray();
            if (moves.All(z => z == 0))
                return;

            if (pk.Moves.SequenceEqual(moves))
                return;

            pk.SetMoves(moves);
        }

        /// <summary>
        /// Set Dates for date locked Pokémon
        /// </summary>
        /// <param name="pk">Pokémon file to modify</param>
        /// <param name="enc">encounter used to generate Pokémon file</param>
        public static void SetDateLocks(this PKM pk, IEncounterable enc)
        {
            if (enc is WC8 { IsHOMEGift: true } wc8)
                SetDateLocksWC8(pk, wc8);
        }

        private static void SetDateLocksWC8(PKM pk, WC8 w)
        {
            var locked = EncounterServerDate.WC8Gifts.TryGetValue(w.CardID, out var time);
            if (locked)
                pk.MetDate = time.Start;
        }

        public static bool TryApplyHardcodedSeedWild8(PK8 pk, IEncounterable enc, int[] ivs, Shiny requestedShiny)
        {
            // Don't bother if there is no overworld correlation
            if (enc is not IOverworldCorrelation8 eo)
                return false;

            // Check if a seed exists
            var flawless = Overworld8Search.GetFlawlessIVCount(enc, ivs, out var seed);

            // Ensure requested criteria matches
            if (flawless == -1)
                return false;

            APILegality.FindWildPIDIV8(pk, requestedShiny, flawless, seed);
            return eo.IsOverworldCorrelationCorrect(pk) && requestedShiny switch
                {
                    Shiny.AlwaysStar when pk.ShinyXor is 0 or > 15 => false,
                    Shiny.Never when pk.ShinyXor < 16 => false,
                    _ => true,
                };
        }

        public static bool ExistsInGame(this GameVersion destVer, ushort species, byte form)
        {
            // Don't process if Game is LGPE and requested PKM is not Kanto / Meltan / Melmetal
            // Don't process if Game is SWSH and requested PKM is not from the Galar Dex (Zukan8.DexLookup)
            if (GameVersion.GG.Contains(destVer))
                return species is <= 151 or 808 or 809;

            if (GameVersion.SWSH.Contains(destVer))
                return PersonalTable.SWSH.IsPresentInGame(species, form);

            if (GameVersion.PLA.Contains(destVer))
                return PersonalTable.LA.IsPresentInGame(species, form);

            return GameVersion.SV.Contains(destVer) ? PersonalTable.SV.IsPresentInGame(species, form) : (uint)species <= destVer.GetMaxSpeciesID();
        }

        public static GameVersion GetIsland(this GameVersion ver)
        {
            return ver switch
            {
                
                GameVersion.BD or GameVersion.SP => GameVersion.BDSP,
                GameVersion.SW or GameVersion.SH => GameVersion.SWSH,
                GameVersion.GP or GameVersion.GE => GameVersion.GG,
                GameVersion.SN or GameVersion.MN => GameVersion.SM,
                GameVersion.US or GameVersion.UM => GameVersion.USUM,
                GameVersion.X or GameVersion.Y => GameVersion.XY,
                GameVersion.OR or GameVersion.AS => GameVersion.ORAS,
                GameVersion.B or GameVersion.W => GameVersion.BW,
                GameVersion.B2 or GameVersion.W2 => GameVersion.B2W2,
                GameVersion.HG or GameVersion.SS => GameVersion.HGSS,
                GameVersion.FR or GameVersion.LG => GameVersion.FRLG,
                GameVersion.D or GameVersion.P or GameVersion.Pt => GameVersion.DPPt,
                GameVersion.R or GameVersion.S or GameVersion.E => GameVersion.RSE,
                GameVersion.GD or GameVersion.SI or GameVersion.C => GameVersion.GSC,
                GameVersion.RD
                or GameVersion.BU
                or GameVersion.YW
                or GameVersion.GN
                    => GameVersion.Gen1,
                _ => ver
            };
        }

        public static void ApplyPostBatchFixes(this PKM pk)
        {
            if (pk is IScaledSizeValue sv)
            {
                sv.ResetHeight();
                sv.ResetWeight();
            }
        }

        public static bool IsUntradeableEncounter(IEncounterTemplate enc) =>
            enc switch
            {
                EncounterStatic7b { Location: 28 } => true, // LGP/E Starter
                _ => false,
            };

        public static void SetRecordFlags(this PKM pk, ushort[] moves)
        {
            if (pk is ITechRecord tr and not PA8)
            {
                if (pk.Species == (ushort)Species.Hydrapple)
                {
                    ushort[] DC = [(ushort)Move.DragonCheer];
                    tr.SetRecordFlags(DC);
                }
                if (moves.Length != 0)
                {
                    tr.SetRecordFlags(moves);
                }
                else
                {
                    var permit = tr.Permit;
                    for (int i = 0; i < permit.RecordCountUsed; i++)
                    {
                        if (permit.IsRecordPermitted(i))
                            tr.SetMoveRecordFlag(i);
                    }
                }
                return;
            }

            if (pk is IMoveShop8Mastery master)
                master.SetMoveShopFlags(pk);
        }

        public static void SetSuggestedContestStats(this PKM pk, IEncounterable enc)
        {
            var la = new LegalityAnalysis(pk);
            pk.SetSuggestedContestStats(enc, la.Info.EvoChainsAllGens);
        }

        private static readonly ushort[] Arceus_PlateIDs =
        [
            303,
            306,
            304,
            305,
            309,
            308,
            310,
            313,
            298,
            299,
            301,
            300,
            307,
            302,
            311,
            312,
            644
        ];

        public static int? GetArceusHeldItemFromForm(int form) => form is >= 1 and <= 17 ? Arceus_PlateIDs[form - 1] : null;

        public static int? GetSilvallyHeldItemFromForm(int form) => form == 0 ? null : form + 903;

        public static int? GetGenesectHeldItemFromForm(int form) => form == 0 ? null : form + 115;
    }
}
